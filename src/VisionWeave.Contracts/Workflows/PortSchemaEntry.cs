using VisionWeave.Contracts.Ports;

namespace VisionWeave.Contracts.Workflows;

/// <summary>
/// One port as a document remembered it when the document was saved. A snapshot is
/// a rendering fallback and never a second source of truth (ADR-0004 decision 6):
/// for a known node type the resolved catalog definition wins, and the snapshot is
/// consulted only to render a placeholder node and to explain a saved connection
/// whose port is unknown.
/// </summary>
/// <remarks>
/// The port identifier and the direction identify the port, so a reader requires
/// both; the descriptive members are best effort, because a document may record
/// only part of a port or predate a member.
/// </remarks>
public sealed record PortSchemaEntry
{
    /// <summary>
    /// Creates a remembered port.
    /// </summary>
    /// <param name="portId">The stable, node-local port identifier.</param>
    /// <param name="direction">Whether the port received or published a value.</param>
    /// <param name="typeId">The port type, when the document recorded one.</param>
    /// <param name="multiplicity">How many connections the port accepted, when the document recorded it.</param>
    /// <param name="isOptional">Whether the node could run without the port bound, when the document recorded it.</param>
    /// <param name="displayName">The label the port carried, when the document recorded one.</param>
    /// <exception cref="ArgumentException">The port identifier is empty or whitespace.</exception>
    public PortSchemaEntry(
        string portId,
        PortDirection direction,
        PortTypeId? typeId = null,
        PortMultiplicity? multiplicity = null,
        bool? isOptional = null,
        string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portId);

        PortId = portId;
        Direction = direction;
        TypeId = typeId;
        Multiplicity = multiplicity;
        IsOptional = isOptional;
        DisplayName = displayName;
    }

    /// <summary>Gets the stable, node-local port identifier.</summary>
    public string PortId { get; }

    /// <summary>Gets whether the port received or published a value.</summary>
    public PortDirection Direction { get; }

    /// <summary>Gets the port type, when the document recorded one.</summary>
    public PortTypeId? TypeId { get; }

    /// <summary>Gets how many connections the port accepted, when the document recorded it.</summary>
    public PortMultiplicity? Multiplicity { get; }

    /// <summary>Gets whether the node could run without the port bound, when the document recorded it.</summary>
    public bool? IsOptional { get; }

    /// <summary>Gets the label the port carried, when the document recorded one.</summary>
    public string? DisplayName { get; }
}
