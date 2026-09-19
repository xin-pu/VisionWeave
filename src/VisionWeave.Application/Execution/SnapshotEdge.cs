namespace VisionWeave.Application.Execution;

/// <summary>
/// One validated connection of an executable snapshot. Snapshot edges are kept
/// in document order, so a port that accepts several connections receives its
/// values in the order the connections were declared.
/// </summary>
/// <param name="ConnectionId">The document connection identifier.</param>
/// <param name="SourceNodeId">The node instance that produces the value.</param>
/// <param name="SourcePortId">The output port identifier on the source node.</param>
/// <param name="TargetNodeId">The node instance that consumes the value.</param>
/// <param name="TargetPortId">The input port identifier on the target node.</param>
public sealed record SnapshotEdge(
    Guid ConnectionId,
    Guid SourceNodeId,
    string SourcePortId,
    Guid TargetNodeId,
    string TargetPortId);
