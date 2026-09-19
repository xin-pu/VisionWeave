using VisionWeave.Contracts.Ports;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// Carries an immutable rectangle set.
/// </summary>
/// <param name="Rectangles">The rectangles to hand to the next node.</param>
public sealed record RectangleCollectionValue(RectangleCollection Rectangles) : PortValue
{
    /// <inheritdoc />
    public override PortTypeId PortTypeId => BuiltInPortTypeIds.RectangleCollection;
}
