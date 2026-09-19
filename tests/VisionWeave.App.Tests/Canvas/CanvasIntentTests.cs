using System.ComponentModel;
using System.Windows;
using Shouldly;
using VisionWeave.App.Canvas;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Editing;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.App.Tests.Canvas;

/// <summary>
/// Covers the gestures the canvas turns into intent: placing, connecting,
/// disconnecting, moving, deleting, and the two history steps. Each one is driven
/// through the view model the markup is bound to, so what is checked is the path a
/// user's gesture actually takes — a command, the document, and the projection the
/// canvas is redrawn from. A refused gesture is covered for what it leaves behind:
/// nothing, because the document never changed.
/// </summary>
public sealed class CanvasIntentTests
{
    private static readonly NodeDefinitionCatalog Catalog =
        NodeDefinitionCatalog.FromProviders([new OpenCvNodeDefinitionProvider()]);

    private readonly EditorSession _session = TestSessions.Create(catalog: Catalog);
    private readonly ShellStatus _status;
    private readonly CanvasViewModel _canvas;

    public CanvasIntentTests()
    {
        _status = new ShellStatus(_session);
        _canvas = new CanvasViewModel(_session, Catalog, new WorkflowValidator(Catalog), _status);
    }

    [Fact]
    public void Add_places_a_catalog_type_in_the_middle_of_what_the_user_sees()
    {
        Show(800, 600);

        _canvas.AddNodeCommand.Execute(OpenCvNodeIds.GaussianBlurTypeId);

        // Centred in the viewport, moved back by roughly half a drawn node so the
        // node lands under the pointer rather than hanging off it.
        _session.Document.Nodes.ShouldHaveSingleItem().Position.ShouldBe(new CanvasPosition(300, 240));
        _canvas.Nodes.ShouldHaveSingleItem().DisplayName.ShouldBe("Gaussian Blur");
        _canvas.IsEmpty.ShouldBeFalse();
        _session.CanUndo.ShouldBeTrue();
    }

    [Fact]
    public void Add_steps_aside_so_a_second_node_does_not_hide_the_first()
    {
        Show(800, 600);

        _canvas.AddNodeCommand.Execute(OpenCvNodeIds.GaussianBlurTypeId);
        _canvas.AddNodeCommand.Execute(OpenCvNodeIds.GaussianBlurTypeId);

        _canvas.Nodes.Count.ShouldBe(2);
        _canvas.Nodes[0].Position.ShouldBe(new CanvasPosition(300, 240));
        _canvas.Nodes[1].Position.ShouldBe(new CanvasPosition(332, 240));
    }

    [Fact]
    public void Add_before_the_editor_has_reported_a_viewport_starts_at_the_origin()
    {
        _canvas.AddNodeCommand.Execute(OpenCvNodeIds.GaussianBlurTypeId);

        _session.Document.Nodes.ShouldHaveSingleItem().Position.ShouldBe(new CanvasPosition(0, 0));
    }

    [Fact]
    public void Add_of_a_type_this_build_does_not_hold_is_reported_and_changes_nothing()
    {
        long revision = _session.Document.Revision;

        _canvas.AddNodeCommand.Execute("visionweave.missing.enhance");

        _canvas.Nodes.ShouldBeEmpty();
        _session.Document.Nodes.ShouldBeEmpty();
        _session.Document.Revision.ShouldBe(revision);
        _session.CanUndo.ShouldBeFalse();
        _status.ConditionSeverity.ShouldBe(DiagnosticSeverity.Error);
        _status.Condition.ShouldContain(DiagnosticCodes.MissingNodeDefinition);
    }

    [Fact]
    public void Connect_joins_the_output_a_drag_left_to_the_input_it_reached()
    {
        Guid blur = Add(OpenCvNodeIds.GaussianBlurTypeId, 0, 0);
        Guid resize = Add(OpenCvNodeIds.ResizeTypeId, 300, 0);

        _canvas.ConnectCommand.Execute(Dragged(Port(blur, OpenCvNodeIds.BlurredPortId), Port(resize, OpenCvNodeIds.ImagePortId)));

        WorkflowConnection connection = _session.Document.Connections.ShouldHaveSingleItem();
        connection.SourceNodeId.ShouldBe(blur);
        connection.TargetNodeId.ShouldBe(resize);
        _canvas.Connectors.ShouldHaveSingleItem().Target.NodeInstanceId.ShouldBe(resize);
    }

