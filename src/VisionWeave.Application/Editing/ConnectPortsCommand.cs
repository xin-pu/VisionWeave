using VisionWeave.Application.Validation;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Editing;

/// <summary>
/// Connects two ports. Legality is decided by <see cref="WorkflowValidator"/>,
/// which is the only place direction, type, multiplicity, and cycle rules live: a
/// refused connection is reported with the validator's diagnostics and leaves the
/// document untouched, so the projection has nothing to roll back.
/// </summary>
public sealed class ConnectPortsCommand : IDocumentCommand
{
    private readonly WorkflowValidator _validator;
    private readonly Guid _sourceNodeId;
    private readonly string _sourcePortId;
    private readonly Guid _targetNodeId;
    private readonly string _targetPortId;
    private Guid? _connectionId;

    /// <summary>
    /// Initializes the command.
    /// </summary>
    /// <param name="validator">The validator that judges the connection.</param>
    /// <param name="sourceNodeId">The producing node instance.</param>
    /// <param name="sourcePortId">The output port identifier.</param>
    /// <param name="targetNodeId">The consuming node instance.</param>
    /// <param name="targetPortId">The input port identifier.</param>
    /// <param name="connectionId">An explicit connection identifier, or a new one.</param>
    public ConnectPortsCommand(
        WorkflowValidator validator,
        Guid sourceNodeId,
        string sourcePortId,
        Guid targetNodeId,
        string targetPortId,
        Guid? connectionId = null)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePortId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPortId);

        _validator = validator;
        _sourceNodeId = sourceNodeId;
        _sourcePortId = sourcePortId;
        _targetNodeId = targetNodeId;
        _targetPortId = targetPortId;
        _connectionId = connectionId;
    }

    /// <summary>
    /// Gets the identifier of the connection, once the command has been applied.
    /// </summary>
    public Guid? ConnectionId => _connectionId;

    /// <inheritdoc />
    public DocumentCommandResult Apply(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _connectionId ??= Guid.NewGuid();

        var candidate = new WorkflowConnection(
            _connectionId.Value,
            _sourceNodeId,
            _sourcePortId,
            _targetNodeId,
            _targetPortId);

        ValidationResult validation = _validator.ValidateConnection(document, candidate);

        if (!validation.IsValid)
        {
            return DocumentCommandResult.Refused(validation.Diagnostics);
        }

        document.AddConnection(_sourceNodeId, _sourcePortId, _targetNodeId, _targetPortId, _connectionId);
        return DocumentCommandResult.Accepted;
    }

    /// <inheritdoc />
    public void Revert(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.RemoveConnection(_connectionId!.Value);
    }
}
