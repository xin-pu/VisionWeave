namespace VisionWeave.Contracts.Diagnostics;

/// <summary>
/// A connection of the document, named by its connection identifier. A
/// connection is attributed to the document rather than to one node, because it
/// belongs to two, so it is the target for a condition the wire itself causes:
/// one that names a node the document does not contain, one whose ends are the
/// same node, or one whose ports cannot carry each other's data type.
/// </summary>
/// <param name="ConnectionId">The connection identifier.</param>
public sealed record ConnectionTarget(Guid ConnectionId) : DiagnosticTarget;