    [Fact]
    public void Connect_turns_a_drag_that_began_at_an_input_around()
    {
        Guid blur = Add(OpenCvNodeIds.GaussianBlurTypeId, 0, 0);
        Guid resize = Add(OpenCvNodeIds.ResizeTypeId, 300, 0);

        // The drag started at the input, which is what a user dragging right to left
        // produces. The document's convention is that a wire leaves an output, so the
        // gesture is turned around before the document sees it.
        _canvas.ConnectCommand.Execute(Dragged(Port(resize, OpenCvNodeIds.ImagePortId), Port(blur, OpenCvNodeIds.BlurredPortId)));

        WorkflowConnection connection = _session.Document.Connections.ShouldHaveSingleItem();
        connection.SourceNodeId.ShouldBe(blur);
        connection.SourcePortId.ShouldBe(OpenCvNodeIds.BlurredPortId);
        connection.TargetNodeId.ShouldBe(resize);
        connection.TargetPortId.ShouldBe(OpenCvNodeIds.ImagePortId);
    }

    [Fact]
    public void Connect_that_the_validator_refuses_leaves_the_canvas_exactly_as_it_was()
    {
        Guid blur = Add(OpenCvNodeIds.GaussianBlurTypeId, 0, 0);
        Guid resize = Add(OpenCvNodeIds.ResizeTypeId, 300, 0);
        long revision = _session.Document.Revision;

        // Two inputs cannot be joined. The canvas drew the pending connection while
        // the drag was in progress; because the refused edit never changed the
        // document, there is nothing to undo and nothing to roll back — the surface
        // is still the projection the drag started from.
        _canvas.ConnectCommand.Execute(Dragged(Port(blur, OpenCvNodeIds.ImagePortId), Port(resize, OpenCvNodeIds.ImagePortId)));

        _session.Document.Connections.ShouldBeEmpty();
        _session.Document.Revision.ShouldBe(revision);
        _canvas.Connectors.ShouldBeEmpty();
        _canvas.Nodes.Count.ShouldBe(2);
        _status.ConditionSeverity.ShouldBe(DiagnosticSeverity.Error);
        _status.Condition.ShouldContain(DiagnosticCodes.IncompatiblePort);

        // The refusal left no step behind: the edit an undo reverses is still the one
        // that placed the second node.
        _canvas.UndoCommand.Execute(null);

        _canvas.Nodes.ShouldHaveSingleItem().InstanceId.ShouldBe(blur);
    }

    [Fact]
    public void Connect_of_something_that_is_not_two_ports_is_not_a_gesture()
    {
        _canvas.ConnectCommand.Execute(Tuple.Create<object, object>("a port", "another port"));

        _session.Document.Connections.ShouldBeEmpty();
        _session.CanUndo.ShouldBeFalse();

        // Nothing was observed, so the status area still says what it said before.
        _status.Condition.ShouldBe(ShellStatus.NoConditionText);
        _status.ConditionSeverity.ShouldBeNull();
    }

    [Fact]
    public void Disconnect_removes_the_wire_the_gesture_named()
    {
        Guid blur = Add(OpenCvNodeIds.GaussianBlurTypeId, 0, 0);
        Guid resize = Add(OpenCvNodeIds.ResizeTypeId, 300, 0);
        Connect(blur, OpenCvNodeIds.BlurredPortId, resize, OpenCvNodeIds.ImagePortId);

        _canvas.DisconnectCommand.Execute(_canvas.Connectors.ShouldHaveSingleItem());

        _session.Document.Connections.ShouldBeEmpty();
        _canvas.Connectors.ShouldBeEmpty();
        _session.CanUndo.ShouldBeTrue();
    }

    [Fact]
    public void Delete_removes_the_selection_and_one_undo_brings_all_of_it_back()
    {
        Guid blur = Add(OpenCvNodeIds.GaussianBlurTypeId, 0, 0);
        Guid resize = Add(OpenCvNodeIds.ResizeTypeId, 300, 0);
        Connect(blur, OpenCvNodeIds.BlurredPortId, resize, OpenCvNodeIds.ImagePortId);
        _session.Select([blur, resize]);

        _canvas.DeleteSelectionCommand.CanExecute(null).ShouldBeTrue();
        _canvas.DeleteSelectionCommand.Execute(null);

        _canvas.Nodes.ShouldBeEmpty();
        _canvas.IsEmpty.ShouldBeTrue();
        _session.Document.Connections.ShouldBeEmpty();

        // Deleting a selection is one gesture, so one undo restores the nodes and the
        // wire between them and leaves nothing else to reverse.
        _canvas.UndoCommand.Execute(null);

        _canvas.Nodes.Count.ShouldBe(2);
        _canvas.Connectors.ShouldHaveSingleItem();
        _session.CanRedo.ShouldBeTrue();
    }

    [Fact]
    public void Delete_with_nothing_selected_is_not_an_edit_the_user_can_make()
    {
        Add(OpenCvNodeIds.GaussianBlurTypeId, 0, 0);

        // A disabled command cannot be executed, so a delete with an empty selection
        // never becomes a history step with nothing in it.
        _canvas.DeleteSelectionCommand.CanExecute(null).ShouldBeFalse();
        _canvas.Nodes.ShouldHaveSingleItem();
    }

