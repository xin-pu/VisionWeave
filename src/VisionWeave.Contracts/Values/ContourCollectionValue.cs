using VisionWeave.Contracts.Ports;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// Carries an immutable contour set.
/// </summary>
/// <param name="Contours">The contours to hand to the next node.</param>
public sealed record ContourCollectionValue(ContourCollection Contours) : PortValue
{
    /// <inheritdoc />
    public override PortTypeId PortTypeId => BuiltInPortTypeIds.ContourCollection;
}
