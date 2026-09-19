using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// The ledger a run is given when a test needs to know the run has let go of
/// everything it took. It forwards to the ledger that does the accounting and
/// completes a task once the last lease and reservation are released, so a test
/// waits for a late executor to drain instead of polling for it.
/// </summary>
internal sealed class SignalledLedger : ILeaseLedger
{
    private readonly ILeaseLedger _ledger;
    private readonly TaskCompletionSource _flat = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Initializes the ledger over the one that accounts for the leases.
    /// </summary>
    /// <param name="ledger">The ledger the counts are read from.</param>
    internal SignalledLedger(ILeaseLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        _ledger = ledger;
    }

    /// <summary>
    /// Gets a task that completes once every lease and reservation created since
    /// the ledger was created has been released.
    /// </summary>
    internal Task Flat => _flat.Task;

    /// <inheritdoc />
    public int Outstanding => _ledger.Outstanding;

    /// <inheritdoc />
    public int ReservationsOutstanding => _ledger.ReservationsOutstanding;

    /// <inheritdoc />
    public long Created => _ledger.Created;

    /// <inheritdoc />
    public long Released => _ledger.Released;

    /// <inheritdoc />
    public void LeaseCreated() => _ledger.LeaseCreated();

    /// <inheritdoc />
    public void LeaseReleased()
    {
        _ledger.LeaseReleased();
        Signal();
    }

    /// <inheritdoc />
    public void ReservationTaken() => _ledger.ReservationTaken();

    /// <inheritdoc />
    public void ReservationReleased()
    {
        _ledger.ReservationReleased();
        Signal();
    }

    private void Signal()
    {
        if (_ledger.Created > 0 && _ledger.Outstanding <= 0 && _ledger.ReservationsOutstanding <= 0)
        {
            _flat.TrySetResult();
        }
    }
}
