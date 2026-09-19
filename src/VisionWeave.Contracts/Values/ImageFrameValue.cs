using VisionWeave.Contracts.Ports;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// Carries a read-only image frame. The runtime, not the value, owns the lease.
/// </summary>
/// <param name="Lease">The leased frame.</param>
public sealed record ImageFrameValue(ImageFrameLease Lease) : PortValue
{
    /// <inheritdoc />
    public override PortTypeId PortTypeId => BuiltInPortTypeIds.ImageFrame;
}
