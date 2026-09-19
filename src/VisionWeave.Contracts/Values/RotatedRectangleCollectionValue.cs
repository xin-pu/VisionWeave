using VisionWeave.Contracts.Ports;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// Carries an immutable set of oriented rectangles.
/// </summary>
/// <param name="Rectangles">The oriented rectangles to hand to the next node.</param>
public sealed record RotatedRectangleCollectionValue(RotatedRectangleCollection Rectangles) : PortValue
{
    /// <inheritdoc />
    public override PortTypeId PortTypeId => BuiltInPortTypeIds.RotatedRectangleCollection;
}
