using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Execution;

/// <summary>
/// Counts native lease and reservation lifetimes so that a leak becomes a
/// number a test can assert instead of a native crash. The counters are
/// monotonic and thread-safe, because leases are created and released from the
/// parallel branches of one run.
/// </summary>
public sealed class LeaseLedger : ILeaseLedger
{
    private int _outstanding;
    private int _reservationsOutstanding;
    private long _created;
    private long _released;

    /// <inheritdoc />
    public int Outstanding => Volatile.Read(ref _outstanding);

    /// <inheritdoc />
    public int ReservationsOutstanding => Volatile.Read(ref _reservationsOutstanding);

    /// <inheritdoc />
    public long Created => Interlocked.Read(ref _created);

    /// <inheritdoc />
    public long Released => Interlocked.Read(ref _released);

    /// <inheritdoc />
    public void LeaseCreated()
    {
        Interlocked.Increment(ref _outstanding);
        Interlocked.Increment(ref _created);
    }

    /// <inheritdoc />
    public void LeaseReleased()
    {
        Interlocked.Decrement(ref _outstanding);
        Interlocked.Increment(ref _released);
    }

    /// <inheritdoc />
    public void ReservationTaken() => Interlocked.Increment(ref _reservationsOutstanding);

    /// <inheritdoc />
    public void ReservationReleased() => Interlocked.Decrement(ref _reservationsOutstanding);
}
