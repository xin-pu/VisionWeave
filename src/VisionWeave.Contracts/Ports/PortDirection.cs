namespace VisionWeave.Contracts.Ports;

/// <summary>
/// The direction of a port relative to its node.
/// </summary>
public enum PortDirection
{
    /// <summary>The port receives a value produced elsewhere.</summary>
    Input,

    /// <summary>The port publishes a value produced by the node.</summary>
    Output,
}
