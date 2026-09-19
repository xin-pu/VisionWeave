namespace VisionWeave.Contracts.Diagnostics;

/// <summary>
/// A port of a node instance, named by the instance that owns it and the port
/// identifier the node definition declares. It is the target for a condition
/// about one port: an unknown port, a port whose direction or data type rejects
/// the wire, or an input port that carries more connections than it accepts.
/// </summary>
/// <param name="NodeInstanceId">The node instance that owns the port.</param>
/// <param name="PortId">The port identifier, as the node definition declares it.</param>
public sealed record PortTarget(Guid NodeInstanceId, string PortId) : DiagnosticTarget;
