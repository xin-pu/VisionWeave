namespace VisionWeave.Domain.Workflows;

/// <summary>
/// A saved edge between an output port and an input port. The connection records
/// identifiers only; whether the pairing is legal is decided by the application
/// validator, so that a loaded document can always be displayed and repaired.
/// </summary>
/// <param name="ConnectionId">The connection identifier, unique inside one document.</param>
/// <param name="SourceNodeId">The node instance that produces the value.</param>
/// <param name="SourcePortId">The output port identifier on the source node.</param>
/// <param name="TargetNodeId">The node instance that consumes the value.</param>
/// <param name="TargetPortId">The input port identifier on the target node.</param>
public sealed record WorkflowConnection(
    Guid ConnectionId,
    Guid SourceNodeId,
    string SourcePortId,
    Guid TargetNodeId,
    string TargetPortId);
