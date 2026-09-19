namespace VisionWeave.Contracts.Ports;

/// <summary>
/// Describes how many connections a port accepts.
/// </summary>
public enum PortMultiplicity
{
    /// <summary>The port accepts at most one connection.</summary>
    Single,

    /// <summary>
    /// The port accepts several fan-in connections, collected in connection
    /// order.
    /// </summary>
    Many,
}
