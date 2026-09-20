using Shouldly;
using VisionWeave.App.Preview;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.Domain.Workflows;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.App.Tests.Preview;

/// <summary>
/// Covers what the preview region draws: the newest image a run published, the node
/// it came from, and what the region says while there is nothing to draw. The
/// hand-off to the thread the window's bindings belong to is covered by the shell's
/// smoke test, which is where a dispatcher exists.
/// </summary>
public sealed class PreviewViewModelTests
{
    [Fact]
    public void A_new_preview_says_that_nothing_has_run_yet()
    {
        PreviewViewModel preview = new(TestSessions.Create());

        preview.HasPreview.ShouldBeFalse();
        preview.Image.ShouldBeNull();
        preview.PreviewTitle.ShouldBeEmpty();
        preview.PreviewDetail.ShouldBeEmpty();
        preview.PreviewNotice.ShouldBe(PreviewViewModel.NoPreviewText);
    }

    [Fact]
    public void Showing_a_preview_names_the_node_and_the_size_the_image_arrived_with()
    {
        PreviewViewModel preview = new(TestSessions.Create());
        List<string?> raised = [];
        preview.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        preview.Show(Frame("Resize", width: 16, height: 8));

        preview.HasPreview.ShouldBeTrue();
        preview.PreviewTitle.ShouldBe("Resize");
        preview.PreviewDetail.ShouldBe("16 × 8 pixels, Bgr24");
        preview.Image!.PixelWidth.ShouldBe(16);
        preview.PreviewNotice.ShouldBeEmpty();

        // The region reads the notice for its visibility, so a state change reaches
        // the field the decision is drawn from rather than only the values behind it.
        raised.ShouldContain(nameof(PreviewViewModel.HasPreview));
        raised.ShouldContain(nameof(PreviewViewModel.PreviewNotice));
    }

    [Fact]
    public void A_shown_preview_is_frozen_so_the_shell_can_draw_what_another_thread_converted()
    {
        PreviewViewModel preview = new(TestSessions.Create());

        preview.Show(Frame("Resize", width: 4, height: 2));

        // A run converts frames on its own thread and the window draws on another, so
        // the bitmap has to belong to no thread at all by the time it arrives.
        preview.Image!.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    public void Replacing_the_document_forgets_the_preview_of_the_document_that_was_open()
    {
        EditorSession session = TestSessions.Create();
        PreviewViewModel preview = new(session);
        preview.Show(Frame("Resize", width: 16, height: 8));

        session.New("another workflow");

        // The measure of the image on screen is what the run read from the document
        // that is no longer open, so it goes with that document instead of staying as
        // a picture of something the shell is not showing.
        preview.HasPreview.ShouldBeFalse();
        preview.Image.ShouldBeNull();
        preview.PreviewTitle.ShouldBeEmpty();
        preview.PreviewNotice.ShouldBe(PreviewViewModel.NoPreviewText);
    }

    [Fact]
    public void Selecting_a_node_shows_that_nodes_cached_output()
    {
        EditorSession session = TestSessions.Create();
        Guid first = session.Document.AddNode(
            new NodeTypeId("visionweave.test.first"), 1, new CanvasPosition(0, 0)).InstanceId;
        Guid second = session.Document.AddNode(
            new NodeTypeId("visionweave.test.second"), 1, new CanvasPosition(100, 0)).InstanceId;
        PreviewViewModel preview = new(session);
        Guid operation = Guid.NewGuid();

        preview.Show(Frame("First", 16, 8, operation, first));
        preview.Show(Frame("Second", 8, 4, operation, second));
        session.Select([first]);

        preview.PreviewTitle.ShouldBe("First");
        preview.PreviewDetail.ShouldStartWith("16 × 8");
    }

    private static RunPreview Frame(
        string title,
        int width,
        int height,
        Guid? operationId = null,
        Guid? nodeInstanceId = null)
        => new(
            RunPreviewImage.Create(new PreviewFrame(
                width,
                height,
                FramePixelFormat.Bgr24,
                width * 3,
                new byte[width * height * 3])),
            operationId ?? Guid.NewGuid(),
            nodeInstanceId ?? Guid.NewGuid(),
            title,
            width,
            height,
            FramePixelFormat.Bgr24);
}
