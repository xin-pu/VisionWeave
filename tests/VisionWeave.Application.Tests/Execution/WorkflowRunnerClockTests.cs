using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Execution;

/// <summary>
/// The clock a run is given: how long it waits for a node that ignores
/// cancellation, what a level that finishes takes off the clock again, and the
/// durations the run and its nodes report. Every one of these is reached by
/// moving the clock, so none of them depends on how loaded the machine is, and
/// every wait for the run is bounded, so a signal that never arrives fails the
/// test instead of hanging the suite.
/// </summary>
public sealed class WorkflowRunnerClockTests : RunnerTestBase
{
    private static readonly DateTimeOffset Start = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Run_grace_period_expires_on_the_clock_and_quarantines_the_node()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        var clock = new TestClock(Start);
        LeaseLedger ledger = NewLedger();
        var observed = new SignalledLedger(ledger);
        var sourceFrames = new FrameSource(observed);
        var blurredFrames = new FrameSource(observed, "blurred");
        TaskCompletionSource<bool> blurStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> blurMayFinish = new(TaskCreationOptions.RunContinuationsAsynchronously);

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, sourceFrames.Executor)
            .Add(TestNodes.BlurExecutorTypeId, async (_, _) =>
            {
                // The frame is allocated before the node stops listening, which is
                // what a node that ignores cancellation looks like: it holds its
                // output while it keeps working.
                var produced = blurredFrames.Produce();
                blurStarted.TrySetResult(true);
                await blurMayFinish.Task;
                return Succeeded(produced);
            });

        using var cancellation = new CancellationTokenSource();
        Task<WorkflowRunSummary> run = Runner(
                executors,
                observed,
                Options(gracePeriod: TimeSpan.FromSeconds(5)),
                timeProvider: clock)
            .RunAsync(Plan(document), cancellation.Token);

        await blurStarted.Task.Within("the run to reach the node that stops listening");
        Task armed = clock.WaitForNextTimer();
        await cancellation.CancelAsync();
        await armed.Within("the run to take on its wait for the cancelled node");

        clock.Advance(TimeSpan.FromSeconds(4));
        run.IsCompleted.ShouldBeFalse("the grace period has not passed yet.");

        clock.Advance(TimeSpan.FromSeconds(1));

        await run.Within("the run to quarantine the node that ignored the stop");
        WorkflowRunSummary summary = await run;

        summary.WasCancelled.ShouldBeTrue();
        summary.Status.ShouldBe(WorkflowRunStatus.Cancelled);
        summary.QuarantinedNodeIds.ShouldBe([blur.InstanceId]);
        summary.HasCode(DiagnosticCodes.ExecutorIgnoredCancellation).ShouldBeTrue();
        summary.StateOf(source.InstanceId).ShouldBe(NodeRunState.Succeeded);
        summary.StateOf(blur.InstanceId).ShouldBe(NodeRunState.Cancelled);

        // The runaway executor may still be reading its input, so the frames stay
        // alive until it finishes instead of being freed from under it.
        sourceFrames.Lease.IsDisposed.ShouldBeFalse();
        blurredFrames.Lease.IsDisposed.ShouldBeFalse();
        ledger.Outstanding.ShouldBe(2);

        blurMayFinish.TrySetResult(true);

        await observed.Flat.Within("the run to release the frames its runaway node still held");

        sourceFrames.Lease.IsDisposed.ShouldBeTrue();
        sourceFrames.Lease.OutstandingReservationsAtRelease.ShouldBe(0);
        blurredFrames.Lease.IsDisposed.ShouldBeTrue();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_level_that_finishes_leaves_no_grace_wait_pending()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        var clock = new TestClock(Start);
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
        Task<WorkflowRunSummary> run = Runner(
                executors,
                ledger,
                Options(gracePeriod: TimeSpan.FromSeconds(5)),
                timeProvider: clock)
            .RunAsync(Plan(document), cancellation.Token);

        await blurStarted.Task.Within("the run to reach the node that listens");
        await cancellation.CancelAsync();

        await run.Within("the run to finish once its node stopped");
        WorkflowRunSummary summary = await run;

        summary.WasCancelled.ShouldBeTrue();
        summary.QuarantinedNodeIds.ShouldBeEmpty();
        summary.StateOf(blur.InstanceId).ShouldBe(NodeRunState.Cancelled);

        // The node noticed the cancellation inside the grace period, so the wait
        // the level took on was taken off again and the clock has nothing to reach.
        clock.PendingTimers.ShouldBe(0);

        clock.Advance(TimeSpan.FromHours(1));

        summary.QuarantinedNodeIds.ShouldBeEmpty();
        summary.HasCode(DiagnosticCodes.ExecutorIgnoredCancellation).ShouldBeFalse();
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_reports_the_durations_the_clock_shows()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));

        var clock = new TestClock(Start);
        LeaseLedger ledger = NewLedger();
        var sourceFrames = new FrameSource(ledger);

        StubResolver executors = new StubResolver().Add(TestNodes.SourceExecutorTypeId, (_, _) =>
        {
            // A node that reports no duration of its own is measured by the run,
            // so the clock alone decides what both of them report having taken.
            clock.Advance(TimeSpan.FromSeconds(2));
            return Task.FromResult(NodeExecutionResult.Success(sourceFrames.Produce(), TimeSpan.Zero));
        });

        WorkflowRunSummary summary = await Runner(executors, ledger, timeProvider: clock)
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Duration.ShouldBe(TimeSpan.FromSeconds(2));
        summary.Nodes.Single(node => node.NodeInstanceId == source.InstanceId)
            .Duration.ShouldBe(TimeSpan.FromSeconds(2));
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task A_wait_nothing_arms_fails_the_test_instead_of_hanging_it()
    {
        var clock = new TestClock(Start);
        bool failed = false;

        try
        {
            // The limit is the test's own here, which is how a suite that waits for
            // a signal it never gets reports the signal rather than stopping at it.
            await clock.WaitForNextTimer().Within("a timer nothing arms", TimeSpan.FromMilliseconds(50));
        }
        catch (ShouldAssertException failure)
        {
            failure.Message.ShouldContain("a timer nothing arms");
            failed = true;
        }

        failed.ShouldBeTrue("a signal that never arrives must fail the test that waits for it");
    }
}
