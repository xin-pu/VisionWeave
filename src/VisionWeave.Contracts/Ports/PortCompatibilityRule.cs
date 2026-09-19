namespace VisionWeave.Contracts.Ports;

/// <summary>
/// Declares that a value of the source port type may be assigned to the target
/// port type without an explicit conversion node.
/// </summary>
/// <param name="Source">The port type produced by the upstream node.</param>
/// <param name="Target">The port type accepted by the downstream node.</param>
public readonly record struct PortCompatibilityRule(PortTypeId Source, PortTypeId Target);
