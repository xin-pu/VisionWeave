using Microsoft.Extensions.Logging;
using Shouldly;
using VisionWeave.App.Commands;
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
    public async Task OpenCommand_readable_document_raises_opened_with_the_loaded_session()
    {
        OpenDocumentCommand command = Create(new DocumentLoader(), _directory.SaveReadableDocument());
        WorkflowSession? opened = null;
        command.Opened += (_, session) => opened = session;

        await command.Command.ExecuteAsync(null);

        opened.ShouldNotBeNull();
        opened.Document.Name.ShouldBe("Saved workflow");
        opened.Path.ShouldBe(System.IO.Path.GetFullPath(_directory.PathOf("workflow.vwflow")));
        _presenter.Presented.ShouldBeEmpty();
        _logger.Count(LogLevel.Error).ShouldBe(0);
    }

    [Fact]
    public async Task OpenCommand_missing_file_reports_vw_file_001_without_logging_a_defect()
    {
        OpenDocumentCommand command = Create(new DocumentLoader(), _directory.PathOf("absent.vwflow"));
        bool opened = false;
        command.Opened += (_, _) => opened = true;

        await command.Command.ExecuteAsync(null);

        NodeDiagnostic reported = _presenter.Presented.ShouldHaveSingleItem();
        reported.Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
        reported.Severity.ShouldBe(DiagnosticSeverity.Error);
        reported.Exception.ShouldNotBeNull();
        opened.ShouldBeFalse();
        _logger.Count(LogLevel.Error).ShouldBe(0);
    }

    [Fact]
    public async Task OpenCommand_malformed_file_reports_the_readers_own_diagnostic()
    {
        OpenDocumentCommand command = Create(new DocumentLoader(), _directory.SaveUnreadableDocument());

        await command.Command.ExecuteAsync(null);

        _presenter.Presented.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
        _logger.Count(LogLevel.Error).ShouldBe(0);
    }

    [Fact]
    public async Task OpenCommand_blank_path_is_reported_as_an_unreadable_document()
    {
        OpenDocumentCommand command = Create(new DocumentLoader(), string.Empty);

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
        OpenDocumentCommand command = Create(loader, "any.vwflow");

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
        OpenDocumentCommand command = Create(loader, "any.vwflow");
        bool opened = false;
        command.Opened += (_, _) => opened = true;

        Task execution = command.Command.ExecuteAsync(null);
        await started.Task;

        command.Command.CanBeCanceled.ShouldBeTrue();
        command.Command.Cancel();
        release.SetResult();
        await execution;

        opened.ShouldBeFalse();
        _presenter.Presented.ShouldBeEmpty();
        _logger.Count(LogLevel.Error).ShouldBe(0);
        command.Command.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task OpenCommand_unexpected_loader_failure_is_logged_once_and_presented_as_a_safe_diagnostic()
    {
        StubDocumentLoader loader = new(
            _ => throw new InvalidOperationException(@"The document at C:\private\workflow.vwflow could not be parsed."));
        OpenDocumentCommand command = Create(loader, "any.vwflow");

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

    private OpenDocumentCommand Create(IDocumentLoader loader, string path)
    {
        OpenDocumentCommand command = new(new AsyncCommandBoundary(_presenter, _logger), loader);
        command.Path = path;
        return command;
    }
}
