using Microsoft.Extensions.Logging;
using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Tests.Support;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Tests.ViewModels;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly RecordingNotificationPresenter _presenter = new();
    private readonly RecordingLogger<AsyncCommandBoundary> _logger = new();
    private readonly TemporaryWorkflowDirectory _directory = new();

    [Fact]
    public async Task OpenDocument_opened_document_replaces_the_session_and_refreshes_the_presentation()
    {
        OpenDocumentCommand openDocument = Create(_directory.SaveReadableDocument());
        MainWindowViewModel viewModel = new(WorkflowSession.New("Untitled"), NodeDefinitionCatalog.Empty, openDocument);
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
        OpenDocumentCommand openDocument = Create(_directory.PathOf("absent.vwflow"));
        MainWindowViewModel viewModel = new(WorkflowSession.New("Untitled"), NodeDefinitionCatalog.Empty, openDocument);
        WorkflowSession original = viewModel.Session;

        await openDocument.Command.ExecuteAsync(null);

        viewModel.Session.ShouldBeSameAs(original);
        viewModel.DocumentTitle.ShouldBe("Untitled");
        _presenter.Presented.Count.ShouldBe(1);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    private OpenDocumentCommand Create(string path)
    {
        OpenDocumentCommand command = new(new AsyncCommandBoundary(_presenter, _logger), new DocumentLoader());
        command.Path = path;
        return command;
    }
}
