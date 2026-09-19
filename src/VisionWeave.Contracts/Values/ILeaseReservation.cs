namespace VisionWeave.Contracts.Values;

/// <summary>
/// Represents one consumer's claim on an image frame lease. Releasing the
/// reservation is the only action a consumer takes; the lease itself is owned by
/// the runtime that published it.
/// </summary>
public interface ILeaseReservation : IDisposable
{
}
