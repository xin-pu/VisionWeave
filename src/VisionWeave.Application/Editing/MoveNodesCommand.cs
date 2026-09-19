using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Editing;

/// <summary>
/// Moves one or more node instances. Dragging a selection is one edit and one
/// undo unit, so the command takes every moved node at once and captures the
/// positions they held before they moved.
/// </summary>
public sealed class MoveNodesCommand : IDocumentCommand
{
    private readonly (Guid InstanceId, CanvasPosition Position)[] _moves;
    private (Guid InstanceId, CanvasPosition Position)[]? _previous;

    /// <summary>
    /// Initializes the command.
    /// </summary>
    /// <param name="moves">The target position of each moved instance.</param>
    /// <exception cref="ArgumentException">No instance was named.</exception>
    public MoveNodesCommand(IEnumerable<(Guid InstanceId, CanvasPosition Position)> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);

        _moves = [.. moves];
        if (_moves.Length == 0)
        {
            throw new ArgumentException("A move command needs at least one node.", nameof(moves));
        }
    }

    /// <inheritdoc />
    public DocumentCommandResult Apply(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Every position is read before the first node moves, so an instance the
        // document does not hold refuses the whole edit instead of moving half of it.
        _previous =
        [
            .. _moves.Select(move => (move.InstanceId, document.GetNode(move.InstanceId).Position)),
        ];

        foreach ((Guid instanceId, CanvasPosition position) in _moves)
        {
            document.MoveNode(instanceId, position);
        }

        return DocumentCommandResult.Accepted;
    }

    /// <inheritdoc />
    public void Revert(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (_previous is null)
        {
            throw new InvalidOperationException("The command cannot be reverted before it has been applied.");
        }

        foreach ((Guid instanceId, CanvasPosition position) in _previous)
        {
            document.MoveNode(instanceId, position);
        }
    }
}
