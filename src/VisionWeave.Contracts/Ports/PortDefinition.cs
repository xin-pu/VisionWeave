namespace VisionWeave.Contracts.Ports;

/// <summary>
/// Describes one port of a node definition. Port definitions are immutable and
/// owned by the catalog, never by a node instance.
/// </summary>
/// <param name="Id">The stable, node-local port identifier.</param>
/// <param name="Direction">Whether the port receives or publishes a value.</param>
/// <param name="TypeId">The port type the port carries.</param>
/// <param name="Multiplicity">How many connections the port accepts.</param>
/// <param name="IsOptional">Whether the node can execute without the port bound.</param>
/// <param name="DisplayName">The label shown on the canvas.</param>
public sealed record PortDefinition(
    string Id,
    PortDirection Direction,
    PortTypeId TypeId,
    PortMultiplicity Multiplicity,
    bool IsOptional,
    string DisplayName);
