using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Execution;

/// <summary>
/// The scheduling behaviour of a run: order, parallelism, which branch a failure
/// blocks, and the lease lifetime that follows from it.
/// </summary>
public sealed class WorkflowRunnerTests : RunnerTestBase
{
    [Fact]
    public async Task Run_linear_graph_succeeds_and_releases_every_lease()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        NodeInstance count = document.AddNode(TestNodes.CountType, 1, new CanvasPosition(400, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");
        document.AddConnection(blur.InstanceId, "blurred", count.InstanceId, "image");

        LeaseLedger ledger = NewLedger();
        var sourceFrames = new FrameSource(ledger);
        var blurredFrames = new FrameSource(ledger, "blurred");
        ImageFrameValue? consumed = null;

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, sourceFrames.Executor)
            .Add(TestNodes.BlurExecutorTypeId, (request, _) =>
            {
                consumed = request.Inputs["image"] as ImageFrameValue;
                return Task.FromResult(Succeeded(blurredFrames.Produce()));
            })
            .Add(TestNodes.CountExecutorTypeId, (_, _) => Task.FromResult(Succeeded(Outputs(("count", new NumberValue(7))))));

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.StateOf(source.InstanceId).ShouldBe(NodeRunState.Succeeded);
        summary.StateOf(blur.InstanceId).ShouldBe(NodeRunState.Succeeded);
        summary.StateOf(count.InstanceId).ShouldBe(NodeRunState.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();

        ImageFrameValue captured = consumed.ShouldNotBeNull();
        captured.Lease.ShouldBeSameAs(sourceFrames.Lease);

        sourceFrames.Lease.IsDisposed.ShouldBeTrue();
        blurredFrames.Lease.IsDisposed.ShouldBeTrue();
        sourceFrames.Lease.OutstandingReservationsAtRelease.ShouldBe(0);
        blurredFrames.Lease.OutstandingReservationsAtRelease.ShouldBe(0);
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_ignores_an_unavailable_optional_input_from_a_disabled_producer()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance image = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance mask = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 100));
        NodeInstance masking = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(300, 0));
        document.AddConnection(image.InstanceId, "image", masking.InstanceId, "image");
        document.AddConnection(mask.InstanceId, "image", masking.InstanceId, "mask");
        document.SetNodeEnabled(mask.InstanceId, false);

        LeaseLedger ledger = NewLedger();
        var frames = new FrameSource(ledger);
        NodeExecutionRequest? request = null;
        var maskingExecutor = new StubExecutor((executionRequest, _) =>
        {
            request = executionRequest;
            return Task.FromResult(Succeeded(Outputs()));
        });
        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, frames.Executor)
            .Add(TestNodes.MaskingExecutorTypeId, maskingExecutor);

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.StateOf(masking.InstanceId).ShouldBe(NodeRunState.Succeeded);
        maskingExecutor.Invocations.ShouldBe(1);
        request!.Inputs.Keys.ShouldBe(["image"]);
        frames.Lease.IsDisposed.ShouldBeTrue();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_fan_out_keeps_the_frame_alive_for_the_second_consumer()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance first = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        NodeInstance second = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 200));
        document.AddConnection(source.InstanceId, "image", first.InstanceId, "image");
        document.AddConnection(source.InstanceId, "image", second.InstanceId, "image");

        LeaseLedger ledger = NewLedger();
        var observed = new SignalledLedger(ledger);
        var sourceFrames = new FrameSource(observed);
        var blurredFrames = new FrameSource(observed, "blurred");
        TaskCompletionSource<bool> secondRan = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> firstMayFinish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool frameAliveForSecond = false;
        bool frameAliveForFirst = false;

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, sourceFrames.Executor)
            .Add(TestNodes.BlurExecutorTypeId, async (request, _) =>
            {
                if (request.NodeInstanceId == first.InstanceId)
                {
                    await firstMayFinish.Task;
                    frameAliveForFirst = !sourceFrames.Lease.IsDisposed;
                }
                else
                {
                    frameAliveForSecond = !sourceFrames.Lease.IsDisposed;
                    secondRan.TrySetResult(true);
                }

                return Succeeded(blurredFrames.Produce());
            });

        Task<WorkflowRunSummary> run = Runner(executors, observed, Options(parallelism: 2))
            .RunAsync(Plan(document), CancellationToken.None);

        await secondRan.Task.Within("the second consumer to reach its work");

        // The second consumer has let go of the reservation it held on the frame, and
        // the first still holds one, so the assertion below proves the frame outlives
        // the consumer that released it rather than that someone still holds it.
        await observed.ReservationsReleased(1).Within("the second consumer to release its reservation");

        firstMayFinish.TrySetResult(true);

        await run.Within("the run to finish once its first consumer was let go");
        WorkflowRunSummary summary = await run;

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        frameAliveForSecond.ShouldBeTrue();
        frameAliveForFirst.ShouldBeTrue();
        sourceFrames.Lease.IsDisposed.ShouldBeTrue();
        ledger.Created.ShouldBe(3);
        ledger.Released.ShouldBe(3);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_failed_node_blocks_only_its_dependent_branch()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        NodeInstance count = document.AddNode(TestNodes.CountType, 1, new CanvasPosition(400, 0));
        NodeInstance other = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 200));
        NodeInstance otherCount = document.AddNode(TestNodes.CountType, 1, new CanvasPosition(200, 200));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");
        document.AddConnection(blur.InstanceId, "blurred", count.InstanceId, "image");
        document.AddConnection(other.InstanceId, "image", otherCount.InstanceId, "image");

        LeaseLedger ledger = NewLedger();
        var sourceFrames = new FrameSource(ledger);
        var countExecutor = new StubExecutor((_, _) => Task.FromResult(Succeeded(Outputs(("count", new NumberValue(1))))));

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, sourceFrames.Executor)
            .Add(TestNodes.BlurExecutorTypeId, (_, _) => Task.FromResult(NodeExecutionResult.Failure(
                new NodeDiagnostic("TEST-BLUR-001", DiagnosticSeverity.Error, "The blur failed."),
                TimeSpan.FromMilliseconds(1))))
            .Add(TestNodes.CountExecutorTypeId, countExecutor);

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Failed);
        summary.StateOf(blur.InstanceId).ShouldBe(NodeRunState.Failed);
        summary.StateOf(count.InstanceId).ShouldBe(NodeRunState.Blocked);
        summary.StateOf(otherCount.InstanceId).ShouldBe(NodeRunState.Succeeded);
        summary.HasCode(DiagnosticCodes.NodeExecutionBlocked).ShouldBeTrue();

        summary.Nodes.Single(node => node.NodeInstanceId == blur.InstanceId)
            .Diagnostics.ShouldHaveSingleItem().NodeInstanceId.ShouldBe(blur.InstanceId);

        // Both source frames were published and both were released again, and the
        // failed consumer did not leave the producer's frame alive.
        sourceFrames.Leases.Count.ShouldBe(2);
        sourceFrames.Leases.ShouldAllBe(lease => lease.IsDisposed);
        sourceFrames.Leases.ShouldAllBe(lease => lease.OutstandingReservationsAtRelease == 0);
        countExecutor.Invocations.ShouldBe(1);
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_fan_in_input_port_blocks_the_consumer()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance first = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance second = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 200));
        NodeInstance merge = document.AddNode(TestNodes.MergeType, 1, new CanvasPosition(200, 0));
        document.AddConnection(first.InstanceId, "image", merge.InstanceId, "frames");
        document.AddConnection(second.InstanceId, "image", merge.InstanceId, "frames");

        LeaseLedger ledger = NewLedger();
        var sourceFrames = new FrameSource(ledger);
        var mergeExecutor = new StubExecutor((_, _) => Task.FromResult(Succeeded(Outputs())));

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, sourceFrames.Executor)
            .Add(TestNodes.MergeExecutorTypeId, mergeExecutor);

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.StateOf(first.InstanceId).ShouldBe(NodeRunState.Succeeded);
        summary.StateOf(second.InstanceId).ShouldBe(NodeRunState.Succeeded);
        summary.StateOf(merge.InstanceId).ShouldBe(NodeRunState.Blocked);
        mergeExecutor.Invocations.ShouldBe(0);

        summary.Nodes.Single(node => node.NodeInstanceId == merge.InstanceId)
            .Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("cannot collect fan-in");

        // The blocked consumer still releases the frames it had reserved.
        sourceFrames.Leases.Count.ShouldBe(2);
        sourceFrames.Leases.ShouldAllBe(lease => lease.IsDisposed);
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_producer_outside_the_plan_blocks_the_consumer()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        LeaseLedger ledger = NewLedger();
        var blurExecutor = new StubExecutor((_, _) => Task.FromResult(Succeeded(Outputs())));
        StubResolver executors = new StubResolver().Add(TestNodes.BlurExecutorTypeId, blurExecutor);

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document, [blur.InstanceId]), CancellationToken.None);

        summary.StateOf(blur.InstanceId).ShouldBe(NodeRunState.Blocked);
        summary.HasCode(DiagnosticCodes.NodeExecutionBlocked).ShouldBeTrue();
        blurExecutor.Invocations.ShouldBe(0);
        ledger.Created.ShouldBe(0);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_producer_outside_the_plan_runs_on_a_supplied_value()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        LeaseLedger ledger = NewLedger();
        LeaseLedger cacheLedger = NewLedger();
        FakeFrameLease cachedFrame = FakeFrameLease.Create(cacheLedger);
        var blurredFrames = new FrameSource(ledger, "blurred");
        ImageFrameValue? consumed = null;

        StubInputSource inputs = new StubInputSource()
            .Supply(source.InstanceId, "image", new ImageFrameValue(cachedFrame));

        StubResolver executors = new StubResolver()
            .Add(TestNodes.BlurExecutorTypeId, (request, _) =>
            {
                consumed = request.Inputs["image"] as ImageFrameValue;
                return Task.FromResult(Succeeded(blurredFrames.Produce()));
            });

        WorkflowRunSummary summary = await Runner(executors, ledger, Options(), inputs: inputs)
            .RunAsync(Plan(document, [blur.InstanceId]), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        consumed.ShouldNotBeNull().Lease.ShouldBeSameAs(cachedFrame);

        // The run releases what it produced and never what it was handed.
        cachedFrame.IsDisposed.ShouldBeFalse();
        cacheLedger.Outstanding.ShouldBe(1);
        blurredFrames.Lease.IsDisposed.ShouldBeTrue();
        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_parallelism_of_one_serializes_nodes_of_the_same_level()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 200));

        LeaseLedger ledger = NewLedger();
        var probe = new ConcurrencyProbe();
        int started = 0;
        TaskCompletionSource<bool> firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> firstMayFinish = new(TaskCreationOptions.RunContinuationsAsynchronously);

        StubResolver executors = new StubResolver().Add(TestNodes.SourceExecutorTypeId, (_, _) =>
            probe.TrackAsync(async () =>
            {
                if (Interlocked.Increment(ref started) == 1)
                {
                    // The first node is held until the test has seen it running, so
                    // the window in which a second node could run beside it is opened
                    // and closed by the test rather than by a moment of time.
                    firstStarted.TrySetResult(true);
                    await firstMayFinish.Task;
                }

                return Succeeded(Outputs());
            }));

        Task<WorkflowRunSummary> run = Runner(executors, ledger, Options(parallelism: 1))
            .RunAsync(Plan(document), CancellationToken.None);

        await firstStarted.Task.Within("the first node to start");

        Volatile.Read(ref started).ShouldBe(
            1,
            "a limit of one node must not start the second one while the first is still running.");

        firstMayFinish.TrySetResult(true);

        await run.Within("the run to finish once its held node was let go");
        WorkflowRunSummary summary = await run;

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Nodes.Count.ShouldBe(2);
        summary.Nodes.ShouldAllBe(node => node.State == NodeRunState.Succeeded);
        Volatile.Read(ref started).ShouldBe(2);
        probe.Peak.ShouldBe(1);
    }
}
