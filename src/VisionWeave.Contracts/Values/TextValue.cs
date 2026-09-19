using VisionWeave.Contracts.Ports;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// Carries textual output, such as recognized text or a formatted measurement.
/// </summary>
/// <param name="Value">The text value.</param>
public sealed record TextValue(string Value) : PortValue
{
    /// <inheritdoc />
    public override PortTypeId PortTypeId => BuiltInPortTypeIds.Text;
}
