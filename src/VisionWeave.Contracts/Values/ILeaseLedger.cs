namespace VisionWeave.Contracts.Values;

/// <summary>
/// Observes native lease lifetime so that leaks can be reported as diagnostics
/// and asserted in tests.
/// </summary>
public interface ILeaseLedger
{
    /// <summary>
    /// Gets the number of leases that have been created but not yet released.
    /// </summary>
    int Outstanding { get; }

    /// <summary>
    /// Records that a lease was created.
    /// </summary>
    void LeaseCreated();

    /// <summary>
    /// Records that a consumer reservation was taken on a lease.
    /// </summary>
    void LeaseReserved();

    /// <summary>
    /// Records that a reservation or a lease release completed.
    /// </summary>
    void LeaseReleased();
}
