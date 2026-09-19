using VisionWeave.Contracts.Ports;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// Carries a boolean measurement or decision result.
/// </summary>
/// <param name="Value">The boolean value.</param>
public sealed record BooleanValue(bool Value) : PortValue
{
    /// <inheritdoc />
    public override PortTypeId PortTypeId => BuiltInPortTypeIds.Boolean;
}
