using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Execution;

/// <summary>
/// What a run does when it is cancelled: the branch that observes cancellation
/// stops, the branch that ignores it is quarantined instead of abandoned, and no
/// frame is freed while something may still be reading it.
/// </summary>
public sealed class WorkflowRunnerCancellationTests : RunnerTestBase
{
    [Fact]
    public async Task Run_cancelled_run_releases_every_lease()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        LeaseLedger ledger = NewLedger();
        var sourceFrames = new FrameSource(ledger);
        TaskCompletionSource<bool> blurStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, sourceFrames.Executor)
            .Add(TestNodes.BlurExecutorTypeId, async (_, cancellationToken) =>
            {
                blurStarted.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Succeeded(Outputs());
            });

        using var cancellation = new CancellationTokenSource();
        Task<WorkflowRunSummary> run = Runner(executors, ledger, Options())
            .RunAsync(Plan(document), cancellation.Token);

        await blurStarted.Task;
        await cancellation.CancelAsync();

        WorkflowRunSummary summary = await run;

        summary.WasCancelled.ShouldBeTrue();
        summary.Status.ShouldBe(WorkflowRunStatus.Cancelled);
        summary.StateOf(source.InstanceId).ShouldBe(NodeRunState.Succeeded);
        summary.StateOf(blur.InstanceId).ShouldBe(NodeRunState.Cancelled);
        summary.QuarantinedNodeIds.ShouldBeEmpty();
        summary.HasCode(DiagnosticCodes.NodeExecutionCancelled).ShouldBeTrue();

        sourceFrames.Lease.IsDisposed.ShouldBeTrue();
        sourceFrames.Lease.OutstandingReservationsAtRelease.ShouldBe(0);
        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_executor_that_ignores_cancellation_is_quarantined()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        LeaseLedger ledger = NewLedger();
        var sourceFrames = new FrameSource(ledger);
        var blurredFrames = new FrameSource(ledger, "blurred");
        TaskCompletionSource<bool> blurStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> blurMayFinish = new(TaskCreationOptions.RunContinuationsAsynchronously);

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, sourceFrames.Executor)
            .Add(TestNodes.BlurExecutorTypeId, async (_, _) =>
            {
                blurStarted.TrySetResult(true);
                await blurMayFinish.Task;
                return Succeeded(blurredFrames.Produce());
            });

        using var cancellation = new CancellationTokenSource();
        Task<WorkflowRunSummary> run = Runner(executors, ledger, Options(gracePeriod: TimeSpan.FromMilliseconds(50)))
            .RunAsync(Plan(document), cancellation.Token);

        await blurStarted.Task;
        await cancellation.CancelAsync();

        WorkflowRunSummary summary = await run;

        summary.WasCancelled.ShouldBeTrue();
        summary.Status.ShouldBe(WorkflowRunStatus.Cancelled);
        summary.QuarantinedNodeIds.ShouldBe([blur.InstanceId]);
        summary.HasCode(DiagnosticCodes.ExecutorIgnoredCancellation).ShouldBeTrue();
        summary.StateOf(blur.InstanceId).ShouldBe(NodeRunState.Cancelled);
        summary.HasCode(DiagnosticCodes.LeaseLeaked).ShouldBeFalse();

        // The runaway executor may still be reading its input, so its frame is
        // deliberately left alive instead of being freed under it.
        sourceFrames.Lease.IsDisposed.ShouldBeFalse();
        ledger.Outstanding.ShouldBe(1);

        blurMayFinish.TrySetResult(true);

        await WaitForAsync(
            () => ledger.Outstanding <= 0,
            "The quarantined node must release its input once it finishes on its own.");

        sourceFrames.Lease.IsDisposed.ShouldBeTrue();
        sourceFrames.Lease.OutstandingReservationsAtRelease.ShouldBe(0);
        blurredFrames.Lease.IsDisposed.ShouldBeTrue();
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }
}
