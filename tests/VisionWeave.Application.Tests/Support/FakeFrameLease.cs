using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// A frame lease that stands in for a native buffer: it registers its own
/// lifetime with the ledger so a test can assert that a run released every lease
/// it created, and it records how many consumer reservations were still
/// outstanding when the buffer was freed, which is the defect that would crash a
/// real native frame.
/// </summary>
public sealed class FakeFrameLease : ImageFrameLease
{
    private readonly ILeaseLedger _ledger;
    private readonly List<FakeReservation> _reservations = [];
    private int _taken;
    private int _released;
    private int _outstandingAtRelease = -1;

    private FakeFrameLease(ILeaseLedger ledger, int width, int height, FramePixelFormat pixelFormat)
        : base(width, height, pixelFormat)
    {
        _ledger = ledger;
        ledger.LeaseCreated();
    }

    /// <summary>
    /// Creates a lease that is tied to a ledger and a non-default frame so that a
    /// test can tell two frames apart.
    /// </summary>
    /// <param name="ledger">The ledger that observes the lease lifetime.</param>
    /// <param name="width">The frame width in pixels.</param>
    /// <param name="height">The frame height in pixels.</param>
    /// <returns>The lease.</returns>
    public static FakeFrameLease Create(ILeaseLedger ledger, int width = 16, int height = 16)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        return new FakeFrameLease(ledger, width, height, FramePixelFormat.Gray8);
    }

    /// <summary>
    /// Gets the number of reservations that were taken and not yet released.
    /// </summary>
    internal int OutstandingReservations => Volatile.Read(ref _taken) - Volatile.Read(ref _released);

    /// <summary>
    /// Gets the number of reservations that were taken since the lease was created.
    /// </summary>
    internal int ReservationsTaken => Volatile.Read(ref _taken);

    /// <summary>
    /// Gets the number of reservations that were still outstanding when the frame
    /// was freed, or <c>-1</c> while the frame is still alive.
    /// </summary>
    internal int OutstandingReservationsAtRelease => Volatile.Read(ref _outstandingAtRelease);

    /// <inheritdoc />
    protected override ILeaseReservation CreateReservation()
    {
        var reservation = new FakeReservation(this);

        lock (_reservations)
        {
            _reservations.Add(reservation);
        }

        Interlocked.Increment(ref _taken);
        return reservation;
    }

    /// <inheritdoc />
    protected override void DisposeCore()
    {
        Volatile.Write(ref _outstandingAtRelease, OutstandingReservations);
        _ledger.LeaseReleased();
    }

    private void Release(FakeReservation reservation)
    {
        lock (_reservations)
        {
            if (!_reservations.Remove(reservation))
            {
                return;
            }
        }

        Interlocked.Increment(ref _released);
    }

    private sealed class FakeReservation : ILeaseReservation
    {
        private readonly FakeFrameLease _lease;
        private int _released;

        internal FakeReservation(FakeFrameLease lease) => _lease = lease;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _lease.Release(this);
            }
        }
    }
}
