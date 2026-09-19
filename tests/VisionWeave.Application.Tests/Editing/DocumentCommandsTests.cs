using Shouldly;
using VisionWeave.Application.Editing;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Editing;

/// <summary>
/// Covers each command's own pair of operations: the edit it applies and the
/// reversal it owns the data for. The history's grouping of those edits is
/// covered separately.
/// </summary>
public sealed class DocumentCommandsTests
{
    private readonly WorkflowDocument _document = WorkflowDocument.Create("workflow");

    [Fact]
    public void AddNode_apply_places_the_instance_and_revert_removes_it()
    {
        var command = new AddNodeCommand(TestNodes.BlurType, 1, new CanvasPosition(10, 20));

        command.Apply(_document).IsAccepted.ShouldBeTrue();

        Guid placed = command.InstanceId!.Value;
        _document.Nodes.ShouldHaveSingleItem();
        _document.GetNode(placed).Position.ShouldBe(new CanvasPosition(10, 20));

        command.Revert(_document);

        _document.Nodes.ShouldBeEmpty();
        _document.Connections.ShouldBeEmpty();
    }

    [Fact]
    public void AddNode_revert_then_apply_keeps_the_same_identifier()
    {
        var command = new AddNodeCommand(TestNodes.SourceType, 1, new CanvasPosition(0, 0));

        command.Apply(_document);
        Guid placed = command.InstanceId!.Value;
        command.Revert(_document);
        command.Apply(_document);

        _document.Nodes.ShouldHaveSingleItem().InstanceId.ShouldBe(placed);
    }

    [Fact]
    public void RemoveNode_apply_removes_the_attached_connections()
    {
        NodeInstance source = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        _document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        var command = new RemoveNodeCommand(blur.InstanceId);
        command.Apply(_document).IsAccepted.ShouldBeTrue();

        _document.Nodes.ShouldHaveSingleItem().InstanceId.ShouldBe(source.InstanceId);
        _document.Connections.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveNode_revert_restores_position_label_value_and_connection()
    {
        NodeInstance source = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 40));
        WorkflowConnection connection = _document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");
        _document.SetNodeLabel(blur.InstanceId, "Smooth");
        _document.SetNodeParameter(blur.InstanceId, "kernelSize", 7);

        var command = new RemoveNodeCommand(blur.InstanceId);
        command.Apply(_document);
        command.Revert(_document);

