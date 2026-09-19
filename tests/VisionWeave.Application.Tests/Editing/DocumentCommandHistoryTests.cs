using Shouldly;
using VisionWeave.Application.Editing;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Editing;

/// <summary>
/// Covers the undo stack itself: which attempts become units, when units merge,
/// and what a caller learns when an attempt is refused.
/// </summary>
public sealed class DocumentCommandHistoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly TestClock _clock = new(Start);
    private readonly WorkflowDocument _document = WorkflowDocument.Create("workflow");
    private readonly DocumentCommandHistory _history;

    public DocumentCommandHistoryTests()
    {
        _history = new DocumentCommandHistory(_document, timeProvider: _clock);
    }

    [Fact]
    public void Execute_accepted_edit_becomes_one_undo_unit()
    {
        DocumentCommandResult result = _history.Execute(
            new AddNodeCommand(TestNodes.SourceType, 1, new CanvasPosition(0, 0)));

        result.IsAccepted.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
        _document.Nodes.ShouldHaveSingleItem();
        _history.CanUndo.ShouldBeTrue();
        _history.CanRedo.ShouldBeFalse();
    }

    [Fact]
    public void Undo_and_redo_restore_the_document_on_both_sides()
    {
        var command = new AddNodeCommand(TestNodes.BlurType, 1, new CanvasPosition(10, 20));
        _history.Execute(command);
        Guid placed = command.InstanceId!.Value;

        _history.Undo().IsAccepted.ShouldBeTrue();
        _document.Nodes.ShouldBeEmpty();

        _history.Redo().IsAccepted.ShouldBeTrue();
        _document.Nodes.ShouldHaveSingleItem().InstanceId.ShouldBe(placed);
    }

    [Fact]
    public void Undo_without_an_applied_edit_reports_an_empty_history()
    {
        DocumentCommandResult result = _history.Undo();

        result.IsAccepted.ShouldBeFalse();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.EmptyHistory);
    }

    [Fact]
    public void Redo_without_an_undone_edit_reports_an_empty_history()
    {
        _history.Execute(new AddNodeCommand(TestNodes.SourceType, 1, new CanvasPosition(0, 0)));

        DocumentCommandResult result = _history.Redo();

        result.IsAccepted.ShouldBeFalse();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.EmptyHistory);
    }

    [Fact]
    public void Execute_after_undo_discards_the_redo_stack()
    {
        _history.Execute(new AddNodeCommand(TestNodes.SourceType, 1, new CanvasPosition(0, 0)));
        _history.Undo();
        _history.CanRedo.ShouldBeTrue();

        _history.Execute(new AddNodeCommand(TestNodes.BlurType, 1, new CanvasPosition(200, 0)));

        _history.CanRedo.ShouldBeFalse();
        _history.Redo().Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.EmptyHistory);
    }

    [Fact]
    public void Execute_edit_naming_a_missing_instance_is_refused_without_becoming_a_unit()
    {
        DocumentCommandResult result = _history.Execute(
            new SetNodeParameterCommand(Guid.NewGuid(), "kernelSize", 5));

        result.IsAccepted.ShouldBeFalse();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.EditRefused);
        _history.CanUndo.ShouldBeFalse();
        _document.Nodes.ShouldBeEmpty();
        _document.Revision.ShouldBe(0);
    }

    [Fact]
    public void Execute_multi_move_naming_a_missing_instance_moves_nothing()
    {
        NodeInstance first = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(100, 0));

        DocumentCommandResult result = _history.Execute(
            new MoveNodesCommand(
            [
                (first.InstanceId, new CanvasPosition(30, 30)),
                (Guid.NewGuid(), new CanvasPosition(300, 300)),
            ]));

        result.IsAccepted.ShouldBeFalse();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.EditRefused);
        first.Position.ShouldBe(new CanvasPosition(0, 0));
        _history.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Execute_parameter_edits_inside_the_window_are_one_undo_unit()
    {
        NodeInstance blur = AddBlur();

        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 5));
        _clock.Advance(TimeSpan.FromMilliseconds(100));
        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 9));
        _clock.Advance(TimeSpan.FromMilliseconds(100));
        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 11));

        blur.Parameters["kernelSize"].ShouldBe(11);

        _history.Undo().IsAccepted.ShouldBeTrue();

        blur.Parameters.ShouldNotContainKey("kernelSize");
        _history.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Execute_parameter_edits_apart_in_time_are_separate_undo_units()
    {
        NodeInstance blur = AddBlur();

        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 5));
        _clock.Advance(TimeSpan.FromSeconds(5));
        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 9));

        _history.Undo().IsAccepted.ShouldBeTrue();
        blur.Parameters["kernelSize"].ShouldBe(5);
        _history.CanUndo.ShouldBeTrue();

        _history.Undo().IsAccepted.ShouldBeTrue();
        blur.Parameters.ShouldNotContainKey("kernelSize");
        _history.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Redo_of_a_coalesced_parameter_edit_restores_the_value_the_unit_ended_on()
    {
        NodeInstance blur = AddBlur();

        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 5));
        _clock.Advance(TimeSpan.FromMilliseconds(100));
        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 9));
        _clock.Advance(TimeSpan.FromMilliseconds(100));
        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 11));

        _history.Undo().IsAccepted.ShouldBeTrue();
        blur.Parameters.ShouldNotContainKey("kernelSize");

        // The merged unit is one edit, so redoing it has to bring back where the user
        // ended rather than the value its first edit happened to set.
        _history.Redo().IsAccepted.ShouldBeTrue();
        blur.Parameters["kernelSize"].ShouldBe(11);

        _history.Undo().IsAccepted.ShouldBeTrue();
        blur.Parameters.ShouldNotContainKey("kernelSize");
    }

    [Fact]
    public void Redo_of_a_coalesced_parameter_edit_restores_the_value_the_unit_replaced()
    {
        NodeInstance blur = AddBlur();

        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 3));
        _clock.Advance(TimeSpan.FromSeconds(5));
        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 7));
        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 11));

        _history.Undo().IsAccepted.ShouldBeTrue();
        blur.Parameters["kernelSize"].ShouldBe(3);

        _history.Redo().IsAccepted.ShouldBeTrue();
        blur.Parameters["kernelSize"].ShouldBe(11);
    }

    [Fact]
    public void Execute_edits_of_two_parameters_are_separate_undo_units()
    {
        NodeInstance blur = AddBlur();

        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 5));
        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "sigmaX", 2d));

        _history.Undo().IsAccepted.ShouldBeTrue();

        blur.Parameters["kernelSize"].ShouldBe(5);
        blur.Parameters.ShouldNotContainKey("sigmaX");
        _history.CanUndo.ShouldBeTrue();
    }

    [Fact]
    public void Execute_edits_of_two_nodes_are_separate_undo_units()
    {
        NodeInstance first = AddBlur();
        NodeInstance second = AddBlur();

        _history.Execute(new SetNodeParameterCommand(first.InstanceId, "kernelSize", 5));
        _history.Execute(new SetNodeParameterCommand(second.InstanceId, "kernelSize", 9));

        _history.Undo().IsAccepted.ShouldBeTrue();

        first.Parameters["kernelSize"].ShouldBe(5);
        second.Parameters.ShouldNotContainKey("kernelSize");
    }

    [Fact]
    public void Execute_coalesced_run_undoes_to_the_value_before_the_run()
    {
        NodeInstance blur = AddBlur();
        _document.SetNodeParameter(blur.InstanceId, "kernelSize", 7);

        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 9));
        _clock.Advance(TimeSpan.FromMilliseconds(50));
        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 21));

        _history.Undo().IsAccepted.ShouldBeTrue();

        blur.Parameters["kernelSize"].ShouldBe(7);
    }

    [Fact]
    public void Execute_semantic_edit_increments_the_revision_and_a_move_does_not()
    {
        NodeInstance blur = AddBlur();
        long afterPlacement = _document.Revision;

        _history.Execute(new SetNodeParameterCommand(blur.InstanceId, "kernelSize", 5));
        long afterParameter = _document.Revision;
        afterParameter.ShouldBe(afterPlacement + 1);

        _history.Execute(new MoveNodesCommand([(blur.InstanceId, new CanvasPosition(40, 40))]));

        _document.Revision.ShouldBe(afterParameter);
    }

    [Fact]
    public void Undo_remove_node_restores_the_node_and_its_connection()
    {
        NodeInstance source = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = AddBlur();
        WorkflowConnection connection = _document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        _history.Execute(new RemoveNodeCommand(blur.InstanceId));
        _document.Nodes.ShouldHaveSingleItem();
        _document.Connections.ShouldBeEmpty();

        _history.Undo().IsAccepted.ShouldBeTrue();

        _document.Nodes.Count.ShouldBe(2);
        _document.GetNode(blur.InstanceId).Position.ShouldBe(new CanvasPosition(100, 0));
        _document.Connections.ShouldHaveSingleItem().ConnectionId.ShouldBe(connection.ConnectionId);
    }

    [Fact]
    public void Undo_of_a_multi_node_delete_brings_the_whole_selection_back_at_once()
    {
        NodeInstance source = _document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = AddBlur();
        WorkflowConnection connection = _document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        _history.Execute(new RemoveNodesCommand([source.InstanceId, blur.InstanceId]));
        _document.Nodes.ShouldBeEmpty();

        _history.Undo().IsAccepted.ShouldBeTrue();

        // Deleting a selection is one gesture, so it is one history step: both nodes
        // and the wire between them return together, and nothing is left for a
        // second undo to reverse.
        _document.Nodes.Count.ShouldBe(2);
        _document.Connections.ShouldHaveSingleItem().ConnectionId.ShouldBe(connection.ConnectionId);
        _history.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Clear_discards_both_directions_of_the_history()
    {
        _history.Execute(new AddNodeCommand(TestNodes.SourceType, 1, new CanvasPosition(0, 0)));
        _history.Undo();

        _history.Clear();

        _history.CanUndo.ShouldBeFalse();
        _history.CanRedo.ShouldBeFalse();
    }

    private NodeInstance AddBlur()
        => _document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(100, 0));
}
