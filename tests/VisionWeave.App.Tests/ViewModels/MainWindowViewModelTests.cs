using Microsoft.Extensions.Logging;
using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Editing;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.App.Tests.ViewModels;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly RecordingNotificationPresenter _presenter = new();
    private readonly RecordingLogger<AsyncCommandBoundary> _logger = new();
    private readonly TemporaryWorkflowDirectory _directory = new();

    [Fact]
    public async Task OpenDocument_opened_document_replaces_the_session_and_refreshes_the_presentation()
    {
        Shell shell = Create(_directory.SaveReadableDocument());
        List<string?> raised = [];
        shell.ViewModel.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        string titleBefore = shell.ViewModel.WindowTitle;

        await shell.OpenDocument.Command.ExecuteAsync(null);

        shell.ViewModel.Session.Document.Name.ShouldBe("Saved workflow");
        shell.ViewModel.DocumentTitle.ShouldBe("workflow.vwflow");
        shell.ViewModel.DocumentCounts.ShouldBe("0 nodes, 0 connections");
        shell.ViewModel.WindowTitle.ShouldNotBe(titleBefore);
        shell.Status.DocumentState.ShouldBe("Saved");
        raised.ShouldContain(nameof(MainWindowViewModel.WindowTitle));
        raised.ShouldContain(nameof(MainWindowViewModel.DocumentTitle));
        raised.ShouldContain(nameof(MainWindowViewModel.DocumentCounts));
    }

    [Fact]
    public async Task OpenDocument_unreadable_file_leaves_the_current_session_in_place()
    {
        Shell shell = Create(_directory.PathOf("absent.vwflow"));
        await shell.OpenDocument.Command.ExecuteAsync(null);

        shell.ViewModel.Session.Path.ShouldBeNull();
        shell.ViewModel.DocumentTitle.ShouldBe(EditorSession.UntitledDocumentName);
        _presenter.Presented.Count.ShouldBe(1);
    }

    [Fact]
    public async Task OpenDocument_committed_edit_marks_the_document_summary_unsaved()
    {
        Shell shell = Create(_directory.SaveReadableDocument());
        await shell.OpenDocument.Command.ExecuteAsync(null);
        List<string?> raised = [];
        shell.ViewModel.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        shell.ViewModel.Session.Execute(
            new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));

        shell.ViewModel.DocumentCounts.ShouldBe("1 node, 0 connections");
        shell.Status.DocumentState.ShouldBe("Unsaved changes");
        raised.ShouldContain(nameof(MainWindowViewModel.DocumentCounts));
        raised.ShouldContain(nameof(MainWindowViewModel.SelectionSummary));
    }

    [Fact]
    public void The_catalogue_groups_the_definitions_the_catalog_holds_by_category()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());

        shell.ViewModel.NodeCatalogSummary.ShouldBe("2 node types available");
        shell.ViewModel.CatalogueGroups.Select(group => group.Category)
            .ShouldBe([OpenCvNodeIds.FilterCategory, OpenCvNodeIds.TransformCategory]);
        shell.ViewModel.CatalogueGroups[0].Entries.Select(entry => entry.DisplayName).ShouldBe(["Gaussian Blur"]);
        shell.ViewModel.CatalogueGroups[1].Entries.Select(entry => entry.DisplayName).ShouldBe(["Resize"]);
    }

    [Fact]
    public async Task Open_asks_the_chooser_for_a_document_and_opens_the_chosen_one()
    {
        Shell shell = Create(_directory.SaveReadableDocument());
        shell.Chooser.Answer(_directory.SaveReadableDocument("second.vwflow"));

        await shell.ViewModel.OpenCommand.ExecuteAsync(null);

        shell.Chooser.AskCount.ShouldBe(1);
        shell.ViewModel.DocumentTitle.ShouldBe("second.vwflow");
        shell.Status.DocumentState.ShouldBe("Saved");
    }

    [Fact]
    public async Task Open_leaves_the_document_alone_when_the_chooser_is_dismissed()
    {
        Shell shell = Create(_directory.SaveReadableDocument());
        shell.Chooser.Answer(null);

        await shell.ViewModel.OpenCommand.ExecuteAsync(null);

        // A dismissed dialog is no intent to carry out, so nothing is opened and
        // nothing is reported.
        shell.ViewModel.DocumentTitle.ShouldBe(EditorSession.UntitledDocumentName);
        shell.Status.DocumentState.ShouldBe("Not saved yet");
        _presenter.Presented.ShouldBeEmpty();
    }

    [Fact]
    public async Task Open_offers_the_file_the_session_is_editing_to_the_chooser()
    {
        Shell shell = Create(_directory.SaveReadableDocument());
        await shell.OpenDocument.Command.ExecuteAsync(null);

        shell.Chooser.Answer(null);
        await shell.ViewModel.OpenCommand.ExecuteAsync(null);

        shell.Chooser.Asked.ShouldHaveSingleItem().ShouldBe(shell.ViewModel.Session.Path);
    }

    [Fact]
    public async Task Open_of_an_unreadable_file_reports_the_condition_in_the_status_area()
    {
        Shell shell = Create(_directory.SaveReadableDocument());
        shell.Chooser.Answer(_directory.SaveUnreadableDocument());

        await shell.ViewModel.OpenCommand.ExecuteAsync(null);

        shell.Status.ConditionSeverity.ShouldBe(DiagnosticSeverity.Error);
        shell.Status.Condition.ShouldContain(DiagnosticCodes.UnreadableDocument);
        shell.Status.DocumentState.ShouldBe("Not saved yet");
        _presenter.Presented.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    private static NodeDefinitionCatalog OpenCvCatalog()
        => NodeDefinitionCatalog.FromProviders([new OpenCvNodeDefinitionProvider()]);

    private Shell Create(string path) => Create(path, NodeDefinitionCatalog.Empty);

    private Shell Create(string path, NodeDefinitionCatalog catalog)
    {
        EditorSession session = TestSessions.Create();
        ShellStatus status = new(session);
        var openDocument = new OpenDocumentCommand(new AsyncCommandBoundary(_presenter, _logger), session, status)
        {
            Path = path,
        };
        StubFileChooser chooser = new();

        return new Shell(
            new MainWindowViewModel(
                session,
                catalog,
                new WorkflowValidator(catalog),
                openDocument,
                chooser,
                status),
            openDocument,
            chooser,
            status);
    }

    /// <summary>
    /// The pieces one test drives, so a test never has to rebuild the wiring the
    /// shell composes at startup.
    /// </summary>
    /// <param name="ViewModel">The view model under test.</param>
    /// <param name="OpenDocument">The command the shell opens a named file with.</param>
    /// <param name="Chooser">The dialog the view model asks for a file.</param>
    /// <param name="Status">The durable state the status area reports.</param>
    private sealed record Shell(
        MainWindowViewModel ViewModel,
        OpenDocumentCommand OpenDocument,
        StubFileChooser Chooser,
        ShellStatus Status);
}
