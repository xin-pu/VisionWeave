using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Execution;

/// <summary>
/// One reservation the runtime holds for a published output while something
/// outside the plan still reads it, such as a preview conversion. Releasing the
/// fence is what lets the frame be freed.
/// </summary>
/// <param name="Publication">The publication the reservation belongs to.</param>
/// <param name="Reservation">The reservation to release.</param>
internal sealed record LeaseFence(LeasePublication Publication, ILeaseReservation Reservation);
