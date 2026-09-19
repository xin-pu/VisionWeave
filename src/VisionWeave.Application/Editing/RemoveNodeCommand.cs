using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Editing;

/// <summary>
/// Removes a node instance and the connections attached to it. The undo data is
/// what the removal took away — the instance itself, with its position, label,
/// enabled flag, saved values, and preserved fields, plus the connection records —
/// so undoing a delete restores the node exactly as it was, wires included.
/// </summary>
public sealed class RemoveNodeCommand : IDocumentCommand
{
    private readonly Guid _instanceId;
    private NodeInstance? _removed;
    private List<WorkflowConnection> _connections = [];

    /// <summary>
    /// Initializes the command.
    /// </summary>
    /// <param name="instanceId">The instance to remove.</param>
    public RemoveNodeCommand(Guid instanceId) => _instanceId = instanceId;

    /// <inheritdoc />
    public DocumentCommandResult Apply(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Captured before the removal, and captured again on a redo, when the
        // reversal has put the same node and connections back in place.
        _connections =
        [
            .. document.Connections.Where(connection =>
                connection.SourceNodeId == _instanceId || connection.TargetNodeId == _instanceId),
        ];

        _removed = document.RemoveNode(_instanceId);
        return DocumentCommandResult.Accepted;
    }

    /// <inheritdoc />
    public void Revert(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (_removed is null)
        {
            throw new InvalidOperationException("The command cannot be reverted before it has been applied.");
        }

        // The node comes back first, because a connection refers to it.
        document.RestoreNode(_removed);

        foreach (WorkflowConnection connection in _connections)
        {
            document.AddConnection(
                connection.SourceNodeId,
                connection.SourcePortId,
                connection.TargetNodeId,
                connection.TargetPortId,
                connection.ConnectionId);
        }
    }
}
