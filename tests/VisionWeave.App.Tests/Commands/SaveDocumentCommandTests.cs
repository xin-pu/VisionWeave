using Microsoft.Extensions.Logging;
using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Editing;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Tests.Commands;

/// <summary>
/// The save flow driven the way the shell drives it: a document with a file is
/// written over it, one that has never been saved is asked for a destination, and
/// a write the session refuses is reported rather than thrown. Nothing here opens
/// a dialog: the chooser is the seam that stands in for it.
/// </summary>
public sealed class SaveDocumentCommandTests : IDisposable
{
    private readonly RecordingNotificationPresenter _presenter = new();
    private readonly RecordingLogger<AsyncCommandBoundary> _logger = new();
    private readonly TemporaryWorkflowDirectory _directory = new();

    [Fact]
    public async Task SaveCommand_writes_the_document_over_the_file_it_belongs_to()
    {
        (SaveDocumentCommand command, EditorSession session, ShellStatus status, StubFileChooser chooser) =
            Create();
        session.Open(_directory.SaveReadableDocument()).Session.ShouldNotBeNull();
        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));

        await command.Command.ExecuteAsync(null);

        // The document has a file, so nothing is asked: the write goes where the
        // session already points, and the file now holds the placed node.
        chooser.SaveCount.ShouldBe(0);
        command.Saved.ShouldBeTrue();
        command.Outcome.ShouldBeEmpty();
        session.IsDirty.ShouldBeFalse();
        new DocumentLoader().Open(session.Path!).Session!.Document.Nodes.Count.ShouldBe(1);
        status.BackgroundOperation.ShouldBe(ShellStatus.ReadyText);
        status.DocumentState.ShouldBe("Saved");
        _presenter.Presented.ShouldBeEmpty();
        _logger.Count(LogLevel.Error).ShouldBe(0);
    }

    [Fact]
    public async Task SaveCommand_asks_for_a_destination_when_the_document_has_never_been_saved()
    {
        (SaveDocumentCommand command, EditorSession session, _, StubFileChooser chooser) = Create();
        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));
        string path = _directory.PathOf("chosen.vwflow");
        chooser.AnswerSave(path);

        await command.Command.ExecuteAsync(null);

        // The dialog is offered the document's own name, so the user is asked to
        // confirm a destination rather than to invent one.
        chooser.SuggestedNames.ShouldHaveSingleItem().ShouldBe(EditorSession.UntitledDocumentName);
        command.Saved.ShouldBeTrue();
        session.Path.ShouldBe(System.IO.Path.GetFullPath(path));
        System.IO.File.Exists(path).ShouldBeTrue();
        session.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveCommand_reports_what_the_shell_is_doing_while_the_write_runs()
    {
        StubFileChooser chooser = new();
        (SaveDocumentCommand command, EditorSession session, ShellStatus status, _) = Create(chooser);
        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));

        // The write is small and runs where the gesture happened, so the running
        // state is observed at the one moment the flow waits for the user.
        chooser.AnswerSave(_directory.PathOf("observed.vwflow"));
        List<string> whileAsking = [];
        chooser.OnAskToSave = () => whileAsking.Add(status.BackgroundOperation);

        await command.Command.ExecuteAsync(null);

        whileAsking.ShouldHaveSingleItem().ShouldBe(SaveDocumentCommand.SavingText);
        status.BackgroundOperation.ShouldBe(ShellStatus.ReadyText);
    }

    [Fact]
    public async Task SaveCommand_a_dismissed_destination_writes_nothing_and_says_nothing()
    {
        (SaveDocumentCommand command, EditorSession session, ShellStatus status, StubFileChooser chooser) = Create();
        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));
        chooser.AnswerSave(null);

        await command.Command.ExecuteAsync(null);

        chooser.SaveCount.ShouldBe(1);
        command.Saved.ShouldBeFalse();
        session.Path.ShouldBeNull();
        session.IsDirty.ShouldBeTrue();
        status.DocumentState.ShouldBe("Not saved yet");

        // A dismissed dialog is not an intent, so there is no failure to report and
        // none to log either.
        _presenter.Presented.ShouldBeEmpty();
        _logger.Count(LogLevel.Error).ShouldBe(0);
    }

    [Fact]
    public async Task SaveCommand_refuses_a_read_only_document_and_leaves_its_file_alone()
    {
        (SaveDocumentCommand command, EditorSession session, ShellStatus status, StubFileChooser chooser) = Create();
        string path = _directory.SaveUnsupportedSchemaDocument();
        session.Open(path).Session.ShouldNotBeNull();
        string before = System.IO.File.ReadAllText(path);

        await command.Command.ExecuteAsync(null);

        command.Saved.ShouldBeFalse();
        chooser.SaveCount.ShouldBe(0);
        NodeDiagnostic refused = command.Outcome.ShouldHaveSingleItem();
        refused.Code.ShouldBe(DiagnosticCodes.UnsupportedDocumentSchema);
        refused.Severity.ShouldBe(DiagnosticSeverity.Error);
        System.IO.File.ReadAllText(path).ShouldBe(before);
        status.ConditionSeverity.ShouldBe(DiagnosticSeverity.Error);
        status.Condition.ShouldContain(DiagnosticCodes.UnsupportedDocumentSchema);
        _presenter.Presented.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnsupportedDocumentSchema);
        _logger.Count(LogLevel.Error).ShouldBe(0);
    }

    [Fact]
    public async Task SaveCommand_an_unusable_destination_is_reported_without_logging_a_defect()
    {
        (SaveDocumentCommand command, EditorSession session, _, StubFileChooser chooser) = Create();
        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));

        // The writer creates a missing folder, so the unusable destination is a path
        // that is already a directory.
        string folder = _directory.PathOf("a-folder");
        System.IO.Directory.CreateDirectory(folder);
        chooser.AnswerSave(folder);

        await Should.NotThrowAsync(() => command.Command.ExecuteAsync(null));

        command.Saved.ShouldBeFalse();
        NodeDiagnostic failure = command.Outcome.ShouldHaveSingleItem();
        failure.Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
        failure.Exception.ShouldNotBeNull();
        session.IsDirty.ShouldBeTrue();
        _presenter.Presented.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnreadableDocument);

        // The path classification is what tells an unusable destination from a
        // defect, so the exception stays in the diagnostic and out of the log.
        _logger.Count(LogLevel.Error).ShouldBe(0);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    private (SaveDocumentCommand Command, EditorSession Session, ShellStatus Status, StubFileChooser Chooser) Create(
        StubFileChooser? chooser = null)
    {
        EditorSession session = TestSessions.Create(new DocumentLoader());
        ShellStatus status = new(session);
        StubFileChooser fileChooser = chooser ?? new StubFileChooser();
        var command = new SaveDocumentCommand(
            new AsyncCommandBoundary(_presenter, _logger),
            session,
            status,
            fileChooser);

        return (command, session, status, fileChooser);
    }
}
