using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// The ledger a run is given when a test needs to know the run has let go of
/// something it took. It forwards to the ledger that does the accounting and
/// completes a task once the last lease and reservation are released, so a test
/// waits for a late executor to drain instead of polling for it, or once a number
/// of reservations have been released, so a test watches one consumer let go of a
/// frame instead of waiting a moment and hoping that it has.
/// </summary>
internal sealed class SignalledLedger : ILeaseLedger
{
    private readonly ILeaseLedger _ledger;
    private readonly object _gate = new();
    private readonly TaskCompletionSource _flat = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<(int Released, TaskCompletionSource Signalled)> _waitingForReleases = [];
    private int _released;

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

        TaskCompletionSource[] reached;

        lock (_gate)
        {
            _released++;
            reached =
            [
                .. _waitingForReleases
                    .Where(waiting => _released >= waiting.Released)
                    .Select(waiting => waiting.Signalled),
            ];
            _waitingForReleases.RemoveAll(waiting => _released >= waiting.Released);
        }

        foreach (TaskCompletionSource signalled in reached)
        {
            signalled.TrySetResult();
        }
    }

    /// <summary>
    /// Gets a task that completes once a number of reservations have been released,
    /// which is how a test watches one consumer let go of a frame while another still
    /// holds it rather than waiting a moment and hoping that it happened.
    /// </summary>
    /// <param name="count">How many releases the test waits for.</param>
    /// <returns>A task that completes once that many reservations were released.</returns>
    internal Task ReservationsReleased(int count)
    {
        lock (_gate)
        {
            if (_released >= count)
            {
                return Task.CompletedTask;
            }

            var signalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waitingForReleases.Add((count, signalled));

            return signalled.Task;
        }
    }

    private void Signal()
    {
        if (_ledger.Created > 0 && _ledger.Outstanding <= 0 && _ledger.ReservationsOutstanding <= 0)
        {
            _flat.TrySetResult();
        }
    }
}
