using Shouldly;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Domain.Tests.Workflows;

public sealed class WorkflowDocumentTests
{
    private static readonly NodeTypeId BlurType = new("visionweave.opencv.gaussian-blur");
    private static readonly NodeTypeId SourceType = new("visionweave.opencv.image-source");

    [Fact]
    public void Create_valid_name_creates_initial_document()
    {
        WorkflowDocument document = WorkflowDocument.Create("New workflow");

        document.Id.ShouldNotBe(Guid.Empty);
        document.Name.ShouldBe("New workflow");
        document.Revision.ShouldBe(0);
        document.Nodes.ShouldBeEmpty();
        document.Connections.ShouldBeEmpty();
        document.ModifiedUtc.ShouldBe(document.CreatedUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_blank_name_throws(string name)
    {
        Should.Throw<ArgumentException>(() => WorkflowDocument.Create(name));
    }

    [Fact]
    public void AddNode_valid_type_increments_revision()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");

        NodeInstance node = document.AddNode(BlurType, 1, new CanvasPosition(10, 20));

        document.Revision.ShouldBe(1);
        node.NodeTypeId.ShouldBe(BlurType);
        node.TypeVersion.ShouldBe(1);
        node.Position.ShouldBe(new CanvasPosition(10, 20));
        node.IsEnabled.ShouldBeTrue();
        document.Nodes.Count.ShouldBe(1);
    }

    [Fact]
    public void AddNode_duplicate_instance_id_is_rejected()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        var instanceId = Guid.NewGuid();
        document.AddNode(BlurType, 1, new CanvasPosition(0, 0), instanceId);

        Should.Throw<InvalidOperationException>(
            () => document.AddNode(SourceType, 1, new CanvasPosition(0, 0), instanceId));
    }

    [Fact]
    public void MoveNode_valid_position_does_not_increment_revision()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance node = document.AddNode(BlurType, 1, new CanvasPosition(0, 0));
        long revisionAfterAdd = document.Revision;

        document.MoveNode(node.InstanceId, new CanvasPosition(40, 80));

        document.Revision.ShouldBe(revisionAfterAdd);
        document.GetNode(node.InstanceId).Position.ShouldBe(new CanvasPosition(40, 80));
    }

    [Fact]
    public void SetNodeParameter_valid_value_increments_revision()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance node = document.AddNode(BlurType, 1, new CanvasPosition(0, 0));
        long revisionAfterAdd = document.Revision;

        document.SetNodeParameter(node.InstanceId, "kernelSize", 5);

        document.Revision.ShouldBe(revisionAfterAdd + 1);
        document.GetNode(node.InstanceId).Parameters["kernelSize"].ShouldBe(5);
    }

    [Fact]
    public void AddConnection_valid_nodes_adds_edge()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance target = document.AddNode(BlurType, 1, new CanvasPosition(200, 0));

        WorkflowConnection connection = document.AddConnection(
            source.InstanceId,
            "image",
            target.InstanceId,
            "image");

        document.Connections.ShouldHaveSingleItem().ShouldBe(connection);
        connection.SourcePortId.ShouldBe("image");
    }

    [Fact]
    public void AddConnection_unknown_node_is_rejected()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance target = document.AddNode(BlurType, 1, new CanvasPosition(200, 0));

        Should.Throw<KeyNotFoundException>(
            () => document.AddConnection(Guid.NewGuid(), "image", target.InstanceId, "image"));
    }

    [Fact]
    public void RemoveNode_with_attached_connections_removes_them()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance target = document.AddNode(BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", target.InstanceId, "image");

        document.RemoveNode(source.InstanceId);

        document.Nodes.Count.ShouldBe(1);
        document.Connections.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveNode_unknown_instance_throws()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");

        Should.Throw<KeyNotFoundException>(() => document.RemoveNode(Guid.NewGuid()));
    }

    [Fact]
    public void RemoveConnection_unknown_connection_throws()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");

        Should.Throw<KeyNotFoundException>(() => document.RemoveConnection(Guid.NewGuid()));
    }

    [Fact]
    public void PreserveExtension_unknown_field_is_kept_without_revision_change()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        long revision = document.Revision;

        document.PreserveExtension("futureField", """{"a":1}""");

        document.Revision.ShouldBe(revision);
        document.ExtensionData["futureField"].ShouldBe("""{"a":1}""");
    }

    [Fact]
    public void Clone_mutating_copy_does_not_affect_original()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance node = document.AddNode(BlurType, 1, new CanvasPosition(0, 0));
        document.SetNodeParameter(node.InstanceId, "kernelSize", 5);
        document.PreserveExtension("futureField", """{"a":1}""");

        WorkflowDocument clone = document.Clone();
        clone.MoveNode(node.InstanceId, new CanvasPosition(99, 99));
        clone.SetNodeParameter(node.InstanceId, "kernelSize", 9);
        clone.RemoveNode(node.InstanceId);

        document.GetNode(node.InstanceId).Position.ShouldBe(new CanvasPosition(0, 0));
        document.GetNode(node.InstanceId).Parameters["kernelSize"].ShouldBe(5);
        document.Nodes.Count.ShouldBe(1);
        document.ExtensionData["futureField"].ShouldBe("""{"a":1}""");
    }

    [Fact]
    public void Restore_stored_revision_is_preserved()
    {
        var createdUtc = new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.Zero);

        WorkflowDocument document = WorkflowDocument.Restore(Guid.NewGuid(), "loaded", 7, createdUtc);

        document.Revision.ShouldBe(7);
        document.CreatedUtc.ShouldBe(createdUtc);
    }

    [Fact]
    public void SetNodeEnabled_changed_state_increments_revision()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance node = document.AddNode(BlurType, 1, new CanvasPosition(0, 0));
        long revision = document.Revision;

        document.SetNodeEnabled(node.InstanceId, false);

        document.Revision.ShouldBe(revision + 1);
        document.GetNode(node.InstanceId).IsEnabled.ShouldBeFalse();
    }
}