        NodeInstance restored = _document.GetNode(blur.InstanceId);
        restored.Position.ShouldBe(new CanvasPosition(200, 40));
        restored.Label.ShouldBe("Smooth");
        restored.Parameters["kernelSize"].ShouldBe(7);
        _document.Connections.ShouldHaveSingleItem().ConnectionId.ShouldBe(connection.ConnectionId);
    }

    [Fact]
    public void RemoveNode_unknown_instance_throws_for_the_history_to_report()
    {
        var command = new RemoveNodeCommand(Guid.NewGuid());

        Should.Throw<KeyNotFoundException>(() => command.Apply(_document));
    }

    [Fact]
    public void MoveNodes_apply_moves_every_named_instance()
    {
        NodeInstance first = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance second = _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(100, 0));

        var command = new MoveNodesCommand(
        [
            (first.InstanceId, new CanvasPosition(30, 30)),
            (second.InstanceId, new CanvasPosition(300, 30)),
        ]);

        command.Apply(_document).IsAccepted.ShouldBeTrue();

        first.Position.ShouldBe(new CanvasPosition(30, 30));
        second.Position.ShouldBe(new CanvasPosition(300, 30));
    }

    [Fact]
    public void MoveNodes_revert_restores_each_captured_position()
    {
        NodeInstance first = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance second = _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(100, 0));

        var command = new MoveNodesCommand(
        [
            (first.InstanceId, new CanvasPosition(30, 30)),
            (second.InstanceId, new CanvasPosition(300, 30)),
        ]);

        command.Apply(_document);
        command.Revert(_document);

        first.Position.ShouldBe(new CanvasPosition(0, 0));
        second.Position.ShouldBe(new CanvasPosition(100, 0));
    }

    [Fact]
    public void SetNodeParameter_revert_restores_the_previous_value()
    {
        NodeInstance blur = _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(0, 0));
        _document.SetNodeParameter(blur.InstanceId, "kernelSize", 3);

        var command = new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 11);
        command.Apply(_document);

        blur.Parameters["kernelSize"].ShouldBe(11);

        command.Revert(_document);

        blur.Parameters["kernelSize"].ShouldBe(3);
    }

    [Fact]
    public void SetNodeParameter_revert_clears_a_value_that_was_unset()
    {
        NodeInstance blur = _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(0, 0));

        var command = new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 11);
        command.Apply(_document);
        command.Revert(_document);

        blur.Parameters.ShouldNotContainKey("kernelSize");
    }

    [Fact]
    public void ConnectPorts_apply_adds_a_legal_connection()
    {
        NodeInstance source = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));

        var command = new ConnectPortsCommand(Validator(), source.InstanceId, "image", blur.InstanceId, "image");

        command.Apply(_document).IsAccepted.ShouldBeTrue();

        WorkflowConnection connection = _document.Connections.ShouldHaveSingleItem();
        connection.ConnectionId.ShouldBe(command.ConnectionId!.Value);
        connection.SourceNodeId.ShouldBe(source.InstanceId);
        connection.TargetNodeId.ShouldBe(blur.InstanceId);
    }

    [Fact]
    public void ConnectPorts_output_as_target_is_refused_with_the_port_diagnostic()
    {
        NodeInstance source = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));

        var command = new ConnectPortsCommand(Validator(), source.InstanceId, "image", blur.InstanceId, "blurred");

        DocumentCommandResult result = command.Apply(_document);

        result.IsAccepted.ShouldBeFalse();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.IncompatiblePort);
        _document.Connections.ShouldBeEmpty();
    }

    [Fact]
    public void ConnectPorts_second_wire_into_a_single_input_is_refused()
    {
        NodeInstance first = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance second = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 100));
        NodeInstance blur = _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        _document.AddConnection(first.InstanceId, "image", blur.InstanceId, "image");

        var command = new ConnectPortsCommand(Validator(), second.InstanceId, "image", blur.InstanceId, "image");

        DocumentCommandResult result = command.Apply(_document);

        result.IsAccepted.ShouldBeFalse();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.IncompatiblePort);
        _document.Connections.ShouldHaveSingleItem().SourceNodeId.ShouldBe(first.InstanceId);
    }

    [Fact]
    public void ConnectPorts_wire_that_would_close_a_cycle_is_refused_with_the_path()
    {
        NodeInstance first = _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(0, 0));
        NodeInstance second = _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        _document.AddConnection(first.InstanceId, "blurred", second.InstanceId, "image");

        var command = new ConnectPortsCommand(Validator(), second.InstanceId, "blurred", first.InstanceId, "image");

        DocumentCommandResult result = command.Apply(_document);

        result.IsAccepted.ShouldBeFalse();
        NodeDiagnostic diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe(DiagnosticCodes.InvalidGraph);
        diagnostic.Message.ShouldContain("closes a cycle");
        diagnostic.Message.ShouldContain(first.InstanceId.ToString());
        _document.Connections.ShouldHaveSingleItem().SourceNodeId.ShouldBe(first.InstanceId);
    }

    [Fact]
    public void ConnectPorts_unknown_endpoint_is_refused_with_the_graph_diagnostic()
    {
        NodeInstance source = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));

        var command = new ConnectPortsCommand(Validator(), source.InstanceId, "image", Guid.NewGuid(), "image");

        DocumentCommandResult result = command.Apply(_document);

        result.IsAccepted.ShouldBeFalse();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.InvalidGraph);
        _document.Connections.ShouldBeEmpty();
    }

    [Fact]
    public void ConnectPorts_revert_removes_the_connection_it_added()
    {
        NodeInstance source = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));

        var command = new ConnectPortsCommand(Validator(), source.InstanceId, "image", blur.InstanceId, "image");
        command.Apply(_document);
        command.Revert(_document);

        _document.Connections.ShouldBeEmpty();
        _document.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void DisconnectPorts_apply_removes_and_revert_restores_the_identifier()
    {
        NodeInstance source = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        WorkflowConnection connection = _document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        var command = new DisconnectPortsCommand(connection.ConnectionId);
        command.Apply(_document);

        _document.Connections.ShouldBeEmpty();

        command.Revert(_document);

        _document.Connections.ShouldHaveSingleItem().ShouldBe(connection);
    }

    [Fact]
    public void DisconnectPorts_unknown_connection_throws_for_the_history_to_report()
    {
        var command = new DisconnectPortsCommand(Guid.NewGuid());

        Should.Throw<KeyNotFoundException>(() => command.Apply(_document));
    }

    private static WorkflowValidator Validator() => new(TestNodes.DefaultCatalog());
}
