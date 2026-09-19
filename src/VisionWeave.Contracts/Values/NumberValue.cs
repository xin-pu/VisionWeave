using VisionWeave.Contracts.Ports;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// Carries a single numeric measurement or parameter value.
/// </summary>
/// <param name="Value">The numeric value.</param>
public sealed record NumberValue(double Value) : PortValue
{
    /// <inheritdoc />
    public override PortTypeId PortTypeId => BuiltInPortTypeIds.Number;
}
