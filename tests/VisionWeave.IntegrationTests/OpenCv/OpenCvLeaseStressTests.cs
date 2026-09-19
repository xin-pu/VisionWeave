using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;
using VisionWeave.IntegrationTests.Support;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.IntegrationTests.OpenCv;

/// <summary>
/// The lease-accounting scenarios the design requires to end with no outstanding
/// native lease: a wide fan-out under parallel execution, and a cancellation that
/// lands while a preview is being converted.
/// </summary>
public sealed class OpenCvLeaseStressTests
{
    [Fact]
    public async Task Run_repeated_wide_fan_out_returns_the_ledger_to_zero()
    {
        const int consumerCount = 8;

        WorkflowDocument document = WorkflowDocument.Create("fan-out");
        NodeInstance source = document.AddNode(NativeWorkflow.SourceType, 1, new CanvasPosition(0, 0));

        for (int index = 0; index < consumerCount; index++)
        {
            NodeInstance blur = NativeWorkflow.AddOpenCvNode(document, OpenCvNodeIds.GaussianBlurTypeId, 200, index * 120);
            document.SetNodeParameter(blur.InstanceId, OpenCvNodeIds.KernelSizeParameter, 3);
            document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, blur.InstanceId, OpenCvNodeIds.ImagePortId);
        }

        LeaseLedger ledger = new();
        var options = new ExecutionOptions { MaxDegreeOfParallelism = 4 };

        for (int run = 1; run <= 3; run++)
        {
            var sourceFrames = new NativeFrameSource(ledger);

            WorkflowRunSummary summary = await RunAsync(document, ledger, sourceFrames, options, observer: null);

            summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
            summary.Diagnostics.ShouldBeEmpty();

            // One source frame plus one frame per consumer, every one of them freed
            // before the next run starts.
            long expected = run * (1 + consumerCount);
            ledger.Created.ShouldBe(expected);
            ledger.Released.ShouldBe(expected);
            ledger.Outstanding.ShouldBe(0);
            ledger.ReservationsOutstanding.ShouldBe(0);

            sourceFrames.Leases.ShouldAllBe(lease => lease.IsDisposed);
            sourceFrames.Lease.OutstandingReservationsAtRelease.ShouldBe(0);
            sourceFrames.Lease.ReservationsTaken.ShouldBe(consumerCount);
        }
    }

    [Fact]
    public async Task Run_cancelled_during_preview_conversion_releases_every_lease()
    {
        WorkflowDocument document = WorkflowDocument.Create("cancel-during-preview");
        NodeInstance source = document.AddNode(NativeWorkflow.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = NativeWorkflow.AddOpenCvNode(document, OpenCvNodeIds.GaussianBlurTypeId, 200, 0);
        document.SetNodeParameter(blur.InstanceId, OpenCvNodeIds.KernelSizeParameter, 3);
        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, blur.InstanceId, OpenCvNodeIds.ImagePortId);

        LeaseLedger ledger = new();
        var sourceFrames = new NativeFrameSource(ledger);
        using var cancellation = new CancellationTokenSource();
        var observer = new ConvertingOutputObserver(
            delay: TimeSpan.FromMilliseconds(150),
            onBeforeConvert: cancellation.Cancel);

        // The grace period is generous on purpose: the conversion is meant to
        // finish, so the run proves that a cancellation releases the frame rather
        // than quarantining the node that was converting it.
        var options = new ExecutionOptions { CancellationGracePeriod = TimeSpan.FromSeconds(5) };

        WorkflowRunSummary summary = await RunAsync(
            document,
            ledger,
            sourceFrames,
            options,
            observer,
            cancellation.Token);

        summary.WasCancelled.ShouldBeTrue();
        summary.HasCode(DiagnosticCodes.LeaseLeaked).ShouldBeFalse();
        summary.HasCode(DiagnosticCodes.ExecutorIgnoredCancellation).ShouldBeFalse();
        summary.QuarantinedNodeIds.ShouldBeEmpty();

        observer.Previews.Count.ShouldBe(1);
        observer.FramesAliveAtConversion.ShouldAllBe(alive => alive);
        sourceFrames.Leases.ShouldAllBe(lease => lease.IsDisposed);
        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    private static Task<WorkflowRunSummary> RunAsync(
        WorkflowDocument document,
        LeaseLedger ledger,
        NativeFrameSource sourceFrames,
        ExecutionOptions options,
        ConvertingOutputObserver? observer,
        CancellationToken cancellationToken = default)
    {
        SnapshotBuildResult captured = new WorkflowSnapshotFactory(NativeWorkflow.Catalog()).Build(document);

        captured.Succeeded.ShouldBeTrue(
            string.Join(Environment.NewLine, captured.Validation.Diagnostics.Select(diagnostic => diagnostic.Message)));

        ExecutionPlan plan = new ExecutionPlanBuilder().Build(captured.Snapshot!);
        MapExecutorResolver executors = MapExecutorResolver.Create(
            ledger,
            (NativeWorkflow.SourceExecutorTypeId, new DelegateExecutor(sourceFrames.Executor)));

        var runner = new WorkflowRunner(executors, ledger, options, observer);

        return runner.RunAsync(plan, cancellationToken);
    }
}
