using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Values;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Execution;

/// <summary>
/// The failure and ownership behaviour of a run: what a missing executor, a
/// throwing executor, a failing cleanup, and a failing preview conversion do to
/// the node, the run, and the native resources it owns.
/// </summary>
public sealed class WorkflowRunnerResourceTests : RunnerTestBase
{
    [Fact]
    public async Task Run_node_without_executor_fails_with_missing_executor()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        LeaseLedger ledger = NewLedger();

        WorkflowRunSummary summary = await Runner(new StubResolver(), ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Failed);
        summary.StateOf(source.InstanceId).ShouldBe(NodeRunState.Failed);
        summary.HasCode(DiagnosticCodes.MissingExecutor).ShouldBeTrue();
        ledger.Created.ShouldBe(0);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_executor_that_throws_fails_the_node_and_releases_its_resources()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        LeaseLedger ledger = NewLedger();
        var frames = new FrameSource(ledger);

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, (request, _) =>
            {
                request.Resources.Own(frames.ProduceLease());
                throw new InvalidOperationException("The executor failed.");
            });

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Failed);
        summary.HasCode(DiagnosticCodes.NodeExecutionFailed).ShouldBeTrue();

        NodeRunReport report = summary.Nodes.Single(node => node.NodeInstanceId == source.InstanceId);
        report.Diagnostics.ShouldHaveSingleItem().Exception.ShouldBeOfType<InvalidOperationException>();

        frames.Lease.IsDisposed.ShouldBeTrue();
        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_private_resource_is_released_when_the_node_finished()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        LeaseLedger ledger = NewLedger();
        var resource = new TrackingDisposable();

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, (request, _) =>
            {
                request.Resources.Own(resource);
                return Task.FromResult(Succeeded(Outputs()));
            });

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        resource.WasDisposed.ShouldBeTrue();
        resource.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task Run_resource_that_fails_to_release_is_a_warning_and_the_node_succeeded()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        LeaseLedger ledger = NewLedger();
        var resource = new TrackingDisposable(new InvalidOperationException("The resource cannot be released."));

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, (request, _) =>
            {
                request.Resources.Own(resource);
                return Task.FromResult(Succeeded(Outputs()));
            });

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.HasCode(DiagnosticCodes.ResourceDisposalFailed).ShouldBeTrue();
        summary.Diagnostics.Single(diagnostic => diagnostic.Code == DiagnosticCodes.ResourceDisposalFailed)
            .Severity.ShouldBe(DiagnosticSeverity.Warning);
        resource.WasDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Run_preview_failure_is_a_warning_and_the_node_still_succeeded()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        LeaseLedger ledger = NewLedger();
        var frames = new FrameSource(ledger);

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, frames.Executor);

        var observer = new RecordingOutputObserver((_, _) => throw new InvalidOperationException("The preview failed."));

        WorkflowRunSummary summary = await Runner(executors, ledger, Options(), observer)
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.StateOf(source.InstanceId).ShouldBe(NodeRunState.Succeeded);
        summary.HasCode(DiagnosticCodes.PreviewConversionFailed).ShouldBeTrue();
        summary.Diagnostics.Single(diagnostic => diagnostic.Code == DiagnosticCodes.PreviewConversionFailed)
            .Severity.ShouldBe(DiagnosticSeverity.Warning);

        observer.Invocations.ShouldBe(1);
        frames.Lease.IsDisposed.ShouldBeTrue();
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_preview_conversion_fence_keeps_the_frame_alive()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        LeaseLedger ledger = NewLedger();
        var frames = new FrameSource(ledger);
        TaskCompletionSource<bool> conversionStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> conversionMayFinish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool frameAliveDuringConversion = false;

        var observer = new RecordingOutputObserver(async (outputs, _) =>
        {
            var produced = (ImageFrameValue)outputs.Values["image"];
            frameAliveDuringConversion = !produced.Lease.IsDisposed;
            conversionStarted.TrySetResult(true);
            await conversionMayFinish.Task;
        });

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, frames.Executor);

        Task<WorkflowRunSummary> run = Runner(executors, ledger, Options(), observer)
            .RunAsync(Plan(document), CancellationToken.None);

        await conversionStarted.Task;
        // The producer already dropped its own ownership, so only the conversion
        // fence can keep the frame alive here.
        frames.Lease.IsDisposed.ShouldBeFalse();
        conversionMayFinish.TrySetResult(true);

        WorkflowRunSummary summary = await run;

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        frameAliveDuringConversion.ShouldBeTrue();
        frames.Lease.IsDisposed.ShouldBeTrue();
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }
}
