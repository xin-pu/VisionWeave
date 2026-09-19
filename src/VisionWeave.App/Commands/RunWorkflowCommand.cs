using CommunityToolkit.Mvvm.Input;
using VisionWeave.App.Sessions;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;

namespace VisionWeave.App.Commands;

/// <summary>
/// Runs the document the shell is editing. The run is blocking work — a file read, a
/// filter, a file write — so it executes off the thread that owns the window's
/// bindings, and it reaches that thread only through the command boundary.
/// </summary>
internal sealed class RunWorkflowCommand
{
    internal const string OperationName = "RunWorkflow";
    internal const string RunningText = "Running the workflow…";

    private const string UnsavedReason = "the workflow has not been saved yet";

    private readonly AsyncCommandBoundary _boundary;
    private readonly EditorSession _session;
    private readonly WorkflowSnapshotFactory _snapshots;
    private readonly ExecutionPlanBuilder _plans;
    private readonly WorkflowRunner _runner;
    private readonly ShellStatus _status;

    internal RunWorkflowCommand(
        AsyncCommandBoundary boundary,
        EditorSession session,
        WorkflowSnapshotFactory snapshots,
        ExecutionPlanBuilder plans,
        WorkflowRunner runner,
        ShellStatus status)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(status);

        _boundary = boundary;
        _session = session;
        _snapshots = snapshots;
        _plans = plans;
        _runner = runner;
        _status = status;

        // The toolkit owns IsRunning, CanExecute, the token source behind Cancel and the
        // notifications that report them, so a second run cannot start while one runs.
        Command = new AsyncRelayCommand(RunAsync, AsyncRelayCommandOptions.None);

        // Stopping is a gesture of its own: available exactly while there is a run to
        // stop, and answered by the toolkit's own cancellation of that run.
        CancelCommand = new RelayCommand(Command.Cancel, () => Command.IsRunning);
        Command.PropertyChanged += OnRunCommandPropertyChanged;
    }

    /// <remarks>Public because the shell's markup binds through it; the type stays internal.</remarks>
    public IAsyncRelayCommand Command { get; }

    /// <remarks>Public for the same reason as <see cref="Command"/>.</remarks>
    public IRelayCommand CancelCommand { get; }

    private void OnRunCommandPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAsyncRelayCommand.IsRunning))
        {
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Runs the document and reports the outcome. The returned task cannot fault.</summary>
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        _status.Begin(RunningText);
        _status.BeginRun();

        CommandExecutionResult result = await _boundary
            .RunAsync(OperationName, RunDocumentAsync, cancellationToken)
            .ConfigureAwait(true);

        _status.Report(result);
    }

    private async Task<IReadOnlyList<NodeDiagnostic>> RunDocumentAsync(CancellationToken cancellationToken)
    {
        if (_session.Path is not { } path)
        {
            // A path is relative to the workflow it belongs to, so a workflow with no
            // file has no folder to resolve one against. Refusing here keeps that reason
            // in one place instead of failing every file-backed node once per run.
            _status.ReportRunRefused(UnsavedReason);

            return
            [
                new NodeDiagnostic(
                    DiagnosticCodes.MissingDocumentPath,
                    DiagnosticSeverity.Error,
                    "The workflow has not been saved, so there is no folder to resolve its file paths against. Save it beside the files it uses and run it again."),
            ];
        }

        SnapshotBuildResult captured = _snapshots.Build(_session.Document);
        if (!captured.Succeeded)
        {
            // Only the errors: they stop the run, while the inspector already shows the
            // whole list at the node and the parameter each condition belongs to.
            IReadOnlyList<NodeDiagnostic> errors =
            [
                .. captured.Validation.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            ];

            _status.ReportRunRefused($"the document reports {Conditions(errors.Count)}");

            return errors;
        }

        ExecutionPlan plan = _plans.Build(
            captured.Snapshot!,
            changedNodeIds: null,
            NodeExecutionEnvironment.At(WorkingDirectoryOf(path)));

        WorkflowRunSummary summary = await Task
            .Run(() => _runner.RunAsync(plan, cancellationToken), CancellationToken.None)
            .ConfigureAwait(true);

        _status.ReportRun(summary);

        return Reported(summary);
    }

    /// <summary>
    /// Reports what the run left behind. A run the user stopped is already named as
    /// stopped in the readout, so the cancellation of each node that never started is
    /// left out; every other diagnostic — a leaked lease, an executor that ignored the
    /// stop — still travels.
    /// </summary>
    private static IReadOnlyList<NodeDiagnostic> Reported(WorkflowRunSummary summary)
        => summary.WasCancelled
            ? [.. summary.Diagnostics.Where(diagnostic => diagnostic.Code != DiagnosticCodes.NodeExecutionCancelled)]
            : summary.Diagnostics;

    /// <summary>The folder the document's own file paths resolve against.</summary>
    private static string WorkingDirectoryOf(string path)
    {
        string full = System.IO.Path.GetFullPath(path);

        return System.IO.Path.GetDirectoryName(full) ?? full;
    }

    private static string Conditions(int count) => count == 1 ? "1 condition" : $"{count} conditions";
}