    [Fact]
    public void Move_commits_the_position_a_drag_left_at_whole_graph_space_units()
    {
        Guid blur = Add(OpenCvNodeIds.GaussianBlurTypeId, 0, 0);
        _canvas.Nodes.ShouldHaveSingleItem().Location = new Point(300.4, 240.6);

        _canvas.MoveCommand.Execute(null);

        // The pointer's sub-pixel noise is quantized rather than written into the
        // document, so the same drag produces the same file.
        _session.Document.GetNode(blur).Position.ShouldBe(new CanvasPosition(300, 241));
        _canvas.Nodes.ShouldHaveSingleItem().Position.ShouldBe(new CanvasPosition(300, 241));
    }

    [Fact]
    public void Move_of_a_drag_that_ended_where_it_started_is_not_an_edit()
    {
        Add(OpenCvNodeIds.GaussianBlurTypeId, 0, 0);
        Add(OpenCvNodeIds.ResizeTypeId, 300, 0);
        _session.Undo();
        long revision = _session.Document.Revision;

        // The container reports a completed drag whether or not the node was moved,
        // and a node that ended where it started is not a change to commit: the edit
        // the user made before it is still the one redo would reapply.
        _canvas.MoveCommand.Execute(null);

        _session.Document.Revision.ShouldBe(revision);
        _session.CanRedo.ShouldBeTrue();
    }

    [Fact]
    public void Selection_a_container_writes_becomes_the_session_selection_and_back()
    {
        Guid blur = Add(OpenCvNodeIds.GaussianBlurTypeId, 0, 0);
        Guid resize = Add(OpenCvNodeIds.ResizeTypeId, 300, 0);
        _session.Select([]);

        // What a container writes when the user clicks it.
        Node(resize).IsSelected = true;

        _session.Selection.ShouldBe([resize]);
        _canvas.DeleteSelectionCommand.CanExecute(null).ShouldBeTrue();

        // What a command elsewhere in the shell does.
        _session.Select([blur]);

        Node(blur).IsSelected.ShouldBeTrue();
        Node(resize).IsSelected.ShouldBeFalse();
    }

    [Fact]
    public void Undo_and_redo_redraw_the_canvas_from_what_the_document_holds()
    {
        List<string?> raised = [];
        _canvas.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        _canvas.UndoCommand.CanExecute(null).ShouldBeFalse();
        _canvas.RedoCommand.CanExecute(null).ShouldBeFalse();

        _canvas.AddNodeCommand.Execute(OpenCvNodeIds.GaussianBlurTypeId);

        _canvas.Nodes.ShouldHaveSingleItem();
        _canvas.UndoCommand.CanExecute(null).ShouldBeTrue();
        raised.ShouldContain(nameof(CanvasViewModel.Nodes));
        raised.ShouldContain(nameof(CanvasViewModel.IsEmpty));

        _canvas.UndoCommand.Execute(null);

        _canvas.Nodes.ShouldBeEmpty();
        _canvas.IsEmpty.ShouldBeTrue();
        _canvas.UndoCommand.CanExecute(null).ShouldBeFalse();
        _canvas.RedoCommand.CanExecute(null).ShouldBeTrue();

        _canvas.RedoCommand.Execute(null);

        _canvas.Nodes.ShouldHaveSingleItem();
        _canvas.RedoCommand.CanExecute(null).ShouldBeFalse();
    }

    private void Show(double width, double height)
    {
        _canvas.ViewportLocation = new Point(0, 0);
        _canvas.ViewportSize = new Size(width, height);
    }

    private Guid Add(string typeId, double x, double y)
    {
        var nodeTypeId = new NodeTypeId(typeId);
        Catalog.TryResolveLatest(nodeTypeId, out NodeDefinition? definition).ShouldBeTrue();

        var command = new AddNodeCommand(nodeTypeId, definition!.TypeVersion, new CanvasPosition(x, y));
        _session.Execute(command).IsAccepted.ShouldBeTrue();

        return command.InstanceId!.Value;
    }

    private void Connect(Guid sourceNodeId, string sourcePortId, Guid targetNodeId, string targetPortId)
        => _canvas.ConnectCommand
            .Execute(Dragged(Port(sourceNodeId, sourcePortId), Port(targetNodeId, targetPortId)));

    /// <summary>
    /// Finds the port presentation the canvas draws, which is what a drag hands over.
    /// </summary>
    /// <param name="instanceId">The node instance the port belongs to.</param>
    /// <param name="portId">The port identifier.</param>
    /// <returns>The port presentation.</returns>
    private PortViewModel Port(Guid instanceId, string portId)
        => Node(instanceId).Inputs
            .Concat(Node(instanceId).Outputs)
            .Single(port => port.PortId == portId);

    private WorkflowNodeViewModel Node(Guid instanceId)
        => _canvas.Nodes.Single(node => node.InstanceId == instanceId);

    private static Tuple<object, object> Dragged(PortViewModel from, PortViewModel to)
        => Tuple.Create<object, object>(from, to);
}
