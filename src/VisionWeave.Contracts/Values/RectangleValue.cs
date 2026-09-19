using System.Drawing;
using VisionWeave.Contracts.Ports;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// Carries a single axis-aligned rectangle.
/// </summary>
/// <param name="Rectangle">The rectangle to hand to the next node.</param>
public sealed record RectangleValue(Rectangle Rectangle) : PortValue
{
    /// <inheritdoc />
    public override PortTypeId PortTypeId => BuiltInPortTypeIds.Rectangle;
}
