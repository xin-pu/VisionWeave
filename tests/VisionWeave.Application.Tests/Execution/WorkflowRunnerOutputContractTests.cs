using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;
using VisionWeave.Contracts.Values;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Execution;

/// <summary>
/// The output side of the executor contract: what the runtime accepts from a node
/// that reported success. Each rejection also has to release the frame the node
/// created, and none of them may release a frame the node only borrowed, so every
/// test ends on the lease ledger.
/// </summary>
public sealed class WorkflowRunnerOutputContractTests : RunnerTestBase
{
    private const string SplitExecutorTypeId = "test.split";

    private static readonly NodeDefinition SplitDefinition = new(
        new NodeTypeId("visionweave.test.split"),
        TypeVersion: 1,
        DisplayName: "Split frames",
        Category: "Composition",
        [
            TestNodes.Input("image", BuiltInPortTypeIds.ImageFrame),
            TestNodes.Output("first", BuiltInPortTypeIds.ImageFrame),
            TestNodes.Output("second", BuiltInPortTypeIds.ImageFrame),
        ],
        Parameters: [],
        SplitExecutorTypeId);

    [Fact]
    public async Task Run_output_on_a_port_that_is_not_declared_fails_the_producer()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        LeaseLedger ledger = NewLedger();
        var frames = new FrameSource(ledger, "blurred");

