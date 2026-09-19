namespace VisionWeave.Contracts.Values;

/// <summary>
/// Observes native lease lifetime so that leaks can be reported as diagnostics
/// and asserted in tests. A lease that was created must be released exactly once,
/// and every reservation taken on a lease must be released before the lease
/// itself, which is what makes a non-zero outstanding count a defect.
/// </summary>
public interface ILeaseLedger
{
    /// <summary>
    /// Gets the number of leases that have been created but not yet released.
    /// </summary>
    int Outstanding { get; }

    /// <summary>
    /// Gets the number of reservations that have been taken but not yet
    /// released.
    /// </summary>
    int ReservationsOutstanding { get; }

    /// <summary>
    /// Gets the number of leases created since the ledger was created.
    /// </summary>
    long Created { get; }

    /// <summary>
    /// Gets the number of leases released since the ledger was created.
    /// </summary>
    long Released { get; }

    /// <summary>
    /// Records that a lease was created.
    /// </summary>
    void LeaseCreated();

    /// <summary>
    /// Records that a lease was released.
    /// </summary>
    void LeaseReleased();

    /// <summary>
    /// Records that a consumer reservation was taken on a lease.
    /// </summary>
    void ReservationTaken();

    /// <summary>
    /// Records that a consumer reservation was released.
    /// </summary>
    void ReservationReleased();
}
