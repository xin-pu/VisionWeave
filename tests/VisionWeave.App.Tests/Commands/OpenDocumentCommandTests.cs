using Microsoft.Extensions.Logging;
using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Tests.Commands;

public sealed class OpenDocumentCommandTests : IDisposable
{
    private readonly RecordingNotificationPresenter _presenter = new();
    private readonly RecordingLogger<AsyncCommandBoundary> _logger = new();
    private readonly TemporaryWorkflowDirectory _directory = new();

    [Fact]
    public async Task OpenCommand_readable_document_opens_it_into_the_session()
    {
        (OpenDocumentCommand command, EditorSession session) = Create(new DocumentLoader(), _directory.SaveReadableDocument());

        await command.Command.ExecuteAsync(null);

        session.Document.Name.ShouldBe("Saved workflow");
        session.Path.ShouldBe(System.IO.Path.GetFullPath(_directory.PathOf("workflow.vwflow")));
        _presenter.Presented.ShouldBeEmpty();
        _logger.Count(LogLevel.Error).ShouldBe(0);
    }

    [Fact]
    public async Task OpenCommand_missing_file_reports_vw_file_001_without_logging_a_defect()
    {
        (OpenDocumentCommand command, EditorSession session) = Create(new DocumentLoader(), _directory.PathOf("absent.vwflow"));

        await command.Command.ExecuteAsync(null);

        NodeDiagnostic reported = _presenter.Presented.ShouldHaveSingleItem();
        reported.Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
        reported.Severity.ShouldBe(DiagnosticSeverity.Error);
        reported.Exception.ShouldNotBeNull();
        session.Path.ShouldBeNull();
        _logger.Count(LogLevel.Error).ShouldBe(0);
    }

    [Fact]
    public async Task OpenCommand_malformed_file_reports_the_readers_own_diagnostic()
    {
        (OpenDocumentCommand command, EditorSession session) = Create(new DocumentLoader(), _directory.SaveUnreadableDocument());

        await command.Command.ExecuteAsync(null);

        _presenter.Presented.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
        session.Document.Name.ShouldBe(EditorSession.UntitledDocumentName);
        _logger.Count(LogLevel.Error).ShouldBe(0);
    }

    [Fact]
    public async Task OpenCommand_blank_path_is_reported_as_an_unreadable_document()
    {
        (OpenDocumentCommand command, _) = Create(new DocumentLoader(), string.Empty);

        await command.Command.ExecuteAsync(null);

        _presenter.Presented.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
        _logger.Count(LogLevel.Error).ShouldBe(0);
    }

    [Fact]
    public async Task OpenCommand_running_open_reports_running_state_and_stops_being_available()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubDocumentLoader loader = new(_ =>
        {
            started.TrySetResult();
            release.Task.Wait();
            return new WorkflowSessionResult(null, []);
        });
        (OpenDocumentCommand command, _) = Create(loader, "any.vwflow");

        Task execution = command.Command.ExecuteAsync(null);
        await started.Task;

        command.Command.IsRunning.ShouldBeTrue();
        command.Command.CanExecute(null).ShouldBeFalse();

        release.SetResult();
        await execution;

        command.Command.IsRunning.ShouldBeFalse();
        command.Command.CanExecute(null).ShouldBeTrue();
        loader.OpenCount.ShouldBe(1);
    }

    [Fact]
    public async Task OpenCommand_cancelled_open_is_not_reported_as_a_failure()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubDocumentLoader loader = new(_ =>
        {
            started.TrySetResult();
            release.Task.Wait();
            return new WorkflowSessionResult(WorkflowSession.New("Loaded"), []);
        });
        (OpenDocumentCommand command, EditorSession session) = Create(loader, "any.vwflow");
        WorkflowSession original = session.Session;

        Task execution = command.Command.ExecuteAsync(null);
        await started.Task;

        command.Command.CanBeCanceled.ShouldBeTrue();
        command.Command.Cancel();
        release.SetResult();
        await execution;

        session.Session.ShouldBeSameAs(original);
        _presenter.Presented.ShouldBeEmpty();
        _logger.Count(LogLevel.Error).ShouldBe(0);
        command.Command.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task OpenCommand_unexpected_loader_failure_is_logged_once_and_presented_as_a_safe_diagnostic()
    {
        StubDocumentLoader loader = new(
            _ => throw new InvalidOperationException(@"The document at C:\private\workflow.vwflow could not be parsed."));
        (OpenDocumentCommand command, _) = Create(loader, "any.vwflow");

        await Should.NotThrowAsync(() => command.Command.ExecuteAsync(null));

        NodeDiagnostic presented = _presenter.Presented.ShouldHaveSingleItem();
        presented.Code.ShouldBe(DiagnosticCodes.UnexpectedCommandFailure);
        presented.Severity.ShouldBe(DiagnosticSeverity.Error);
        presented.Message.ShouldNotContain("private");
        _logger.Count(LogLevel.Error).ShouldBe(1);
        command.Command.IsRunning.ShouldBeFalse();
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    private (OpenDocumentCommand Command, EditorSession Session) Create(IDocumentLoader loader, string path)
    {
        EditorSession session = TestSessions.Create(loader);
        OpenDocumentCommand command = new(new AsyncCommandBoundary(_presenter, _logger), session);
        command.Path = path;
        return (command, session);
    }
}
