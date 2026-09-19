using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;
using VisionWeave.IntegrationTests.Support;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.IntegrationTests.OpenCv;

/// <summary>
/// A whole run over real native frames: the scheduler, the lease publications,
/// and the OpenCV executors together. Each test ends by asserting that the ledger
/// is flat, which is the invariant the detailed design requires of every scenario.
/// </summary>
public sealed class OpenCvExecutionTests
{
    [Fact]
    public async Task Run_linear_native_graph_releases_every_lease()
    {
        WorkflowDocument document = WorkflowDocument.Create("native");
        NodeInstance source = document.AddNode(NativeWorkflow.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = NativeWorkflow.AddOpenCvNode(document, OpenCvNodeIds.GaussianBlurTypeId, 200, 0);
        NodeInstance resize = NativeWorkflow.AddOpenCvNode(document, OpenCvNodeIds.ResizeTypeId, 400, 0);
        document.SetNodeParameter(blur.InstanceId, OpenCvNodeIds.KernelSizeParameter, 5);
        document.SetNodeParameter(blur.InstanceId, OpenCvNodeIds.SigmaParameter, 1d);
        document.SetNodeParameter(resize.InstanceId, OpenCvNodeIds.WidthParameter, 4);
        document.SetNodeParameter(resize.InstanceId, OpenCvNodeIds.HeightParameter, 4);
        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, blur.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(blur.InstanceId, OpenCvNodeIds.BlurredPortId, resize.InstanceId, OpenCvNodeIds.ImagePortId);

        LeaseLedger ledger = new();
        var sourceFrames = new NativeFrameSource(ledger);

        WorkflowRunSummary summary = await RunAsync(document, ledger, sourceFrames);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Nodes.ShouldAllBe(node => node.State == NodeRunState.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();

        sourceFrames.Lease.IsDisposed.ShouldBeTrue();
        sourceFrames.Lease.OutstandingReservationsAtRelease.ShouldBe(0);
        ledger.Created.ShouldBe(3);
        ledger.Released.ShouldBe(3);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_nodes_without_saved_parameters_run_on_declared_defaults()
    {
        WorkflowDocument document = WorkflowDocument.Create("native");
        NodeInstance source = document.AddNode(NativeWorkflow.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = NativeWorkflow.AddOpenCvNode(document, OpenCvNodeIds.GaussianBlurTypeId, 200, 0);
        NodeInstance resize = NativeWorkflow.AddOpenCvNode(document, OpenCvNodeIds.ResizeTypeId, 400, 0);
        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, blur.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(blur.InstanceId, OpenCvNodeIds.BlurredPortId, resize.InstanceId, OpenCvNodeIds.ImagePortId);

        LeaseLedger ledger = new();
        var sourceFrames = new NativeFrameSource(ledger);

        WorkflowRunSummary summary = await RunAsync(document, ledger, sourceFrames);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Nodes.ShouldAllBe(node => node.State == NodeRunState.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();
        ledger.Created.ShouldBe(3);
        ledger.Released.ShouldBe(3);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_with_an_invalid_kernel_fails_the_blur_and_still_releases_the_source_frame()
    {
        WorkflowDocument document = WorkflowDocument.Create("native");
        NodeInstance source = document.AddNode(NativeWorkflow.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = NativeWorkflow.AddOpenCvNode(document, OpenCvNodeIds.GaussianBlurTypeId, 200, 0);
        NodeInstance resize = NativeWorkflow.AddOpenCvNode(document, OpenCvNodeIds.ResizeTypeId, 400, 0);
        document.SetNodeParameter(blur.InstanceId, OpenCvNodeIds.KernelSizeParameter, 4);
        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, blur.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(blur.InstanceId, OpenCvNodeIds.BlurredPortId, resize.InstanceId, OpenCvNodeIds.ImagePortId);

        LeaseLedger ledger = new();
        var sourceFrames = new NativeFrameSource(ledger);

        WorkflowRunSummary summary = await RunAsync(document, ledger, sourceFrames);

        summary.Status.ShouldBe(WorkflowRunStatus.Failed);
        summary.StateOf(blur.InstanceId).ShouldBe(NodeRunState.Failed);
        summary.StateOf(resize.InstanceId).ShouldBe(NodeRunState.Blocked);
        summary.HasCode(DiagnosticCodes.NodeExecutionFailed).ShouldBeTrue();

        // The rejected node produced no frame, and the frame it consumed is gone
        // even though the node failed.
        sourceFrames.Lease.IsDisposed.ShouldBeTrue();
        sourceFrames.Lease.OutstandingReservationsAtRelease.ShouldBe(0);
        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_fan_out_keeps_the_native_frame_alive_for_both_consumers()
    {
        WorkflowDocument document = WorkflowDocument.Create("native");
        NodeInstance source = document.AddNode(NativeWorkflow.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance first = NativeWorkflow.AddOpenCvNode(document, OpenCvNodeIds.GaussianBlurTypeId, 200, 0);
        NodeInstance second = NativeWorkflow.AddOpenCvNode(document, OpenCvNodeIds.GaussianBlurTypeId, 200, 200);
        document.SetNodeParameter(first.InstanceId, OpenCvNodeIds.KernelSizeParameter, 3);
        document.SetNodeParameter(second.InstanceId, OpenCvNodeIds.KernelSizeParameter, 3);
        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, first.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, second.InstanceId, OpenCvNodeIds.ImagePortId);

        LeaseLedger ledger = new();
        var sourceFrames = new NativeFrameSource(ledger);

        WorkflowRunSummary summary = await RunAsync(document, ledger, sourceFrames);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();
        sourceFrames.Lease.ReservationsTaken.ShouldBe(2);

        // The first consumer must not free the frame the second one is reading.
        sourceFrames.Lease.IsDisposed.ShouldBeTrue();
        sourceFrames.Lease.OutstandingReservationsAtRelease.ShouldBe(0);
        ledger.Created.ShouldBe(3);
        ledger.Released.ShouldBe(3);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_preview_conversion_reads_frames_that_are_still_alive()
    {
        WorkflowDocument document = WorkflowDocument.Create("native");
        NodeInstance source = document.AddNode(NativeWorkflow.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = NativeWorkflow.AddOpenCvNode(document, OpenCvNodeIds.GaussianBlurTypeId, 200, 0);
        document.SetNodeParameter(blur.InstanceId, OpenCvNodeIds.KernelSizeParameter, 3);
        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, blur.InstanceId, OpenCvNodeIds.ImagePortId);

        LeaseLedger ledger = new();
        var sourceFrames = new NativeFrameSource(ledger);
        var observer = new ConvertingOutputObserver();

        WorkflowRunSummary summary = await RunAsync(document, ledger, sourceFrames, observer);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();
        observer.Previews.Count.ShouldBe(2);
        observer.Previews.ShouldAllBe(preview => preview.Width == 8 && preview.Height == 8);
        observer.FramesAliveAtConversion.ShouldAllBe(alive => alive);
        observer.RejectedFormats.ShouldBe(0);

        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    private static Task<WorkflowRunSummary> RunAsync(
        WorkflowDocument document,
        LeaseLedger ledger,
        NativeFrameSource sourceFrames,
        ConvertingOutputObserver? observer = null)
    {
        SnapshotBuildResult captured = new WorkflowSnapshotFactory(NativeWorkflow.Catalog()).Build(document);

        captured.Succeeded.ShouldBeTrue(
            string.Join(Environment.NewLine, captured.Validation.Diagnostics.Select(diagnostic => diagnostic.Message)));

        ExecutionPlan plan = new ExecutionPlanBuilder().Build(captured.Snapshot!);
        MapExecutorResolver executors = MapExecutorResolver.Create(
            ledger,
            (NativeWorkflow.SourceExecutorTypeId, new DelegateExecutor(sourceFrames.Executor)));

        var runner = new WorkflowRunner(executors, ledger, observer: observer);

        return runner.RunAsync(plan, CancellationToken.None);
    }
}
