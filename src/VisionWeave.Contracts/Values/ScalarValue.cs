using VisionWeave.Contracts.Ports;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// Carries up to four scalar components, such as a color.
/// </summary>
/// <param name="Components">The scalar components.</param>
public sealed record ScalarValue(ScalarComponents Components) : PortValue
{
    /// <inheritdoc />
    public override PortTypeId PortTypeId => BuiltInPortTypeIds.Scalar;
}
