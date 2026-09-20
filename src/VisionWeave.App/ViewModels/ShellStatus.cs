using CommunityToolkit.Mvvm.ComponentModel;
using VisionWeave.App.Commands;
using VisionWeave.App.Presentation;
using VisionWeave.App.Sessions;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.ViewModels;

/// <summary>
/// The durable state the status area reports: what the document is, what the shell
/// is doing, what the last run produced, and the newest condition the shell saw.
/// It is the counterpart of the transient snackbar — a condition that is worth
/// keeping in front of the user stays here, and one that is only worth announcing
/// goes to the presenter — and it derives every string from the session and the
/// results a command reports rather than holding a second copy of either.
/// </summary>
internal sealed partial class ShellStatus : ObservableObject
{
    /// <summary>The state reported while the shell is waiting for a gesture.</summary>
    internal const string ReadyText = "Ready";

    /// <summary>
    /// The state reported after the user stopped an operation. Cancellation is an
    /// outcome and not a failure, so it is named plainly and records no condition.
    /// </summary>
    internal const string StoppedText = "Stopped at your request";

    /// <summary>The run-outcome field before anything can run.</summary>
    internal const string NoRunText = "No run yet";

    /// <summary>The run-outcome field while a run is executing.</summary>
    internal const string RunningText = "Running…";

    /// <summary>The run-outcome field for a run that finished and reported no failure.</summary>
    internal const string RanText = "Completed";

    /// <summary>The run-outcome field for a run a node failed in.</summary>
    internal const string RunFailedText = "Failed";

    /// <summary>The condition field before the shell has observed anything.</summary>
    internal const string NoConditionText = "No conditions reported yet";

    private readonly EditorSession _session;

    /// <summary>
    /// Creates the status area over the session it reports on.
    /// </summary>
    /// <param name="session">The session the document state is read from.</param>
    /// <remarks>
    /// The constructor is public because the host's service container resolves this
    /// type and only considers public constructors; the type itself stays internal
    /// to the application.
    /// </remarks>
    public ShellStatus(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;

        BackgroundOperation = ReadyText;
        RunOutcome = NoRunText;
        Condition = NoConditionText;

        _session.PropertyChanged += OnSessionPropertyChanged;
    }

    /// <summary>
    /// Gets what the shell is doing, or what it last did: the operation while it
    /// runs, or the state it returned to when it finished.
    /// </summary>
    /// <remarks>
    /// The properties below are public because the status area is bound to them and
    /// WPF binds only to public members: a non-public one fails silently, leaving an
    /// empty field rather than an error. The type itself stays internal.
    /// </remarks>
    [ObservableProperty]
    public partial string BackgroundOperation { get; private set; }

    /// <summary>
    /// Gets the outcome of the last workflow run: that it is running, what it
    /// produced, that the user stopped it, or why it was never started.
    /// </summary>
    [ObservableProperty]
    public partial string RunOutcome { get; private set; }

    /// <summary>
    /// Gets the newest condition the shell observed, named by its severity word,
    /// its stable code, and its safe message, so the state is readable without
    /// relying on colour.
    /// </summary>
    [ObservableProperty]
    public partial string Condition { get; private set; }

    /// <summary>
    /// Gets how serious that condition is, which the status area also uses to pick
    /// a colour. It carries no meaning on its own: the wording says the same thing.
    /// </summary>
    [ObservableProperty]
    public partial DiagnosticSeverity? ConditionSeverity { get; private set; }

    /// <summary>
    /// Gets the newest run the shell was told about, which the canvas marks its nodes
    /// from. It is <see langword="null"/> until a run finishes, and a run that never
    /// started leaves it <see langword="null"/> rather than holding the run before it,
    /// because the marks describe a run of the graph rather than a run that was asked
    /// for. The projection decides whether a summary still describes the document on
    /// screen, so nothing is filtered here.
    /// </summary>
    [ObservableProperty]
    public partial WorkflowRunSummary? LastRun { get; private set; }

    /// <summary>
    /// Gets the state of the document being edited: whether it has ever been saved
    /// and whether it holds changes its file does not.
    /// </summary>
    public string DocumentState
        => _session.Path is null
            ? "Not saved yet"
            : _session.IsDirty ? "Unsaved changes" : "Saved";

    /// <summary>
    /// Reports that an operation started, so the status area names what the shell
    /// is waiting for instead of showing the previous outcome.
    /// </summary>
    /// <param name="activity">The phrase describing the operation, such as "Opening a document…".</param>
    internal void Begin(string activity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activity);

        BackgroundOperation = activity;
    }

    /// <summary>
    /// Reports how an operation finished.
    /// </summary>
    /// <param name="result">The outcome the command boundary returned.</param>
    internal void Report(CommandExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Completion == CommandCompletion.Cancelled)
        {
            // The user stopped the operation, so nothing was observed. The status
            // says the shell stopped and clears the condition, exactly as an
            // operation that observed nothing does: an old condition left standing
            // would read as the outcome of the operation that was just stopped.
            BackgroundOperation = StoppedText;
            ClearCondition();
            return;
        }

        BackgroundOperation = ReadyText;

        // The condition field describes the shell's current state rather than a
        // history of every message, so an operation that observed nothing clears
        // it. The record of what happened belongs to the log and, later, to the
        // diagnostics panel.
        if (result.Diagnostics.Count == 0)
        {
            ClearCondition();
            return;
        }

        NodeDiagnostic diagnostic = result.Diagnostics[0];
        ConditionSeverity = diagnostic.Severity;
        Condition = DiagnosticText.Of(diagnostic);
    }

    private void ClearCondition()
    {
        ConditionSeverity = null;
        Condition = NoConditionText;
    }

    /// <summary>
    /// Reports that a run started, so the readout names the work in progress rather
    /// than the outcome of the run before it. The marks of the run before it go with
    /// it: while a run is in flight there is no finished run for the canvas to
    /// describe, and leaving the last one standing would say a node succeeded while
    /// the run that is about to report on it is still going.
    /// </summary>
    internal void BeginRun()
    {
        RunOutcome = RunningText;
        LastRun = null;
    }

    /// <summary>
    /// Reports what a run produced.
    /// </summary>
    /// <param name="summary">The outcome the runner returned.</param>
    /// <remarks>
    /// Only the run readout is written. A stopped run returns what it produced
    /// rather than throwing, so the shell did finish the Run operation and the
    /// operation field reads the same as it would for any command that returned —
    /// the stop is a statement about the run, and this is where the run is read.
    /// The condition field is left alone, so a condition the run observed on its way
    /// out stays readable.
    /// </remarks>
    internal void ReportRun(WorkflowRunSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        LastRun = summary;
        RunOutcome = summary.WasCancelled
            ? StoppedText
            : summary.Status == WorkflowRunStatus.Failed ? RunFailedText : RanText;
    }

    /// <summary>
    /// Reports that a run never started, and why. A refusal is a state rather than a
    /// history: leaving the outcome of the run before it standing would describe work
    /// the user did not ask for.
    /// </summary>
    /// <param name="reason">What prevented the run, as the rest of the sentence reads it.</param>
    internal void ReportRunRefused(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        LastRun = null;
        RunOutcome = $"Not run: {reason}";
    }

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => OnPropertyChanged(nameof(DocumentState));
}
