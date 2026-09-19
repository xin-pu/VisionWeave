using VisionWeave.Contracts.Ports;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// The closed hierarchy of values that may travel along a typed port.
/// </summary>
public abstract record PortValue
{
    /// <summary>
    /// Gets the identifier of the port type this value satisfies.
    /// </summary>
    public abstract PortTypeId PortTypeId { get; }
}
