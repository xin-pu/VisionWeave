using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Execution;

public sealed class WorkflowSnapshotFactoryTests
{
    private readonly WorkflowSnapshotFactory _factory = new(TestNodes.DefaultCatalog());

    [Fact]
    public void Build_valid_document_captures_identity_revision_and_graph()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        WorkflowConnection connection = document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        SnapshotBuildResult result = _factory.Build(document);

        result.Succeeded.ShouldBeTrue();
        result.Validation.IsValid.ShouldBeTrue();

        WorkflowSnapshot snapshot = result.Snapshot!;
        snapshot.DocumentId.ShouldBe(document.Id);
        snapshot.Revision.ShouldBe(document.Revision);
        snapshot.Nodes.Count.ShouldBe(2);
        snapshot.Edges.ShouldHaveSingleItem().ShouldBe(
            new SnapshotEdge(connection.ConnectionId, source.InstanceId, "image", blur.InstanceId, "image"));
    }

    [Fact]
    public void Build_valid_document_resolves_definitions_and_parameters()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");
        document.SetNodeParameter(blur.InstanceId, "kernelSize", 5);

        WorkflowSnapshot snapshot = _factory.Build(document).Snapshot!;

        SnapshotNode node = snapshot.GetNode(blur.InstanceId);
        node.Definition.TypeId.ShouldBe(TestNodes.BlurType);
        node.Definition.TypeVersion.ShouldBe(1);
        node.Parameters.GetInt32("kernelSize").ShouldBe(5);
        node.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Build_invalid_document_reports_diagnostics_without_a_snapshot()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        document.AddNode(TestNodes.AbsentType, 1, new CanvasPosition(0, 0));

        SnapshotBuildResult result = _factory.Build(document);

        result.Succeeded.ShouldBeFalse();
        result.Snapshot.ShouldBeNull();
        result.Validation.HasCode(DiagnosticCodes.MissingNodeDefinition).ShouldBeTrue();
    }

    [Fact]
    public void Build_fan_in_keeps_connection_declaration_order()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance first = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance second = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 100));
        NodeInstance merge = document.AddNode(TestNodes.MergeType, 1, new CanvasPosition(300, 0));
        document.AddConnection(second.InstanceId, "image", merge.InstanceId, "frames");
        document.AddConnection(first.InstanceId, "image", merge.InstanceId, "frames");

        WorkflowSnapshot snapshot = _factory.Build(document).Snapshot!;

        snapshot.Edges.Select(edge => edge.SourceNodeId).ShouldBe([second.InstanceId, first.InstanceId]);
    }
}
