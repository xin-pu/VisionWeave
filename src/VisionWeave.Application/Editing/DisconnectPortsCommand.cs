using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Editing;

/// <summary>
/// Removes a connection. The undo data is the removed connection record, so
/// undoing a disconnect restores the same connection with the same identifier.
/// </summary>
public sealed class DisconnectPortsCommand : IDocumentCommand
{
    private readonly Guid _connectionId;
    private WorkflowConnection? _removed;

    /// <summary>
    /// Initializes the command.
    /// </summary>
    /// <param name="connectionId">The connection to remove.</param>
    public DisconnectPortsCommand(Guid connectionId) => _connectionId = connectionId;

    /// <inheritdoc />
    public DocumentCommandResult Apply(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        WorkflowConnection connection = document.Connections
            .FirstOrDefault(item => item.ConnectionId == _connectionId)
            ?? throw new KeyNotFoundException($"Connection '{_connectionId}' is not present in the document.");

        document.RemoveConnection(_connectionId);
        _removed = connection;
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

        document.AddConnection(
            _removed.SourceNodeId,
            _removed.SourcePortId,
            _removed.TargetNodeId,
            _removed.TargetPortId,
            _removed.ConnectionId);
    }
}
