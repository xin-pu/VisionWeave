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
/// Covers what one run hands the shell: the images a node published, named after the node
/// and the output they arrived on, held as copies rather than as claims on the frames, and
/// nothing at all for a node whose outputs hold no image.
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
            await observer.ObserveAsync(
                Outputs(OpenCvNodeIds.ResizeTypeId, (OpenCvNodeIds.ResizedPortId, frame)),
                CancellationToken.None);
        }
        finally
        {
            frame.Dispose();
        }

        RunPreview preview = presenter.Presented.ShouldHaveSingleItem();

        preview.NodeTitle.ShouldBe("Resize");
        preview.PortId.ShouldBe(OpenCvNodeIds.ResizedPortId);
        preview.PortTitle.ShouldBe("Image");
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
    public async Task Observing_a_node_that_published_several_images_presents_them_together_in_declared_order()
    {
        var ledger = new LeaseLedger();
        var presenter = new RecordingPreviewPresenter();
        var observer = new RunPreviewObserver(FramePreviewConverter.Default, Catalog(), presenter);

        // The node publishes its three channels as one set, and the dictionary it hands
        // over enumerates the ports in an order of its own, so what tells the images
        // apart is the port each arrived on rather than the place it happened to take.
        MatFrameLease blue = MatFrameLease.Create(new Mat(2, 4, MatType.CV_8UC1, Scalar.All(10)), ledger);
        MatFrameLease green = MatFrameLease.Create(new Mat(2, 4, MatType.CV_8UC1, Scalar.All(20)), ledger);
        MatFrameLease red = MatFrameLease.Create(new Mat(2, 4, MatType.CV_8UC1, Scalar.All(30)), ledger);

        try
        {
            await observer.ObserveAsync(
                Outputs(
                    OpenCvNodeIds.SplitChannelsTypeId,
                    (OpenCvNodeIds.RedChannelPortId, red),
                    (OpenCvNodeIds.BlueChannelPortId, blue),
                    (OpenCvNodeIds.GreenChannelPortId, green)),
                CancellationToken.None);
        }
        finally
        {
            blue.Dispose();
            green.Dispose();
            red.Dispose();
        }

        // One node, one hand-off: the shell draws a node's images as the set the node
        // published rather than as whichever one arrived last.
        IReadOnlyList<RunPreview> previews = presenter.Sets.ShouldHaveSingleItem();

        previews.Count.ShouldBe(3);
        previews.Select(preview => preview.NodeTitle).ShouldAllBe(title => title == "Split Channels");
        previews.Select(preview => preview.PortId).ShouldBe(
            [OpenCvNodeIds.BlueChannelPortId, OpenCvNodeIds.GreenChannelPortId, OpenCvNodeIds.RedChannelPortId]);
        previews.Select(preview => preview.PortTitle).ShouldBe(["Blue", "Green", "Red"]);
        previews.ShouldAllBe(preview => preview.Image.IsFrozen);

        // The images keep the pixels of their own channel, so which tile shows which
        // channel is the node's declaration rather than a coincidence of enumeration.
        previews.Select(preview => BitmapPixels.First(preview.Image)).ShouldBe<byte>([10, 20, 30]);

        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Observing_passes_over_a_frame_this_build_cannot_convert_and_shows_the_rest()
    {
        var ledger = new LeaseLedger();
        var presenter = new RecordingPreviewPresenter();
        var observer = new RunPreviewObserver(FramePreviewConverter.Default, Catalog(), presenter);
        MatFrameLease blue = MatFrameLease.Create(new Mat(2, 4, MatType.CV_8UC1, Scalar.All(10)), ledger);
        MatFrameLease green = MatFrameLease.Create(new Mat(2, 4, MatType.CV_8UC1, Scalar.All(20)), ledger);

        // A depth a preview cannot be made of: the node succeeded, and the shell shows
        // one image fewer rather than refusing the node's whole set.
        MatFrameLease float32 = MatFrameLease.Create(new Mat(2, 4, MatType.CV_32FC1, Scalar.All(1)), ledger);

        try
        {
            await observer.ObserveAsync(
                Outputs(
                    OpenCvNodeIds.SplitChannelsTypeId,
                    (OpenCvNodeIds.BlueChannelPortId, blue),
                    (OpenCvNodeIds.GreenChannelPortId, green),
                    (OpenCvNodeIds.RedChannelPortId, float32)),
                CancellationToken.None);
        }
        finally
        {
            blue.Dispose();
            green.Dispose();
            float32.Dispose();
        }

        IReadOnlyList<RunPreview> previews = presenter.Sets.ShouldHaveSingleItem();

        previews.Select(preview => preview.PortTitle).ShouldBe(["Blue", "Green"]);
    }

    [Fact]
    public async Task Observing_a_node_that_published_no_image_presents_nothing()
    {
        var presenter = new RecordingPreviewPresenter();
        var observer = new RunPreviewObserver(FramePreviewConverter.Default, Catalog(), presenter);

        await observer.ObserveAsync(Outputs(OpenCvNodeIds.SaveImageTypeId), CancellationToken.None);

        presenter.Presented.ShouldBeEmpty();
        presenter.Sets.ShouldBeEmpty();
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
            await observer.ObserveAsync(
                Outputs(OpenCvNodeIds.ImageSourceTypeId, (OpenCvNodeIds.ImagePortId, frame)),
                cancellation.Token);
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
            await observer.ObserveAsync(
                Outputs("visionweave.test.retired", ("published", frame)),
                CancellationToken.None);
        }
        finally
        {
            frame.Dispose();
        }

        // An identifier is a worse title than a name and a much better one than an
        // empty field, so a node the catalog no longer describes still says which, and
        // so does the port the image arrived on.
        RunPreview preview = presenter.Presented.ShouldHaveSingleItem();

        preview.NodeTitle.ShouldBe("visionweave.test.retired");
        preview.PortTitle.ShouldBe("published");
    }

    private static NodeDefinitionCatalog Catalog()
        => NodeDefinitionCatalog.FromProviders([new OpenCvNodeDefinitionProvider()]);

    private static NodeOutputs Outputs(string nodeTypeId, params (string PortId, ImageFrameLease Frame)[] published)
    {
        Dictionary<string, PortValue> values = new(StringComparer.Ordinal);

        foreach ((string portId, ImageFrameLease frame) in published)
        {
            values[portId] = new ImageFrameValue(frame);
        }

        return new NodeOutputs(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new NodeTypeId(nodeTypeId),
            values);
    }
}
