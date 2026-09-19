using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Editing;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.App.Tests.ViewModels;

/// <summary>
/// Covers what the status area reports: the document it describes, the operation
/// it is waiting for, the newest condition — including the two cases the direction
/// singles out, a cancellation that is an outcome rather than a failure and a
/// severity that has to read without its colour — and every way a run can end.
/// </summary>
public sealed class ShellStatusTests : IDisposable
{
    private readonly TemporaryWorkflowDirectory _directory = new();

    [Fact]
    public void A_new_status_area_reports_the_document_and_nothing_else_yet()
    {
        ShellStatus status = new(TestSessions.Create());

        status.DocumentState.ShouldBe("Not saved yet");
        status.BackgroundOperation.ShouldBe(ShellStatus.ReadyText);
        status.RunOutcome.ShouldBe(ShellStatus.NoRunText);
        status.Condition.ShouldBe(ShellStatus.NoConditionText);
        status.ConditionSeverity.ShouldBeNull();
    }

    [Fact]
    public void The_document_state_follows_the_session_from_unsaved_to_saved_to_modified()
    {
        EditorSession session = TestSessions.Create();
        ShellStatus status = new(session);
        List<string?> raised = [];
        status.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        session.SaveAs(_directory.PathOf("workflow.vwflow"));

        status.DocumentState.ShouldBe("Saved");
        raised.ShouldContain(nameof(ShellStatus.DocumentState));

        raised.Clear();
        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));

        status.DocumentState.ShouldBe("Unsaved changes");
        raised.ShouldContain(nameof(ShellStatus.DocumentState));
    }

    [Fact]
    public void Beginning_an_operation_names_what_the_shell_is_waiting_for()
    {
        ShellStatus status = new(TestSessions.Create());

        status.Begin(OpenDocumentCommand.OpeningText);

        status.BackgroundOperation.ShouldBe(OpenDocumentCommand.OpeningText);
    }

    [Fact]
    public void A_completed_operation_returns_the_status_to_ready_and_clears_the_condition()
    {
        ShellStatus status = new(TestSessions.Create());
        status.Begin(OpenDocumentCommand.OpeningText);
        status.Report(Failed(DiagnosticCodes.UnreadableDocument));
        status.Begin(OpenDocumentCommand.OpeningText);

        status.Report(new CommandExecutionResult(CommandCompletion.Completed, []));

        // The condition field describes the current state rather than a history of
        // announcements, so an operation that observed nothing clears it.
        status.BackgroundOperation.ShouldBe(ShellStatus.ReadyText);
        status.Condition.ShouldBe(ShellStatus.NoConditionText);
        status.ConditionSeverity.ShouldBeNull();
    }

    [Fact]
    public void An_error_condition_is_named_by_its_severity_word_and_stable_code()
    {
        ShellStatus status = new(TestSessions.Create());

        status.Report(Failed(DiagnosticCodes.UnreadableDocument));

        status.ConditionSeverity.ShouldBe(DiagnosticSeverity.Error);
        status.Condition.ShouldStartWith("Error: ");
        status.Condition.ShouldContain(DiagnosticCodes.UnreadableDocument);
        status.BackgroundOperation.ShouldBe(ShellStatus.ReadyText);
    }

    [Fact]
    public void A_warning_condition_is_named_as_a_warning()
    {
        ShellStatus status = new(TestSessions.Create());

        status.Report(new CommandExecutionResult(
            CommandCompletion.Completed,
            [new NodeDiagnostic(DiagnosticCodes.DroppedDocumentEntry, DiagnosticSeverity.Warning, "One entry was skipped.")]));

        status.ConditionSeverity.ShouldBe(DiagnosticSeverity.Warning);
        status.Condition.ShouldStartWith("Warning: ");
        status.Condition.ShouldContain(DiagnosticCodes.DroppedDocumentEntry);
    }

    [Fact]
    public void A_cancelled_operation_reports_that_the_shell_stopped_and_no_condition()
    {
        ShellStatus status = new(TestSessions.Create());
        status.Report(Failed(DiagnosticCodes.UnreadableDocument));

        status.Report(new CommandExecutionResult(CommandCompletion.Cancelled, []));

        // Cancellation is the outcome the user asked for, so it is named plainly and
        // records nothing: a condition here would report a failure that never happened.
        status.BackgroundOperation.ShouldBe(ShellStatus.StoppedText);
        status.Condition.ShouldBe(ShellStatus.NoConditionText);
        status.ConditionSeverity.ShouldBeNull();
    }

    [Fact]
    public void A_run_in_progress_is_named_as_running()
    {
        ShellStatus status = new(TestSessions.Create());

        status.BeginRun();

        status.RunOutcome.ShouldBe(ShellStatus.RunningText);
    }

    [Fact]
    public void A_run_that_produced_its_outputs_is_named_as_completed()
    {
        ShellStatus status = new(TestSessions.Create());
        status.BeginRun();

        status.ReportRun(Run());

        status.RunOutcome.ShouldBe(ShellStatus.RanText);
    }

    [Fact]
    public void A_run_a_node_failed_in_is_named_as_failed()
    {
        ShellStatus status = new(TestSessions.Create());
        status.BeginRun();

        status.ReportRun(Run(NodeRunState.Failed));

        status.RunOutcome.ShouldBe(ShellStatus.RunFailedText);
    }

    [Fact]
    public void A_run_the_user_stopped_is_named_as_stopped_and_leaves_the_condition_alone()
    {
        ShellStatus status = new(TestSessions.Create());
        status.Report(Failed(DiagnosticCodes.UnreadableDocument));
        status.BeginRun();

        status.ReportRun(Run(cancelled: true));

        // A stopped run returns what it produced rather than throwing, so the shell
        // did finish the Run command — what the run did is the run's own readout, and
        // a condition observed on the way out stays readable beside it.
        status.RunOutcome.ShouldBe(ShellStatus.StoppedText);
        status.BackgroundOperation.ShouldBe(ShellStatus.ReadyText);
        status.Condition.ShouldContain(DiagnosticCodes.UnreadableDocument);
    }

    [Fact]
    public void A_run_that_never_started_says_why()
    {
        ShellStatus status = new(TestSessions.Create());

        status.ReportRunRefused("the workflow has not been saved yet");

        status.RunOutcome.ShouldBe("Not run: the workflow has not been saved yet");
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    /// <summary>
    /// A run summary with one node in it, which is enough for the readout: the
    /// outcome is derived from how the nodes ended and whether the run was stopped.
    /// </summary>
    /// <param name="state">How the one node ended.</param>
    /// <param name="cancelled">Whether cancellation was requested during the run.</param>
    /// <returns>The summary.</returns>
    private static WorkflowRunSummary Run(NodeRunState state = NodeRunState.Succeeded, bool cancelled = false)
        => new()
        {
            OperationId = Guid.NewGuid(),
            DocumentId = Guid.NewGuid(),
            Revision = 1,
            Duration = TimeSpan.FromMilliseconds(12),
            Nodes = [new NodeRunReport(Guid.NewGuid(), state, TimeSpan.FromMilliseconds(12), [])],
            Diagnostics = [],
            WasCancelled = cancelled,
            QuarantinedNodeIds = [],
        };

    private static CommandExecutionResult Failed(string code)
        => new(
            CommandCompletion.Completed,
            [new NodeDiagnostic(code, DiagnosticSeverity.Error, "The document could not be read.")]);
}
