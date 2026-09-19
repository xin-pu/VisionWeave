using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Editing;

/// <summary>
/// Places a node instance on the canvas. The undo data is the identifier the
/// instance was given, which is what makes the reversal exact: undoing an add
/// removes that instance, and redoing it places the same one back.
/// </summary>
public sealed class AddNodeCommand : IDocumentCommand
{
    private readonly NodeTypeId _nodeTypeId;
    private readonly int _typeVersion;
    private readonly CanvasPosition _position;
    private Guid? _instanceId;

    /// <summary>
    /// Initializes the command.
    /// </summary>
    /// <param name="nodeTypeId">The node type to place.</param>
    /// <param name="typeVersion">The definition version to place.</param>
    /// <param name="position">The canvas position.</param>
    /// <param name="instanceId">An explicit instance identifier, or a new one.</param>
    public AddNodeCommand(
        NodeTypeId nodeTypeId,
        int typeVersion,
        CanvasPosition position,
        Guid? instanceId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(typeVersion);

        _nodeTypeId = nodeTypeId;
        _typeVersion = typeVersion;
        _position = position;
        _instanceId = instanceId;
    }

    /// <summary>
    /// Gets the identifier of the placed instance, once the command has been applied.
    /// </summary>
    public Guid? InstanceId => _instanceId;

    /// <inheritdoc />
    public DocumentCommandResult Apply(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        NodeInstance node = document.AddNode(_nodeTypeId, _typeVersion, _position, _instanceId);
        _instanceId = node.InstanceId;
        return DocumentCommandResult.Accepted;
    }

    /// <inheritdoc />
    public void Revert(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.RemoveNode(_instanceId!.Value);
    }
}