        StubResolver executors = new StubResolver().Add(TestNodes.SourceExecutorTypeId, frames.Executor);

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Failed);
        summary.HasCode(DiagnosticCodes.NodeOutputUndeclared).ShouldBeTrue();
        frames.Lease.IsDisposed.ShouldBeTrue();
        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_output_on_an_input_port_fails_the_producer()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance masking = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(300, 0));
        document.AddConnection(source.InstanceId, "image", masking.InstanceId, "image");
        LeaseLedger ledger = NewLedger();
        var frames = new FrameSource(ledger);
        var maskingFrames = new FrameSource(ledger, "image");

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, frames.Executor)
            .Add(TestNodes.MaskingExecutorTypeId, maskingFrames.Executor);

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Failed);
        summary.StateOf(source.InstanceId).ShouldBe(NodeRunState.Succeeded);
        summary.StateOf(masking.InstanceId).ShouldBe(NodeRunState.Failed);
        summary.HasCode(DiagnosticCodes.NodeOutputUndeclared).ShouldBeTrue();
        maskingFrames.Lease.IsDisposed.ShouldBeTrue();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_output_with_the_wrong_port_type_fails_the_producer()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance masking = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(300, 0));
        document.AddConnection(source.InstanceId, "image", masking.InstanceId, "image");
        LeaseLedger ledger = NewLedger();
        var frames = new FrameSource(ledger);

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, frames.Executor)
            .Add(
                TestNodes.MaskingExecutorTypeId,
                (_, _) => Task.FromResult(Succeeded(Outputs(("masked", new NumberValue(3))))));

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Failed);
        summary.HasCode(DiagnosticCodes.NodeOutputTypeMismatch).ShouldBeTrue();
        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_missing_value_on_a_declared_port_fails_the_producer()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance masking = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(300, 0));
        document.AddConnection(source.InstanceId, "image", masking.InstanceId, "image");
        LeaseLedger ledger = NewLedger();
        var frames = new FrameSource(ledger);

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, frames.Executor)
            .Add(TestNodes.MaskingExecutorTypeId, (_, _) => Task.FromResult(Succeeded(Outputs(("masked", null!)))));

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Failed);
        summary.HasCode(DiagnosticCodes.NodeOutputTypeMismatch).ShouldBeTrue();
        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_lease_received_as_input_and_reported_as_output_fails_the_producer()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance masking = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(300, 0));
        document.AddConnection(source.InstanceId, "image", masking.InstanceId, "image");
        LeaseLedger ledger = NewLedger();
        var frames = new FrameSource(ledger);
        var maskingExecutor = new StubExecutor(
            (request, _) => Task.FromResult(Succeeded(Outputs(("masked", request.Inputs["image"])))));

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, frames.Executor)
            .Add(TestNodes.MaskingExecutorTypeId, maskingExecutor);

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Failed);
        summary.StateOf(masking.InstanceId).ShouldBe(NodeRunState.Failed);
        summary.HasCode(DiagnosticCodes.NodeOutputLeaseNotOwned).ShouldBeTrue();

        // The source frame was released once, by the run that owns it, and never
        // while the node that borrowed it was still reading.
        frames.Lease.IsDisposed.ShouldBeTrue();
        frames.Lease.OutstandingReservationsAtRelease.ShouldBe(0);
        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_failed_result_reporting_a_borrowed_lease_does_not_release_it()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance masking = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(300, 0));
        document.AddConnection(source.InstanceId, "image", masking.InstanceId, "image");
        LeaseLedger ledger = NewLedger();
        var frames = new FrameSource(ledger);

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, frames.Executor)
            .Add(
                TestNodes.MaskingExecutorTypeId,
                (request, _) => Task.FromResult(new NodeExecutionResult
                {
                    Status = NodeExecutionStatus.Failed,
                    Outputs = Outputs(("masked", request.Inputs["image"])),
                    Diagnostics =
                    [
                        new NodeDiagnostic(
                            DiagnosticCodes.NodeExecutionFailed,
                            DiagnosticSeverity.Error,
                            "The node failed.",
                            null),
                    ],
                }));

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(Plan(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Failed);
        summary.HasCode(DiagnosticCodes.NodeExecutionFailed).ShouldBeTrue();
        frames.Lease.IsDisposed.ShouldBeTrue();
        frames.Lease.OutstandingReservationsAtRelease.ShouldBe(0);
        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_one_lease_reported_on_two_output_ports_fails_the_producer()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance split = document.AddNode(SplitDefinition.TypeId, 1, new CanvasPosition(200, 0));
        NodeInstance count = document.AddNode(TestNodes.CountType, 1, new CanvasPosition(400, 0));
        document.AddConnection(source.InstanceId, "image", split.InstanceId, "image");
        document.AddConnection(split.InstanceId, "first", count.InstanceId, "image");
        LeaseLedger ledger = NewLedger();
        var frames = new FrameSource(ledger);
        var splitFrames = new FrameSource(ledger);
        var splitExecutor = new StubExecutor((_, _) =>
        {
            FakeFrameLease shared = splitFrames.ProduceLease();

            return Task.FromResult(Succeeded(new Dictionary<string, PortValue>(StringComparer.Ordinal)
            {
                ["first"] = new ImageFrameValue(shared),
                ["second"] = new ImageFrameValue(shared),
            }));
        });

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, frames.Executor)
            .Add(SplitExecutorTypeId, splitExecutor)
            .Add(TestNodes.CountExecutorTypeId, (_, _) => Task.FromResult(Succeeded(Outputs(("count", new NumberValue(1))))));

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(PlanWithSplit(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Failed);
        summary.StateOf(split.InstanceId).ShouldBe(NodeRunState.Failed);
        summary.StateOf(count.InstanceId).ShouldBe(NodeRunState.Blocked);
        summary.HasCode(DiagnosticCodes.NodeOutputLeaseAliased).ShouldBeTrue();
        splitFrames.Lease.IsDisposed.ShouldBeTrue();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_distinct_leases_satisfy_two_declared_output_ports()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance split = document.AddNode(SplitDefinition.TypeId, 1, new CanvasPosition(200, 0));
        NodeInstance count = document.AddNode(TestNodes.CountType, 1, new CanvasPosition(400, 0));
        document.AddConnection(source.InstanceId, "image", split.InstanceId, "image");
        document.AddConnection(split.InstanceId, "first", count.InstanceId, "image");
        LeaseLedger ledger = NewLedger();
        var frames = new FrameSource(ledger);
        var splitFrames = new FrameSource(ledger);
        var splitExecutor = new StubExecutor((_, _) => Task.FromResult(Succeeded(
            new Dictionary<string, PortValue>(StringComparer.Ordinal)
            {
                ["first"] = new ImageFrameValue(splitFrames.ProduceLease()),
                ["second"] = new ImageFrameValue(splitFrames.ProduceLease()),
            })));

        StubResolver executors = new StubResolver()
            .Add(TestNodes.SourceExecutorTypeId, frames.Executor)
            .Add(SplitExecutorTypeId, splitExecutor)
            .Add(TestNodes.CountExecutorTypeId, (_, _) => Task.FromResult(Succeeded(Outputs(("count", new NumberValue(1))))));

        WorkflowRunSummary summary = await Runner(executors, ledger, Options())
            .RunAsync(PlanWithSplit(document), CancellationToken.None);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();
        summary.StateOf(split.InstanceId).ShouldBe(NodeRunState.Succeeded);
        summary.StateOf(count.InstanceId).ShouldBe(NodeRunState.Succeeded);

        // The frame no consumer subscribes to is released as soon as it is
        // published, and the one the count node reads stays alive until the run
        // ends, so both frames are gone and nothing is left reserved.
        splitFrames.Leases.Count.ShouldBe(2);
        splitFrames.Leases.ShouldAllBe(lease => lease.IsDisposed);
        ledger.Created.ShouldBe(3);
        ledger.Released.ShouldBe(3);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    private ExecutionPlan PlanWithSplit(WorkflowDocument document)
    {
        SnapshotBuildResult built = new WorkflowSnapshotFactory(
            TestNodes.Catalog(TestNodes.Source(), SplitDefinition, TestNodes.Count())).Build(document);

        built.Succeeded.ShouldBeTrue(
            string.Join(Environment.NewLine, built.Validation.Diagnostics.Select(item => item.Message)));

        return new ExecutionPlanBuilder().Build(built.Snapshot!);
    }
}
