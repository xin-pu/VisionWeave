using OpenCvSharp;
using Shouldly;
using VisionWeave.App.Preview;
using VisionWeave.App.Tests.Support;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.App.Tests.Preview;

/// <summary>
/// Covers what one run hands the shell: an image per node that published one, named
/// after the node that published it, held as a copy rather than as a claim on the
/// frame, and nothing at all for a node whose outputs hold no image.
/// </summary>
public sealed class RunPreviewObserverTests
{
    [Fact]
    public async Task Observing_a_published_frame_presents_a_copy_named_after_the_node_that_published_it()
    {
        var ledger = new LeaseLedger();
        var presenter = new RecordingPreviewPresenter();
        var observer = new RunPreviewObserver(FramePreviewConverter.Default, Catalog(), presenter);
        MatFrameLease frame = MatFrameLease.Create(new Mat(2, 4, MatType.CV_8UC3, Scalar.All(120)), ledger);

        try
        {
            await observer.ObserveAsync(Outputs(OpenCvNodeIds.ResizeTypeId, frame), CancellationToken.None);
        }
        finally
        {
            frame.Dispose();
        }

        RunPreview preview = presenter.Presented.ShouldHaveSingleItem();

        preview.NodeTitle.ShouldBe("Resize");
        preview.Width.ShouldBe(4);
        preview.Height.ShouldBe(2);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Bgr24);
        preview.Image.IsFrozen.ShouldBeTrue();

        // The preview is the shell's own copy: it is read back here after the frame it
        // was converted from has been released, which is what lets a run finish and
        // free everything it held while its last image is still on screen.
        var pixels = new byte[4 * 2 * 3];

        preview.Image.CopyPixels(pixels, 12, 0);
        pixels[0].ShouldBe((byte)120);

        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Observing_a_node_that_published_no_image_presents_nothing()
    {
        var presenter = new RecordingPreviewPresenter();
        var observer = new RunPreviewObserver(FramePreviewConverter.Default, Catalog(), presenter);

        await observer.ObserveAsync(Outputs(OpenCvNodeIds.SaveImageTypeId), CancellationToken.None);

        presenter.Presented.ShouldBeEmpty();
    }

    [Fact]
    public async Task Observing_while_the_run_is_stopping_still_presents_the_image_the_run_produced()
    {
        var ledger = new LeaseLedger();
        var presenter = new RecordingPreviewPresenter();
        var observer = new RunPreviewObserver(FramePreviewConverter.Default, Catalog(), presenter);
        MatFrameLease frame = MatFrameLease.Create(new Mat(2, 4, MatType.CV_8UC3, Scalar.All(120)), ledger);
        using var cancellation = new CancellationTokenSource();

        cancellation.Cancel();

        try
        {
            await observer.ObserveAsync(Outputs(OpenCvNodeIds.ImageSourceTypeId, frame), cancellation.Token);
        }
        finally
        {
            frame.Dispose();
        }

        // A stop explains the nodes it stopped and nothing else. The frame exists, was
        // paid for, and is the newest thing the user asked to see, so it is shown.
        presenter.Presented.ShouldHaveSingleItem().NodeTitle.ShouldBe("Image Source");
    }

    [Fact]
    public async Task Observing_a_frame_of_a_type_the_catalog_no_longer_holds_names_its_identifier()
    {
        var ledger = new LeaseLedger();
        var presenter = new RecordingPreviewPresenter();
        var observer = new RunPreviewObserver(FramePreviewConverter.Default, Catalog(), presenter);
        MatFrameLease frame = MatFrameLease.Create(new Mat(2, 4, MatType.CV_8UC3, Scalar.All(120)), ledger);

        try
        {
            await observer.ObserveAsync(Outputs("visionweave.test.retired", frame), CancellationToken.None);
        }
        finally
        {
            frame.Dispose();
        }

        // An identifier is a worse title than a name and a much better one than an
        // empty field, so a node the catalog no longer describes still says which.
        presenter.Presented.ShouldHaveSingleItem().NodeTitle.ShouldBe("visionweave.test.retired");
    }

    private static NodeDefinitionCatalog Catalog()
        => NodeDefinitionCatalog.FromProviders([new OpenCvNodeDefinitionProvider()]);

    private static NodeOutputs Outputs(string nodeTypeId, ImageFrameLease? frame = null)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new NodeTypeId(nodeTypeId),
            frame is null
                ? new Dictionary<string, PortValue>(StringComparer.Ordinal)
                : new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.ImagePortId] = new ImageFrameValue(frame),
                });
}
