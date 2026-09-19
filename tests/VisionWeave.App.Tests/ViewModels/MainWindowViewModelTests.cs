using Microsoft.Extensions.Logging;
using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Editing;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.App.Tests.ViewModels;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly RecordingNotificationPresenter _presenter = new();
    private readonly RecordingLogger<AsyncCommandBoundary> _logger = new();
    private readonly TemporaryWorkflowDirectory _directory = new();

    [Fact]
    public async Task OpenDocument_opened_document_replaces_the_session_and_refreshes_the_presentation()
    {
        (OpenDocumentCommand openDocument, EditorSession session) = Create(_directory.SaveReadableDocument());
        MainWindowViewModel viewModel = new(session, NodeDefinitionCatalog.Empty, openDocument);
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        string titleBefore = viewModel.WindowTitle;

        await openDocument.Command.ExecuteAsync(null);

        viewModel.Session.Document.Name.ShouldBe("Saved workflow");
        viewModel.DocumentTitle.ShouldBe("workflow.vwflow");
        viewModel.DocumentSummary.ShouldContain("saved");
        viewModel.WindowTitle.ShouldNotBe(titleBefore);
        raised.ShouldContain(nameof(MainWindowViewModel.WindowTitle));
        raised.ShouldContain(nameof(MainWindowViewModel.DocumentTitle));
        raised.ShouldContain(nameof(MainWindowViewModel.DocumentSummary));
    }

    [Fact]
    public async Task OpenDocument_unreadable_file_leaves_the_current_session_in_place()
    {
        (OpenDocumentCommand openDocument, EditorSession session) = Create(_directory.PathOf("absent.vwflow"));
        MainWindowViewModel viewModel = new(session, NodeDefinitionCatalog.Empty, openDocument);

        await openDocument.Command.ExecuteAsync(null);

        viewModel.Session.Path.ShouldBeNull();
        viewModel.DocumentTitle.ShouldBe(EditorSession.UntitledDocumentName);
        _presenter.Presented.Count.ShouldBe(1);
    }

    [Fact]
    public async Task OpenDocument_committed_edit_marks_the_document_summary_unsaved()
    {
        (OpenDocumentCommand openDocument, EditorSession session) = Create(_directory.SaveReadableDocument());
        MainWindowViewModel viewModel = new(session, NodeDefinitionCatalog.Empty, openDocument);
        await openDocument.Command.ExecuteAsync(null);
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));

        viewModel.DocumentSummary.ShouldContain("unsaved changes");
        raised.ShouldContain(nameof(MainWindowViewModel.DocumentSummary));
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    private (OpenDocumentCommand Command, EditorSession Session) Create(string path)
    {
        EditorSession session = TestSessions.Create();
        OpenDocumentCommand command = new(new AsyncCommandBoundary(_presenter, _logger), session);
        command.Path = path;
        return (command, session);
    }
}
