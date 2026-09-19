using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Execution;

public sealed class ExecutionPlanBuilderTests
{
    private readonly WorkflowSnapshotFactory _factory = new(TestNodes.DefaultCatalog());
    private readonly ExecutionPlanBuilder _builder = new();

    [Fact]
    public void Build_linear_graph_places_every_node_in_its_own_level()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        NodeInstance count = document.AddNode(TestNodes.CountType, 1, new CanvasPosition(400, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");
        document.AddConnection(blur.InstanceId, "blurred", count.InstanceId, "image");

        ExecutionPlan plan = _builder.Build(Snapshot(document));

        plan.ScheduledNodeIds.ShouldBe([source.InstanceId, blur.InstanceId, count.InstanceId]);
        plan.Levels.Count.ShouldBe(3);
        plan.Levels[0].NodeInstanceIds.ShouldBe([source.InstanceId]);
        plan.Levels[1].NodeInstanceIds.ShouldBe([blur.InstanceId]);
        plan.Levels[2].NodeInstanceIds.ShouldBe([count.InstanceId]);
        plan.BlockedNodeIds.ShouldBeEmpty();
    }

    [Fact]
    public void Build_diamond_graph_groups_parallel_nodes_into_one_level()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance first = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        NodeInstance second = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 200));
        NodeInstance merge = document.AddNode(TestNodes.MergeType, 1, new CanvasPosition(400, 0));
        document.AddConnection(source.InstanceId, "image", first.InstanceId, "image");
        document.AddConnection(source.InstanceId, "image", second.InstanceId, "image");
        document.AddConnection(first.InstanceId, "blurred", merge.InstanceId, "frames");
        document.AddConnection(second.InstanceId, "blurred", merge.InstanceId, "frames");

        ExecutionPlan plan = _builder.Build(Snapshot(document));

        plan.Levels.Count.ShouldBe(3);
        plan.Levels[0].NodeInstanceIds.ShouldBe([source.InstanceId]);
        plan.Levels[1].NodeInstanceIds.ShouldBe([first.InstanceId, second.InstanceId], ignoreOrder: true);
        plan.Levels[2].NodeInstanceIds.ShouldBe([merge.InstanceId]);
    }

    [Fact]
    public void Build_changed_node_schedules_only_the_downstream_branch()
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

        ExecutionPlan plan = _builder.Build(Snapshot(document), [blur.InstanceId]);

        plan.ScheduledNodeIds.ShouldBe([blur.InstanceId, count.InstanceId]);
        plan.ScheduledNodeIds.ShouldNotContain(source.InstanceId);
        plan.ScheduledNodeIds.ShouldNotContain(other.InstanceId);
        plan.ScheduledNodeIds.ShouldNotContain(otherCount.InstanceId);
        plan.Levels.Count.ShouldBe(2);
    }

    [Fact]
    public void Build_changed_node_keeps_input_bindings_from_outside_the_scope()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        ExecutionPlan plan = _builder.Build(Snapshot(document), [blur.InstanceId]);

        ExecutionPlanNode planned = plan.GetNode(blur.InstanceId);
        planned.Inputs.ShouldHaveSingleItem().ShouldBe(new NodeInputBinding("image", source.InstanceId, "image"));
    }

    [Fact]
    public void Build_disabled_node_blocks_its_dependent_branch()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        NodeInstance count = document.AddNode(TestNodes.CountType, 1, new CanvasPosition(400, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");
        document.AddConnection(blur.InstanceId, "blurred", count.InstanceId, "image");
        document.SetNodeEnabled(blur.InstanceId, false);

        ExecutionPlan plan = _builder.Build(Snapshot(document));

        plan.ScheduledNodeIds.ShouldBe([source.InstanceId]);
        plan.BlockedNodeIds.ShouldBe([blur.InstanceId, count.InstanceId], ignoreOrder: true);
    }

    [Fact]
    public void Build_disabled_producer_of_optional_input_keeps_the_consumer_running()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance image = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance mask = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 100));
        NodeInstance masking = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(300, 0));
        document.AddConnection(image.InstanceId, "image", masking.InstanceId, "image");
        document.AddConnection(mask.InstanceId, "image", masking.InstanceId, "mask");
        document.SetNodeEnabled(mask.InstanceId, false);

        ExecutionPlan plan = _builder.Build(Snapshot(document));

        plan.ScheduledNodeIds.ShouldBe([image.InstanceId, masking.InstanceId], ignoreOrder: true);
        plan.BlockedNodeIds.ShouldBe([mask.InstanceId]);
    }

    [Fact]
    public void Build_plan_nodes_are_in_topological_order()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance first = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        NodeInstance second = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(400, 0));
        NodeInstance merge = document.AddNode(TestNodes.MergeType, 1, new CanvasPosition(600, 0));
        document.AddConnection(source.InstanceId, "image", first.InstanceId, "image");
        document.AddConnection(source.InstanceId, "image", second.InstanceId, "image");
        document.AddConnection(first.InstanceId, "blurred", merge.InstanceId, "frames");
        document.AddConnection(second.InstanceId, "blurred", merge.InstanceId, "frames");

        ExecutionPlan plan = _builder.Build(Snapshot(document));

        List<Guid> order = [.. plan.Nodes.Select(node => node.InstanceId)];
        order.IndexOf(source.InstanceId).ShouldBeLessThan(order.IndexOf(first.InstanceId));
        order.IndexOf(source.InstanceId).ShouldBeLessThan(order.IndexOf(second.InstanceId));
        order.IndexOf(first.InstanceId).ShouldBeLessThan(order.IndexOf(merge.InstanceId));
        order.IndexOf(second.InstanceId).ShouldBeLessThan(order.IndexOf(merge.InstanceId));
    }

    [Fact]
    public void Build_unknown_changed_node_throws()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));

        Should.Throw<KeyNotFoundException>(() => _builder.Build(Snapshot(document), [Guid.NewGuid()]));
    }

    [Fact]
    public void Build_snapshot_with_cycle_throws()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        var snapshot = new WorkflowSnapshot(
            Guid.NewGuid(),
            1,
            [
                new SnapshotNode(first, TestNodes.Blur(), NodeParameterSet.Empty, true),
                new SnapshotNode(second, TestNodes.Blur(), NodeParameterSet.Empty, true),
            ],
            [
                new SnapshotEdge(Guid.NewGuid(), first, "blurred", second, "image"),
                new SnapshotEdge(Guid.NewGuid(), second, "blurred", first, "image"),
            ]);

        Should.Throw<InvalidOperationException>(() => _builder.Build(snapshot));
    }

    [Fact]
    public void Build_document_without_nodes_produces_an_empty_plan()
    {
        ExecutionPlan plan = _builder.Build(Snapshot(WorkflowDocument.Create("workflow")));

        plan.IsEmpty.ShouldBeTrue();
        plan.Levels.ShouldBeEmpty();
        plan.BlockedNodeIds.ShouldBeEmpty();
    }

    private WorkflowSnapshot Snapshot(WorkflowDocument document)
    {
        SnapshotBuildResult result = _factory.Build(document);
        result.Succeeded.ShouldBeTrue(string.Join(Environment.NewLine, result.Validation.Diagnostics.Select(item => item.Message)));
        return result.Snapshot!;
    }
}
