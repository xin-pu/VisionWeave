using Microsoft.Extensions.Logging;
using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Preview;
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

        shell.ViewModel.NodeCatalogSummary.ShouldBe("4 node types available");
        shell.ViewModel.CatalogueGroups.Select(group => group.Category)
            .ShouldBe([OpenCvNodeIds.FilterCategory, OpenCvNodeIds.InputOutputCategory, OpenCvNodeIds.TransformCategory]);
        shell.ViewModel.CatalogueGroups[0].Entries.Select(entry => entry.DisplayName).ShouldBe(["Gaussian Blur"]);
        shell.ViewModel.CatalogueGroups[1].Entries.Select(entry => entry.DisplayName)
            .ShouldBe(["Image Source", "Save Image"]);
        shell.ViewModel.CatalogueGroups[2].Entries.Select(entry => entry.DisplayName).ShouldBe(["Resize"]);
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

    [Fact]
    public async Task Open_with_unsaved_changes_saves_them_when_the_user_says_save()
    {
        Shell shell = Create(_directory.SaveReadableDocument());
        await shell.OpenDocument.Command.ExecuteAsync(null);
        shell.ViewModel.Session.Execute(
            new AddNodeCommand(new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId), 1, new CanvasPosition(0, 0)));
        string saved = shell.ViewModel.Session.Path!;
        shell.Chooser.Answer(_directory.SaveReadableDocument("second.vwflow"));

        Task opening = shell.ViewModel.OpenCommand.ExecuteAsync(null);
        shell.Prompt.IsOpen.ShouldBeTrue();
        shell.Prompt.Question.ShouldContain("unsaved changes");
        shell.Prompt.AcceptCommand.Execute(null);
        await opening;

        // The answer wrote the document before the other one was opened, so the work
        // the user still held is in its file rather than lost to the swap.
        shell.Chooser.SaveCount.ShouldBe(0);
        new DocumentLoader().Open(saved).Session!.Document.Nodes.Count.ShouldBe(1);
        shell.ViewModel.DocumentTitle.ShouldBe("second.vwflow");
        shell.Status.DocumentState.ShouldBe("Saved");
        shell.Prompt.IsOpen.ShouldBeFalse();
    }

    [Fact]
    public async Task Open_with_unsaved_changes_discards_them_when_the_user_says_discard()
    {
        Shell shell = Create(_directory.SaveReadableDocument());
        await shell.OpenDocument.Command.ExecuteAsync(null);
        shell.ViewModel.Session.Execute(
            new AddNodeCommand(new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId), 1, new CanvasPosition(0, 0)));
        string saved = shell.ViewModel.Session.Path!;
        shell.Chooser.Answer(_directory.SaveReadableDocument("second.vwflow"));

        Task opening = shell.ViewModel.OpenCommand.ExecuteAsync(null);
        shell.Prompt.RefuseText.ShouldBe("Discard");
        shell.Prompt.RefuseCommand.Execute(null);
        await opening;

        // Discarding is a decision: the file keeps what it already held and the other
        // document opens, because the user said so.
        new DocumentLoader().Open(saved).Session!.Document.Nodes.ShouldBeEmpty();
        shell.ViewModel.DocumentTitle.ShouldBe("second.vwflow");
    }

    [Fact]
    public async Task Open_cancelled_at_the_question_leaves_the_document_in_place()
    {
        Shell shell = Create(_directory.SaveReadableDocument());
        await shell.OpenDocument.Command.ExecuteAsync(null);
        shell.ViewModel.Session.Execute(
            new AddNodeCommand(new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId), 1, new CanvasPosition(0, 0)));
        shell.Chooser.Answer(_directory.SaveReadableDocument("second.vwflow"));

        Task opening = shell.ViewModel.OpenCommand.ExecuteAsync(null);
        shell.Prompt.DismissCommand.Execute(null);
        await opening;

        // Cancelling decides nothing, so the document the user was editing stays
        // exactly as it was, changes included.
        shell.ViewModel.DocumentTitle.ShouldBe("workflow.vwflow");
        shell.ViewModel.Session.IsDirty.ShouldBeTrue();
        shell.ViewModel.DocumentCounts.ShouldBe("1 node, 0 connections");
        shell.Status.DocumentState.ShouldBe("Unsaved changes");
    }

    [Fact]
    public async Task Open_stops_when_the_save_the_user_asked_for_was_refused()
    {
        Shell shell = Create(_directory.SaveReadableDocument());
        await shell.OpenDocument.Command.ExecuteAsync(null);
        shell.ViewModel.Session.Execute(
            new AddNodeCommand(new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId), 1, new CanvasPosition(0, 0)));
        string saved = shell.ViewModel.Session.Path!;

        // The writer creates a missing folder, so an unusable destination is a
        // directory sitting where the document belongs.
        System.IO.File.Delete(saved);
        System.IO.Directory.CreateDirectory(saved);

        shell.Chooser.Answer(_directory.SaveReadableDocument("second.vwflow"));
        Task opening = shell.ViewModel.OpenCommand.ExecuteAsync(null);
        shell.Prompt.AcceptCommand.Execute(null);
        await opening;

        // The work could not be written, so continuing would have replaced it with
        // the other document: the flow stops and says why instead.
        shell.ViewModel.DocumentTitle.ShouldBe("workflow.vwflow");
        shell.ViewModel.Session.Path.ShouldBe(saved);
        shell.Status.Condition.ShouldContain(DiagnosticCodes.UnreadableDocument);
        shell.Prompt.IsOpen.ShouldBeFalse();
    }

    [Fact]
    public async Task Open_resumes_the_working_copy_when_the_user_asks_for_it()
    {
        string path = _directory.SaveReadableDocument();
        WorkflowDocument workingCopy = WorkflowDocument.Create("Recovered workflow");
        workingCopy.AddNode(new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId), 1, new CanvasPosition(0, 0));
        WorkflowDocumentWriter.SaveWorkingCopy(workingCopy, path);

        Shell shell = Create(path);
        shell.Chooser.Answer(path);

        Task opening = shell.ViewModel.OpenCommand.ExecuteAsync(null);
        shell.Prompt.IsOpen.ShouldBeTrue();
        shell.Prompt.AcceptText.ShouldBe("Resume the working copy");
        shell.Prompt.Question.ShouldContain("working copy");
        shell.Prompt.AcceptCommand.Execute(null);
        await opening;

        // Resuming takes the work the file does not hold, and starts it unsaved,
        // because its content is not yet the content of its file.
        shell.ViewModel.DocumentCounts.ShouldBe("1 node, 0 connections");
        shell.ViewModel.Session.Path.ShouldBe(System.IO.Path.GetFullPath(path));
        shell.Status.DocumentState.ShouldBe("Unsaved changes");
    }

    [Fact]
    public async Task Open_of_a_document_with_a_working_copy_opens_the_saved_file_when_recovery_is_refused()
    {
        string path = _directory.SaveReadableDocument();
        WorkflowDocument workingCopy = WorkflowDocument.Create("Recovered workflow");
        workingCopy.AddNode(new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId), 1, new CanvasPosition(0, 0));
        WorkflowDocumentWriter.SaveWorkingCopy(workingCopy, path);

        Shell shell = Create(path);
        shell.Chooser.Answer(path);

        Task opening = shell.ViewModel.OpenCommand.ExecuteAsync(null);
        shell.Prompt.RefuseText.ShouldBe("Open the saved file");
        shell.Prompt.RefuseCommand.Execute(null);
        await opening;

        shell.ViewModel.DocumentCounts.ShouldBe("0 nodes, 0 connections");
        shell.Status.DocumentState.ShouldBe("Saved");
    }

    [Fact]
    public async Task Open_leaves_everything_alone_when_the_recovery_question_is_cancelled()
    {
        string path = _directory.SaveReadableDocument();
        WorkflowDocumentWriter.SaveWorkingCopy(WorkflowDocument.Create("Recovered workflow"), path);

        Shell shell = Create(path);
        shell.Chooser.Answer(path);

        Task opening = shell.ViewModel.OpenCommand.ExecuteAsync(null);
        shell.Prompt.DismissCommand.Execute(null);
        await opening;

        shell.ViewModel.DocumentTitle.ShouldBe(EditorSession.UntitledDocumentName);
        shell.ViewModel.Session.Path.ShouldBeNull();
    }

    [Fact]
    public async Task Save_with_a_document_that_has_never_been_saved_asks_for_a_destination()
    {
        Shell shell = Create(_directory.SaveReadableDocument());
        shell.ViewModel.Session.Execute(
            new AddNodeCommand(new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId), 1, new CanvasPosition(0, 0)));
        string chosen = _directory.PathOf("chosen.vwflow");
        shell.Chooser.AnswerSave(chosen);

        await shell.ViewModel.SaveCommand.ExecuteAsync(null);

        shell.Chooser.SuggestedNames.ShouldHaveSingleItem().ShouldBe(EditorSession.UntitledDocumentName);
        shell.ViewModel.Session.Path.ShouldBe(System.IO.Path.GetFullPath(chosen));
        shell.ViewModel.DocumentTitle.ShouldBe("chosen.vwflow");
        shell.Status.DocumentState.ShouldBe("Saved");
    }

    [Fact]
    public void Catalogue_search_matches_the_display_name_the_user_reads()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());

        shell.ViewModel.CatalogueSearch = "blur";

        shell.ViewModel.CatalogueGroups.Select(group => group.Category)
            .ShouldBe([OpenCvNodeIds.FilterCategory]);
        shell.ViewModel.CatalogueGroups[0].Entries.Select(entry => entry.DisplayName).ShouldBe(["Gaussian Blur"]);
        shell.ViewModel.NodeCatalogSummary.ShouldBe("1 of 4 node types match “blur”");
        shell.ViewModel.CatalogueNotice.ShouldBeEmpty();
    }

    [Fact]
    public void Catalogue_search_matches_the_identifier_a_document_stores()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());

        shell.ViewModel.CatalogueSearch = OpenCvNodeIds.ResizeTypeId;

        shell.ViewModel.CatalogueGroups[0].Entries.Select(entry => entry.TypeId)
            .ShouldBe([OpenCvNodeIds.ResizeTypeId]);
    }

    [Fact]
    public void Catalogue_search_that_matches_nothing_leaves_an_empty_catalogue_that_says_so()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());

        shell.ViewModel.CatalogueSearch = "  nothing-like-this  ";

        // Showing the whole catalog again would read as a search that was ignored,
        // so the region explains itself instead.
        shell.ViewModel.CatalogueGroups.ShouldBeEmpty();
        shell.ViewModel.CatalogueNotice.ShouldBe("No node type matches this search.");
        shell.ViewModel.NodeCatalogSummary.ShouldBe("0 of 4 node types match “nothing-like-this”");
    }

    [Fact]
    public void Catalogue_search_cleared_offers_every_type_again()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());
        shell.ViewModel.CatalogueSearch = "blur";

        shell.ViewModel.CatalogueSearch = string.Empty;

        shell.ViewModel.CatalogueGroups.Count.ShouldBe(3);
        shell.ViewModel.NodeCatalogSummary.ShouldBe("4 node types available");
        shell.ViewModel.CatalogueNotice.ShouldBeEmpty();
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
        var boundary = new AsyncCommandBoundary(_presenter, _logger);
        var openDocument = new OpenDocumentCommand(boundary, session, status)
        {
            Path = path,
        };
        StubFileChooser chooser = new();
        var saveDocument = new SaveDocumentCommand(boundary, session, status, chooser);
        ShellPromptViewModel prompt = new();

        return new Shell(
            new MainWindowViewModel(
                session,
                catalog,
                new WorkflowValidator(catalog),
                openDocument,
                saveDocument,
                ShellRun.CommandFor(session, catalog, status, boundary),
                new PreviewViewModel(session),
                chooser,
                prompt,
                status),
            openDocument,
            saveDocument,
            chooser,
            prompt,
            status);
    }

    /// <summary>
    /// The pieces one test drives, so a test never has to rebuild the wiring the
    /// shell composes at startup.
    /// </summary>
    /// <param name="ViewModel">The view model under test.</param>
    /// <param name="OpenDocument">The command the shell opens a named file with.</param>
    /// <param name="SaveDocument">The command the shell writes the document with.</param>
    /// <param name="Chooser">The dialog the view model asks for a file.</param>
    /// <param name="Prompt">The surface a question about the document is asked on.</param>
    /// <param name="Status">The durable state the status area reports.</param>
    private sealed record Shell(
        MainWindowViewModel ViewModel,
        OpenDocumentCommand OpenDocument,
        SaveDocumentCommand SaveDocument,
        StubFileChooser Chooser,
        ShellPromptViewModel Prompt,
        ShellStatus Status);
}
