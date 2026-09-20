using OpenCvSharp;
using Shouldly;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.Domain.Workflows;
using VisionWeave.IntegrationTests.Support;
using VisionWeave.OpenCv.Nodes;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.IntegrationTests.OpenCv;

/// <summary>
/// The first workflow a user can actually run: a stored document that reads a real
/// image from the folder it lives in, transforms it, and writes the result. Every
/// test ends by asserting that the ledger is flat, including the ones where the
/// run failed or was stopped early, which is the invariant the detailed design
/// requires of every scenario.
/// </summary>
public sealed class FileBackedWorkflowTests
{
    [Fact]
    public async Task Run_a_saved_workflow_reads_transforms_writes_and_releases_every_lease()
    {
        using var directory = new TemporaryDirectory();
        WritePlate(directory.File("plate.png"), width: 32, height: 24, value: 120);
        (WorkflowDocument document, _, _, _) = BuildWorkflow();
        string workflowPath = Save(document, directory);

        WorkflowLoadResult loaded = WorkflowDocumentReader.Load(workflowPath);
        loaded.Succeeded.ShouldBeTrue();
        loaded.IsReadOnly.ShouldBeFalse();
        loaded.Diagnostics.ShouldBeEmpty();

        LeaseLedger ledger = new();
        var observer = new ConvertingOutputObserver();

        WorkflowRunSummary summary = await RunAsync(loaded.Document!, ledger, directory, observer);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Nodes.ShouldAllBe(node => node.State == NodeRunState.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();

        // What the run wrote is the image the document names, at the size the
        // transform asked for, holding the pixels the source read.
        using Mat written = Cv2.ImRead(directory.File("done.png"), ImreadModes.Color);
        written.Cols.ShouldBe(16);
        written.Rows.ShouldBe(8);
        written.At<Vec3b>(0, 0).Item0.ShouldBe((byte)120);

        // Both images the run published reached a preview, and both were still
        // alive when they did: the conversion fence holds until the observer is
        // done rather than relying on the frame surviving by accident.
        observer.Previews.Count.ShouldBe(2);
        observer.Previews[^1].Width.ShouldBe(16);
        observer.Previews[^1].Height.ShouldBe(8);
        observer.FramesAliveAtConversion.ShouldAllBe(alive => alive);
        observer.RejectedFormats.ShouldBe(0);

        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_of_the_migrated_single_image_nodes_writes_what_they_produced_and_releases_every_lease()
    {
        using var directory = new TemporaryDirectory();
        WritePlate(directory.File("plate.png"), width: 32, height: 24, value: 120);
        WorkflowDocument document = BuildMigratedWorkflow();
        _ = Save(document, directory);

        LeaseLedger ledger = new();

        WorkflowRunSummary summary = await RunAsync(document, ledger, directory);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Nodes.ShouldAllBe(node => node.State == NodeRunState.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();

        // The plate holds one value, so the whole chain is decidable by hand: the
        // crop keeps the 16 by 12 corner it names, the conversion to greyscale
        // keeps that value on one channel, and the threshold turns it into white
        // because 120 passes a threshold of 100.
        using Mat written = Cv2.ImRead(directory.File("mask.png"), ImreadModes.Grayscale);
        written.Cols.ShouldBe(16);
        written.Rows.ShouldBe(12);
        written.At<byte>(0, 0).ShouldBe((byte)255);

        // One frame per node that produces one: the source's read, the crop's copy,
        // the converted frame, and the thresholded one.
        ledger.Created.ShouldBe(4);
        ledger.Released.ShouldBe(4);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_cancelled_while_it_runs_writes_no_output_and_releases_every_lease()
    {
        using var directory = new TemporaryDirectory();
        WritePlate(directory.File("plate.png"), width: 32, height: 24, value: 120);
        (WorkflowDocument document, _, _, NodeInstance save) = BuildWorkflow();
        _ = Save(document, directory);

        LeaseLedger ledger = new();
        using var cancellation = new CancellationTokenSource();

        // The run is stopped from inside the first preview, which is after the
        // source published its frame and before the nodes downstream of it start.
        var observer = new ConvertingOutputObserver(onBeforeConvert: cancellation.Cancel);

        WorkflowRunSummary summary = await RunAsync(document, ledger, directory, observer, cancellation.Token);

        summary.WasCancelled.ShouldBeTrue();
        summary.Status.ShouldBe(WorkflowRunStatus.Cancelled);
        summary.StateOf(save.InstanceId).ShouldBe(NodeRunState.NotRun);
        File.Exists(directory.File("done.png")).ShouldBeFalse();

        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_whose_source_cannot_read_its_file_blocks_the_save_and_writes_nothing()
    {
        using var directory = new TemporaryDirectory();
        (WorkflowDocument document, _, _, NodeInstance save) = BuildWorkflow(inputName: "missing.png");
        _ = Save(document, directory);

        LeaseLedger ledger = new();

        WorkflowRunSummary summary = await RunAsync(document, ledger, directory);

        summary.Status.ShouldBe(WorkflowRunStatus.Failed);
        summary.HasCode(DiagnosticCodes.NodeExecutionFailed).ShouldBeTrue();
        summary.StateOf(save.InstanceId).ShouldBe(NodeRunState.Blocked);
        summary.Diagnostics.ShouldContain(diagnostic => diagnostic.Message.Contains("missing.png", StringComparison.Ordinal));
        File.Exists(directory.File("done.png")).ShouldBeFalse();

        ledger.Created.ShouldBe(0);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_of_the_filter_nodes_writes_what_they_produced_and_releases_every_lease()
    {
        using var directory = new TemporaryDirectory();
        WritePlate(directory.File("plate.png"), width: 32, height: 24, value: 120);
        WorkflowDocument document = BuildFilterWorkflow();
        _ = Save(document, directory);

        LeaseLedger ledger = new();
        var observer = new ConvertingOutputObserver();

        WorkflowRunSummary summary = await RunAsync(document, ledger, directory, observer);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();

        // The plate holds one value, so each step is decidable by hand: a box blur
        // of a flat frame is that frame, and halving it keeps every pixel. What the
        // run wrote is therefore the plate's value at half its size.
        using Mat written = Cv2.ImRead(directory.File("smoothed.png"), ImreadModes.Grayscale);
        written.Cols.ShouldBe(16);
        written.Rows.ShouldBe(12);
        written.At<byte>(0, 0).ShouldBe((byte)120);

        observer.Previews.Count.ShouldBe(3);
        observer.Previews[^1].PixelFormat.ShouldBe(FramePixelFormat.Bgr24);

        ledger.Created.ShouldBe(3);
        ledger.Released.ShouldBe(3);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_of_the_derivative_nodes_writes_what_they_produced_and_releases_every_lease()
    {
        using var directory = new TemporaryDirectory();
        WriteSplitPlate(directory.File("plate.png"), width: 32, height: 24, dark: 40, bright: 200);
        WorkflowDocument document = BuildEdgeWorkflow();
        _ = Save(document, directory);

        LeaseLedger ledger = new();
        var observer = new ConvertingOutputObserver();

        WorkflowRunSummary summary = await RunAsync(document, ledger, directory, observer);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();

        // The plate is dark on one side and bright on the other, and the node runs
        // with the thresholds and the aperture a placed node starts with rather than
        // with values this test chose, so the line the run writes is the one the
        // definition's defaults produce: a single row of white down the split, and
        // nothing anywhere the frame is flat.
        using Mat written = Cv2.ImRead(directory.File("edges.png"), ImreadModes.Grayscale);
        written.Cols.ShouldBe(32);
        written.Rows.ShouldBe(24);
        Cv2.CountNonZero(written).ShouldBe(24);
        written.At<byte>(0, 15).ShouldBe((byte)255);
        written.At<byte>(0, 20).ShouldBe((byte)0);

        observer.Previews.Count.ShouldBe(2);
        observer.Previews[^1].PixelFormat.ShouldBe(FramePixelFormat.Gray8);

        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_of_the_morphology_nodes_writes_what_they_produced_and_releases_every_lease()
    {
        using var directory = new TemporaryDirectory();
        WriteSplitPlate(directory.File("plate.png"), width: 32, height: 24, dark: 40, bright: 200);
        WorkflowDocument document = BuildMorphologyWorkflow();
        _ = Save(document, directory);

        LeaseLedger ledger = new();
        var observer = new ConvertingOutputObserver();

        WorkflowRunSummary summary = await RunAsync(document, ledger, directory, observer);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();

        // The node runs with the element a placed node starts with — a three by three
        // square, applied once — so the plate gives up one column at the split: column
        // sixteen is the first column of the bright half, and the darkest pixel of its
        // element is the dark value beside it, which is therefore what it is written
        // as. Every column further right has a whole bright element around it and keeps
        // the bright value, including the last one, whose border repeats the frame.
        using Mat written = Cv2.ImRead(directory.File("thinned.png"), ImreadModes.Grayscale);
        written.Cols.ShouldBe(32);
        written.Rows.ShouldBe(24);
        written.At<byte>(0, 15).ShouldBe((byte)40);
        written.At<byte>(0, 16).ShouldBe((byte)40);
        written.At<byte>(0, 17).ShouldBe((byte)200);
        written.At<byte>(23, 31).ShouldBe((byte)200);

        observer.Previews.Count.ShouldBe(2);
        observer.Previews[^1].PixelFormat.ShouldBe(FramePixelFormat.Bgr24);

        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_of_the_draw_nodes_writes_what_they_produced_and_releases_every_lease()
    {
        using var directory = new TemporaryDirectory();
        WritePlate(directory.File("plate.png"), width: 32, height: 24, value: 40);
        WorkflowDocument document = BuildDrawWorkflow();
        string workflowPath = Save(document, directory);

        WorkflowLoadResult loaded = WorkflowDocumentReader.Load(workflowPath);
        loaded.Succeeded.ShouldBeTrue();
        loaded.IsReadOnly.ShouldBeFalse();

        LeaseLedger ledger = new();
        var observer = new ConvertingOutputObserver();

        WorkflowRunSummary summary = await RunAsync(loaded.Document!, ledger, directory, observer);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();

        // The rectangle the document states is outlined where it is, in the white a
        // placed node starts with, and everything the outline does not cover keeps the
        // value the plate was read with.
        using Mat written = Cv2.ImRead(directory.File("marked.png"), ImreadModes.Color);
        written.Cols.ShouldBe(32);
        written.Rows.ShouldBe(24);

        Vec3b corner = written.At<Vec3b>(6, 8);
        corner.Item0.ShouldBe((byte)255);
        corner.Item1.ShouldBe((byte)255);
        corner.Item2.ShouldBe((byte)255);
        written.At<Vec3b>(6, 23).Item0.ShouldBe((byte)255);
        written.At<Vec3b>(17, 8).Item0.ShouldBe((byte)255);
        written.At<Vec3b>(17, 23).Item0.ShouldBe((byte)255);
        written.At<Vec3b>(7, 8).Item0.ShouldBe((byte)255);
        written.At<Vec3b>(7, 9).Item0.ShouldBe((byte)40);
        written.At<Vec3b>(0, 0).Item0.ShouldBe((byte)40);

        observer.Previews.Count.ShouldBe(2);
        observer.Previews[^1].PixelFormat.ShouldBe(FramePixelFormat.Bgr24);

        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_of_the_contour_nodes_writes_what_they_produced_and_releases_every_lease()
    {
        using var directory = new TemporaryDirectory();
        WriteShapePlate(directory.File("plate.png"), width: 32, height: 24, field: 40, shape: 200);
        WorkflowDocument document = BuildContourWorkflow();
        string workflowPath = Save(document, directory);

        WorkflowLoadResult loaded = WorkflowDocumentReader.Load(workflowPath);
        loaded.Succeeded.ShouldBeTrue();
        loaded.IsReadOnly.ShouldBeFalse();

        LeaseLedger ledger = new();
        var observer = new ConvertingOutputObserver();

        WorkflowRunSummary summary = await RunAsync(loaded.Document!, ledger, directory, observer);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();

        // The plate is dark with one bright rectangle in it, so the mask the run
        // thresholds out of it holds exactly that shape and the contour is its border.
        // Drawing it back on the plate marks the four sides of the rectangle in the
        // white a placed node starts with, and leaves everything else, inside included,
        // as the plate was read.
        using Mat written = Cv2.ImRead(directory.File("found.png"), ImreadModes.Color);
        written.Cols.ShouldBe(32);
        written.Rows.ShouldBe(24);

        written.At<Vec3b>(6, 8).Item0.ShouldBe((byte)255);
        written.At<Vec3b>(6, 23).Item0.ShouldBe((byte)255);
        written.At<Vec3b>(17, 8).Item0.ShouldBe((byte)255);
        written.At<Vec3b>(17, 23).Item0.ShouldBe((byte)255);
        written.At<Vec3b>(10, 15).Item0.ShouldBe((byte)200);
        written.At<Vec3b>(0, 0).Item0.ShouldBe((byte)40);

        // The four nodes that produce a frame each published one, and the node that
        // found the contours published a value instead: a contour set is not an image,
        // so there is nothing for a preview to show and nothing for it to reject.
        observer.Previews.Count.ShouldBe(4);
        observer.Previews[^1].PixelFormat.ShouldBe(FramePixelFormat.Bgr24);
        observer.RejectedFormats.ShouldBe(0);

        ledger.Created.ShouldBe(4);
        ledger.Released.ShouldBe(4);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_again_leaves_the_output_alone_until_the_document_allows_replacement()
    {
        using var directory = new TemporaryDirectory();
        WritePlate(directory.File("plate.png"), width: 32, height: 24, value: 120);
        (WorkflowDocument document, _, _, NodeInstance save) = BuildWorkflow();
        _ = Save(document, directory);

        LeaseLedger first = new();
        (await RunAsync(document, first, directory)).Status.ShouldBe(WorkflowRunStatus.Succeeded);

        LeaseLedger second = new();
        WorkflowRunSummary refused = await RunAsync(document, second, directory);

        refused.Status.ShouldBe(WorkflowRunStatus.Failed);
        refused.StateOf(save.InstanceId).ShouldBe(NodeRunState.Failed);
        second.Outstanding.ShouldBe(0);

        using (Mat kept = Cv2.ImRead(directory.File("done.png"), ImreadModes.Color))
        {
            kept.At<Vec3b>(0, 0).Item0.ShouldBe((byte)120);
        }

        document.SetNodeParameter(save.InstanceId, OpenCvNodeIds.OverwriteParameter, true);

        LeaseLedger third = new();
        (await RunAsync(document, third, directory)).Status.ShouldBe(WorkflowRunStatus.Succeeded);
        third.Outstanding.ShouldBe(0);
    }

    /// <summary>
    /// Builds the workflow of the migrated single-image nodes: read a plate, keep a
    /// corner of it, convert that corner to greyscale, threshold it, and write the
    /// mask. It is the chain the Aries migration added, run the way a user runs it.
    /// </summary>
    private static WorkflowDocument BuildMigratedWorkflow()
    {
        WorkflowDocument document = WorkflowDocument.Create("mask");
        NodeInstance source = document.AddNode(new NodeTypeId(OpenCvNodeIds.ImageSourceTypeId), 1, new CanvasPosition(0, 0));
        NodeInstance crop = document.AddNode(new NodeTypeId(OpenCvNodeIds.CropTypeId), 1, new CanvasPosition(200, 0));
        NodeInstance conversion = document.AddNode(new NodeTypeId(OpenCvNodeIds.CvtColorTypeId), 1, new CanvasPosition(400, 0));
        NodeInstance threshold = document.AddNode(new NodeTypeId(OpenCvNodeIds.ThresholdTypeId), 1, new CanvasPosition(600, 0));
        NodeInstance save = document.AddNode(new NodeTypeId(OpenCvNodeIds.SaveImageTypeId), 1, new CanvasPosition(800, 0));

        document.SetNodeParameter(source.InstanceId, OpenCvNodeIds.PathParameter, "plate.png");
        document.SetNodeParameter(crop.InstanceId, OpenCvNodeIds.XParameter, 0);
        document.SetNodeParameter(crop.InstanceId, OpenCvNodeIds.YParameter, 0);
        document.SetNodeParameter(crop.InstanceId, OpenCvNodeIds.WidthParameter, 16);
        document.SetNodeParameter(crop.InstanceId, OpenCvNodeIds.HeightParameter, 12);
        document.SetNodeParameter(conversion.InstanceId, OpenCvNodeIds.ConversionParameter, OpenCvNodeIds.ConversionGray);
        document.SetNodeParameter(threshold.InstanceId, OpenCvNodeIds.ThresholdParameter, 100d);
        document.SetNodeParameter(threshold.InstanceId, OpenCvNodeIds.MaxValueParameter, 255d);
        document.SetNodeParameter(save.InstanceId, OpenCvNodeIds.PathParameter, "mask.png");

        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, crop.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(crop.InstanceId, OpenCvNodeIds.CroppedPortId, conversion.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(conversion.InstanceId, OpenCvNodeIds.ConvertedPortId, threshold.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(threshold.InstanceId, OpenCvNodeIds.ThresholdedPortId, save.InstanceId, OpenCvNodeIds.ImagePortId);

        return document;
    }

    private static (WorkflowDocument Document, NodeInstance Source, NodeInstance Resize, NodeInstance Save) BuildWorkflow(
        string inputName = "plate.png",
        string outputName = "done.png")
    {
        WorkflowDocument document = WorkflowDocument.Create("plate");
        NodeInstance source = document.AddNode(new NodeTypeId(OpenCvNodeIds.ImageSourceTypeId), 1, new CanvasPosition(0, 0));
        NodeInstance resize = document.AddNode(new NodeTypeId(OpenCvNodeIds.ResizeTypeId), 1, new CanvasPosition(200, 0));
        NodeInstance save = document.AddNode(new NodeTypeId(OpenCvNodeIds.SaveImageTypeId), 1, new CanvasPosition(400, 0));

        document.SetNodeParameter(source.InstanceId, OpenCvNodeIds.PathParameter, inputName);
        document.SetNodeParameter(resize.InstanceId, OpenCvNodeIds.WidthParameter, 16);
        document.SetNodeParameter(resize.InstanceId, OpenCvNodeIds.HeightParameter, 8);
        document.SetNodeParameter(save.InstanceId, OpenCvNodeIds.PathParameter, outputName);

        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, resize.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(resize.InstanceId, OpenCvNodeIds.ResizedPortId, save.InstanceId, OpenCvNodeIds.ImagePortId);

        return (document, source, resize, save);
    }

    /// <summary>
    /// Builds the workflow of the migrated filtering nodes: read a plate, smooth it
    /// with a box blur, halve it with a pyramid, and write the result.
    /// </summary>
    private static WorkflowDocument BuildFilterWorkflow()
    {
        WorkflowDocument document = WorkflowDocument.Create("smoothed");
        NodeInstance source = document.AddNode(new NodeTypeId(OpenCvNodeIds.ImageSourceTypeId), 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(new NodeTypeId(OpenCvNodeIds.BlurTypeId), 1, new CanvasPosition(200, 0));
        NodeInstance pyramid = document.AddNode(new NodeTypeId(OpenCvNodeIds.PyrDownTypeId), 1, new CanvasPosition(400, 0));
        NodeInstance save = document.AddNode(new NodeTypeId(OpenCvNodeIds.SaveImageTypeId), 1, new CanvasPosition(600, 0));

        document.SetNodeParameter(source.InstanceId, OpenCvNodeIds.PathParameter, "plate.png");
        document.SetNodeParameter(blur.InstanceId, OpenCvNodeIds.KernelSizeParameter, 3);
        document.SetNodeParameter(save.InstanceId, OpenCvNodeIds.PathParameter, "smoothed.png");

        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, blur.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(blur.InstanceId, OpenCvNodeIds.BlurredPortId, pyramid.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(pyramid.InstanceId, OpenCvNodeIds.ReducedPortId, save.InstanceId, OpenCvNodeIds.ImagePortId);

        return document;
    }

    /// <summary>
    /// Builds the workflow of the migrated derivative nodes: read a plate, find the
    /// edges in it with the parameters a placed node starts with, and write the map.
    /// </summary>
    private static WorkflowDocument BuildEdgeWorkflow()
    {
        WorkflowDocument document = WorkflowDocument.Create("edges");
        NodeInstance source = document.AddNode(new NodeTypeId(OpenCvNodeIds.ImageSourceTypeId), 1, new CanvasPosition(0, 0));
        NodeInstance canny = document.AddNode(new NodeTypeId(OpenCvNodeIds.CannyTypeId), 1, new CanvasPosition(200, 0));
        NodeInstance save = document.AddNode(new NodeTypeId(OpenCvNodeIds.SaveImageTypeId), 1, new CanvasPosition(400, 0));

        document.SetNodeParameter(source.InstanceId, OpenCvNodeIds.PathParameter, "plate.png");
        document.SetNodeParameter(save.InstanceId, OpenCvNodeIds.PathParameter, "edges.png");

        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, canny.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(canny.InstanceId, OpenCvNodeIds.EdgesPortId, save.InstanceId, OpenCvNodeIds.ImagePortId);

        return document;
    }

    /// <summary>
    /// Builds the workflow of the migrated morphology nodes: read a plate, thin it by
    /// the structuring element a placed node starts with, and write the result.
    /// </summary>
    private static WorkflowDocument BuildMorphologyWorkflow()
    {
        WorkflowDocument document = WorkflowDocument.Create("thinned");
        NodeInstance source = document.AddNode(new NodeTypeId(OpenCvNodeIds.ImageSourceTypeId), 1, new CanvasPosition(0, 0));
        NodeInstance erode = document.AddNode(new NodeTypeId(OpenCvNodeIds.ErodeTypeId), 1, new CanvasPosition(200, 0));
        NodeInstance save = document.AddNode(new NodeTypeId(OpenCvNodeIds.SaveImageTypeId), 1, new CanvasPosition(400, 0));

        document.SetNodeParameter(source.InstanceId, OpenCvNodeIds.PathParameter, "plate.png");
        document.SetNodeParameter(save.InstanceId, OpenCvNodeIds.PathParameter, "thinned.png");

        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, erode.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(erode.InstanceId, OpenCvNodeIds.ErodedPortId, save.InstanceId, OpenCvNodeIds.ImagePortId);

        return document;
    }

    /// <summary>
    /// Builds the workflow of the migrated draw node: read a plate, outline a
    /// rectangle on it in the colour a placed node starts with, and write the result.
    /// </summary>
    private static WorkflowDocument BuildDrawWorkflow()
    {
        WorkflowDocument document = WorkflowDocument.Create("marked");
        NodeInstance source = document.AddNode(new NodeTypeId(OpenCvNodeIds.ImageSourceTypeId), 1, new CanvasPosition(0, 0));
        NodeInstance draw = document.AddNode(new NodeTypeId(OpenCvNodeIds.DrawRectangleTypeId), 1, new CanvasPosition(200, 0));
        NodeInstance save = document.AddNode(new NodeTypeId(OpenCvNodeIds.SaveImageTypeId), 1, new CanvasPosition(400, 0));

        document.SetNodeParameter(source.InstanceId, OpenCvNodeIds.PathParameter, "plate.png");
        document.SetNodeParameter(draw.InstanceId, OpenCvNodeIds.XParameter, 8);
        document.SetNodeParameter(draw.InstanceId, OpenCvNodeIds.YParameter, 6);
        document.SetNodeParameter(draw.InstanceId, OpenCvNodeIds.WidthParameter, 16);
        document.SetNodeParameter(draw.InstanceId, OpenCvNodeIds.HeightParameter, 12);
        document.SetNodeParameter(save.InstanceId, OpenCvNodeIds.PathParameter, "marked.png");

        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, draw.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(draw.InstanceId, OpenCvNodeIds.DrawnPortId, save.InstanceId, OpenCvNodeIds.ImagePortId);

        return document;
    }

    /// <summary>
    /// Builds the workflow of the contour pair: read a plate, convert it to greyscale,
    /// threshold the greyscale into a mask, find the shapes the mask holds, and mark
    /// them on the plate that was read. The two nodes sit in different categories and
    /// pass a value no other node in the catalog produces, which is what makes the pair
    /// a workflow of its own rather than a step of another one.
    /// </summary>
    private static WorkflowDocument BuildContourWorkflow()
    {
        WorkflowDocument document = WorkflowDocument.Create("found");
        NodeInstance source = document.AddNode(new NodeTypeId(OpenCvNodeIds.ImageSourceTypeId), 1, new CanvasPosition(0, 0));
        NodeInstance conversion = document.AddNode(new NodeTypeId(OpenCvNodeIds.CvtColorTypeId), 1, new CanvasPosition(200, 0));
        NodeInstance threshold = document.AddNode(new NodeTypeId(OpenCvNodeIds.ThresholdTypeId), 1, new CanvasPosition(400, 0));
        NodeInstance find = document.AddNode(new NodeTypeId(OpenCvNodeIds.FindContoursTypeId), 1, new CanvasPosition(600, 0));
        NodeInstance draw = document.AddNode(new NodeTypeId(OpenCvNodeIds.DrawContoursTypeId), 1, new CanvasPosition(600, 200));
        NodeInstance save = document.AddNode(new NodeTypeId(OpenCvNodeIds.SaveImageTypeId), 1, new CanvasPosition(800, 200));

        document.SetNodeParameter(source.InstanceId, OpenCvNodeIds.PathParameter, "plate.png");
        document.SetNodeParameter(conversion.InstanceId, OpenCvNodeIds.ConversionParameter, OpenCvNodeIds.ConversionGray);
        document.SetNodeParameter(threshold.InstanceId, OpenCvNodeIds.ThresholdParameter, 100d);
        document.SetNodeParameter(threshold.InstanceId, OpenCvNodeIds.MaxValueParameter, 255d);
        document.SetNodeParameter(save.InstanceId, OpenCvNodeIds.PathParameter, "found.png");

        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, conversion.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(conversion.InstanceId, OpenCvNodeIds.ConvertedPortId, threshold.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(threshold.InstanceId, OpenCvNodeIds.ThresholdedPortId, find.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, draw.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(find.InstanceId, OpenCvNodeIds.ContoursPortId, draw.InstanceId, OpenCvNodeIds.ContoursPortId);
        document.AddConnection(draw.InstanceId, OpenCvNodeIds.DrawnPortId, save.InstanceId, OpenCvNodeIds.ImagePortId);

        return document;
    }

    /// <summary>
    /// Stores the workflow in the folder that also holds the images, because that
    /// folder is what a run resolves the document's relative paths against.
    /// </summary>
    private static string Save(WorkflowDocument document, TemporaryDirectory directory)
    {
        string path = directory.File("plate.vwflow");
        WorkflowDocumentWriter.Save(document, path);

        return path;
    }

    private static Task<WorkflowRunSummary> RunAsync(
        WorkflowDocument document,
        LeaseLedger ledger,
        TemporaryDirectory directory,
        ConvertingOutputObserver? observer = null,
        CancellationToken cancellationToken = default)
    {
        var catalog = new NodeDefinitionCatalog(new OpenCvNodeDefinitionProvider().GetDefinitions());
        SnapshotBuildResult captured = new WorkflowSnapshotFactory(catalog).Build(document);

        captured.Succeeded.ShouldBeTrue(
            string.Join(Environment.NewLine, captured.Validation.Diagnostics.Select(diagnostic => diagnostic.Message)));

        ExecutionPlan plan = new ExecutionPlanBuilder().Build(
            captured.Snapshot!,
            changedNodeIds: null,
            NodeExecutionEnvironment.At(directory.Path));

        return new WorkflowRunner(MapExecutorResolver.Create(ledger), ledger, observer: observer)
            .RunAsync(plan, cancellationToken);
    }

    private static void WritePlate(string path, int width, int height, byte value)
    {
        using var plate = new Mat(height, width, MatType.CV_8UC3, Scalar.All(value));

        Cv2.ImWrite(path, plate).ShouldBeTrue();
    }

    private static void WriteSplitPlate(string path, int width, int height, byte dark, byte bright)
    {
        using var plate = new Mat(height, width, MatType.CV_8UC3, Scalar.All(dark));

        Cv2.Rectangle(
            plate,
            new Rect(width / 2, 0, width - (width / 2), height),
            Scalar.All(bright),
            thickness: -1);

        Cv2.ImWrite(path, plate).ShouldBeTrue();
    }

    /// <summary>
    /// Writes a plate that holds one shape: a frame of the field value with a filled
    /// rectangle of the shape value in the middle of it, which is what a threshold
    /// leaves behind as a mask for a node to find the boundary of.
    /// </summary>
    private static void WriteShapePlate(string path, int width, int height, byte field, byte shape)
    {
        using var plate = new Mat(height, width, MatType.CV_8UC3, Scalar.All(field));

        Cv2.Rectangle(
            plate,
            new Rect(width / 4, height / 4, width / 2, height / 2),
            Scalar.All(shape),
            thickness: -1);

        Cv2.ImWrite(path, plate).ShouldBeTrue();
    }
}
