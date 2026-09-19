using VisionWeave.Contracts.Ports;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// Carries an immutable point set.
/// </summary>
/// <param name="Points">The points to hand to the next node.</param>
public sealed record PointCollectionValue(PointCollection Points) : PortValue
{
    /// <inheritdoc />
    public override PortTypeId PortTypeId => BuiltInPortTypeIds.PointCollection;
}
