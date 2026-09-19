using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Editing;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.App.Tests.ViewModels;

/// <summary>
/// Covers what the status area reports: the document it describes, the operation
/// it is waiting for, and the newest condition — including the two cases the
/// direction singles out, a cancellation that is an outcome rather than a failure
/// and a severity that has to read without its colour.
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

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    private static CommandExecutionResult Failed(string code)
        => new(
            CommandCompletion.Completed,
            [new NodeDiagnostic(code, DiagnosticSeverity.Error, "The document could not be read.")]);
}
