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

        shell.ViewModel.NodeCatalogSummary.ShouldBe("33 node types available");
        shell.ViewModel.CatalogueGroups.Select(group => group.Category)
            .ShouldBe(
            [
                OpenCvNodeIds.ContoursCategory,
                OpenCvNodeIds.DrawCategory,
                OpenCvNodeIds.FilterCategory,
                OpenCvNodeIds.InputOutputCategory,
                OpenCvNodeIds.MorphologyCategory,
                OpenCvNodeIds.ThresholdCategory,
                OpenCvNodeIds.TransformCategory,
            ]);
        shell.ViewModel.CatalogueGroups[0].Entries.Select(entry => entry.DisplayName)
            .ShouldBe(["Find Contours"]);
        shell.ViewModel.CatalogueGroups[1].Entries.Select(entry => entry.DisplayName)
            .ShouldBe(["Draw Circle", "Draw Contours", "Draw Line", "Draw Rectangle"]);
        shell.ViewModel.CatalogueGroups[2].Entries.Select(entry => entry.DisplayName)
            .ShouldBe(
            [
                "Bilateral Filter",
                "Blur",
                "Canny",
                "Gaussian Blur",
                "Laplacian",
                "Median Blur",
                "Scharr",
                "Sharpen",
                "Sobel",
            ]);
        shell.ViewModel.CatalogueGroups[3].Entries.Select(entry => entry.DisplayName)
            .ShouldBe(["Image Source", "Save Image"]);
        shell.ViewModel.CatalogueGroups[4].Entries.Select(entry => entry.DisplayName)
            .ShouldBe(["Dilate", "Erode", "Morphology Ex"]);
        shell.ViewModel.CatalogueGroups[5].Entries.Select(entry => entry.DisplayName)
            .ShouldBe(["Adaptive Threshold", "In Range", "Threshold"]);
        shell.ViewModel.CatalogueGroups[6].Entries.Select(entry => entry.DisplayName)
            .ShouldBe(
            [
                "Bitwise Not",
                "Brightness / Contrast",
                "Colour Conversion",
                "Crop",
                "Equalize Histogram",
                "Flip",
                "Normalize",
                "Pyramid Down",
                "Pyramid Up",
                "Resize",
                "Rotate",
            ]);
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
        shell.ViewModel.CatalogueGroups[0].Entries.Select(entry => entry.DisplayName)
            .ShouldBe(["Blur", "Gaussian Blur", "Median Blur"]);
        shell.ViewModel.NodeCatalogSummary.ShouldBe("3 of 33 node types match “blur”");
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
        shell.ViewModel.NodeCatalogSummary.ShouldBe("0 of 33 node types match “nothing-like-this”");
    }

    [Fact]
    public void Catalogue_search_cleared_offers_every_type_again()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());
        shell.ViewModel.CatalogueSearch = "blur";

        shell.ViewModel.CatalogueSearch = string.Empty;

        shell.ViewModel.CatalogueGroups.Count.ShouldBe(7);
        shell.ViewModel.NodeCatalogSummary.ShouldBe("33 node types available");
        shell.ViewModel.CatalogueNotice.ShouldBeEmpty();
    }

    [Fact]
    public void The_tag_row_offers_the_words_the_catalog_carries_behind_the_chip_that_clears_them()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());

        // The row is the vocabulary the definitions use rather than a list this shell
        // decides, so a provider that tags a node needs no change here.
        shell.ViewModel.CatalogueTags.Select(chip => chip.Label)
            .ShouldBe(["All", .. OpenCvNodeTags.All]);
        shell.ViewModel.CatalogueTags[0].ShouldBe(new ShellCatalogueTag("All", null, IsSelected: true));
        shell.ViewModel.CatalogueTags.Count(chip => chip.IsSelected).ShouldBe(1);
    }

    [Fact]
    public void A_catalog_whose_definitions_carry_no_tags_offers_no_filter_row()
    {
        Shell shell = Create(_directory.SaveReadableDocument());

        // A chip the catalog cannot answer would be a filter that only ever empties the
        // region, so the region offers none at all.
        shell.ViewModel.CatalogueTags.ShouldBeEmpty();
    }

    [Fact]
    public void A_tag_narrows_the_catalogue_to_the_types_that_carry_it()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());

        shell.ViewModel.ToggleTagCommand.Execute(OpenCvNodeTags.Geometry);

        shell.ViewModel.CatalogueGroups.Select(group => group.Category)
            .ShouldBe([OpenCvNodeIds.TransformCategory]);
        shell.ViewModel.CatalogueGroups[0].Entries.Select(entry => entry.DisplayName)
            .ShouldBe(["Crop", "Flip", "Pyramid Down", "Pyramid Up", "Resize", "Rotate"]);
        shell.ViewModel.NodeCatalogSummary.ShouldBe("6 of 33 node types carry the “geometry” tag");
        shell.ViewModel.CatalogueNotice.ShouldBeEmpty();
    }

    [Fact]
    public void A_tag_that_cuts_across_two_categories_offers_both_of_them()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());

        // The contour pair sits in two categories and carries one word, which is what a
        // tag is for: it offers the two nodes that work on a contour set together,
        // wherever the library happens to file them.
        shell.ViewModel.ToggleTagCommand.Execute(OpenCvNodeTags.Contour);

        shell.ViewModel.CatalogueGroups.Select(group => group.Category)
            .ShouldBe([OpenCvNodeIds.ContoursCategory, OpenCvNodeIds.DrawCategory]);
        shell.ViewModel.CatalogueGroups[0].Entries.Select(entry => entry.DisplayName)
            .ShouldBe(["Find Contours"]);
        shell.ViewModel.CatalogueGroups[1].Entries.Select(entry => entry.DisplayName)
            .ShouldBe(["Draw Contours"]);
        shell.ViewModel.NodeCatalogSummary.ShouldBe("2 of 33 node types carry the “contour” tag");
    }

    [Fact]
    public void Two_tags_narrow_together_rather_than_widening_the_list()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());

        // They intersect rather than union: adding a word to a filter is a user asking
        // for less, and the eight types that carry either word are not what is offered.
        shell.ViewModel.ToggleTagCommand.Execute(OpenCvNodeTags.Mask);
        shell.ViewModel.ToggleTagCommand.Execute(OpenCvNodeTags.Threshold);

        shell.ViewModel.CatalogueGroups.ShouldHaveSingleItem().Entries.Select(entry => entry.DisplayName)
            .ShouldBe(["Adaptive Threshold", "In Range", "Threshold"]);
        shell.ViewModel.NodeCatalogSummary.ShouldBe("3 of 33 node types carry the “mask” and “threshold” tags");
    }

    [Fact]
    public void A_tag_and_a_search_term_narrow_together()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());

        shell.ViewModel.ToggleTagCommand.Execute(OpenCvNodeTags.Mask);
        shell.ViewModel.CatalogueSearch = "canny";

        shell.ViewModel.CatalogueGroups.ShouldHaveSingleItem().Entries.Select(entry => entry.DisplayName)
            .ShouldBe(["Canny"]);
        shell.ViewModel.NodeCatalogSummary.ShouldBe("1 of 33 node types carry the “mask” tag and match “canny”");
    }

    [Fact]
    public void A_tag_and_a_search_that_together_match_nothing_say_so()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());

        shell.ViewModel.ToggleTagCommand.Execute(OpenCvNodeTags.File);
        shell.ViewModel.CatalogueSearch = "blur";

        shell.ViewModel.CatalogueGroups.ShouldBeEmpty();
        shell.ViewModel.CatalogueNotice.ShouldBe("No node type carries the “file” tag and matches this search.");
        shell.ViewModel.NodeCatalogSummary.ShouldBe("0 of 33 node types carry the “file” tag and match “blur”");
    }

    [Fact]
    public void Choosing_the_tag_that_is_already_in_force_stops_narrowing_by_it()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());

        shell.ViewModel.ToggleTagCommand.Execute(OpenCvNodeTags.Edges);
        shell.ViewModel.ToggleTagCommand.Execute(OpenCvNodeTags.Edges);

        shell.ViewModel.CatalogueGroups.Count.ShouldBe(7);
        shell.ViewModel.NodeCatalogSummary.ShouldBe("33 node types available");
        shell.ViewModel.CatalogueTags[0].IsSelected.ShouldBeTrue();
    }

    [Fact]
    public void The_chip_that_clears_the_filter_offers_every_type_again()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());
        shell.ViewModel.ToggleTagCommand.Execute(OpenCvNodeTags.File);

        shell.ViewModel.ToggleTagCommand.Execute(null);

        shell.ViewModel.CatalogueGroups.Count.ShouldBe(7);
        shell.ViewModel.CatalogueTags[0].IsSelected.ShouldBeTrue();
        shell.ViewModel.CatalogueTags.ShouldAllBe(chip => chip.IsSelected == (chip.Tag == null));
        shell.ViewModel.CatalogueNotice.ShouldBeEmpty();
    }

    [Fact]
    public void A_catalogue_entry_shows_the_words_it_carries()
    {
        Shell shell = Create(_directory.SaveReadableDocument(), OpenCvCatalog());

        shell.ViewModel.CatalogueGroups
            .SelectMany(group => group.Entries)
            .Single(entry => entry.DisplayName == "Threshold")
            .TagLine.ShouldBe($"{OpenCvNodeTags.Threshold}, {OpenCvNodeTags.Mask}");
    }

    [Fact]
    public void A_word_the_catalog_carries_but_the_provider_does_not_declare_still_filters()
    {
        // The row is built from the definitions themselves, so a plugin that arrives
        // with a word of its own is filterable without this shell changing. Keeping the
        // words consistent is the provider's contract, checked where the provider is.
        Shell shell = Create(
            _directory.SaveReadableDocument(),
            Catalogue(Definition("visionweave.test.figure", "Figure", "figure")));

        shell.ViewModel.CatalogueTags.Select(chip => chip.Label).ShouldBe(["All", "figure"]);

        shell.ViewModel.ToggleTagCommand.Execute("figure");

        shell.ViewModel.CatalogueGroups.ShouldHaveSingleItem().Entries.Select(entry => entry.DisplayName)
            .ShouldBe(["Figure"]);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    private static NodeDefinitionCatalog OpenCvCatalog()
        => NodeDefinitionCatalog.FromProviders([new OpenCvNodeDefinitionProvider()]);

    private static NodeDefinitionCatalog Catalogue(params NodeDefinition[] definitions) => new(definitions);

    /// <summary>
    /// One definition that declares nothing but its identity and its tags, so a test
    /// can present a catalog this build does not ship.
    /// </summary>
    private static NodeDefinition Definition(string typeId, string displayName, params string[] tags)
        => new(
            new NodeTypeId(typeId),
            TypeVersion: 1,
            displayName,
            Category: "Test",
            Ports: [],
            Parameters: [],
            ExecutorTypeId: "visionweave.test.executor")
        {
            Tags = tags,
        };

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
