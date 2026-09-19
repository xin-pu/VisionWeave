using OpenCvSharp;
using VisionWeave.Contracts.Values;

namespace VisionWeave.OpenCv.Frames;

/// <summary>
/// The image frame lease of the OpenCV layer: it owns one native
/// <see cref="Mat"/>, reports its lifetime to the lease ledger, and hands out the
/// consumer reservations that keep the mat alive until the last reader is
/// finished.
/// <para>
/// The mat is never shared as a mutable buffer. Code inside this assembly reads
/// it through <see cref="Mat"/>; an executor that must write pixels calls
/// <see cref="CloneWritable"/> and owns that copy. The mat is freed only by
/// <see cref="ImageFrameLease.Dispose"/>, which the runtime calls once the
/// producer ownership, every reservation, and every cache entry is gone.
/// </para>
/// </summary>
public sealed class MatFrameLease : ImageFrameLease
{
    private readonly ILeaseLedger _ledger;
    private readonly List<Reservation> _reservations = [];
    private Mat? _mat;
    private int _taken;
    private int _outstandingAtRelease = -1;

    private MatFrameLease(Mat mat, ILeaseLedger ledger)
        : base(mat.Cols, mat.Rows, MatTypeMapping.ToFramePixelFormat(mat.Type()))
    {
        _mat = mat;
        _ledger = ledger;
        ledger.LeaseCreated();
    }

    /// <summary>
    /// Takes ownership of a native mat and registers the lease with the ledger.
    /// The caller must not dispose or write the mat afterwards.
    /// </summary>
    /// <param name="mat">The mat the lease takes ownership of.</param>
    /// <param name="ledger">The ledger that observes the lease lifetime.</param>
    /// <returns>The lease.</returns>
    /// <exception cref="ArgumentException">The mat is empty or its pixel layout is unsupported.</exception>
    public static MatFrameLease Create(Mat mat, ILeaseLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(mat);
        ArgumentNullException.ThrowIfNull(ledger);

        if (mat.Empty())
        {
            mat.Dispose();
            throw new ArgumentException("An empty mat cannot become an image frame lease.", nameof(mat));
        }

        return new MatFrameLease(mat, ledger);
    }

    /// <summary>
    /// Gets the native mat of this frame. The mat is read-only by contract: it is
    /// internal so that no assembly outside this one can write a published frame.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The frame was released.</exception>
    internal Mat Mat => Volatile.Read(ref _mat) ?? throw new ObjectDisposedException(nameof(MatFrameLease));

    /// <summary>
    /// Gets the number of consumer reservations that were taken and not yet
    /// released.
    /// </summary>
    public int OutstandingReservations
    {
        get
        {
            lock (_reservations)
            {
                return _reservations.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of reservations that were taken since the frame was created.
    /// </summary>
    public int ReservationsTaken => Volatile.Read(ref _taken);

    /// <summary>
    /// Gets the number of reservations that were still outstanding when the mat
    /// was freed, or <c>-1</c> while the frame is still alive. A value other than
    /// zero means a consumer kept reading a frame that its owner had released.
    /// </summary>
    public int OutstandingReservationsAtRelease => Volatile.Read(ref _outstandingAtRelease);

    /// <summary>
    /// Creates an independent, writable copy of the frame. The caller owns the
    /// copy and must register it in the execution resource scope so that the
    /// runtime disposes it.
    /// </summary>
    /// <returns>The writable copy.</returns>
    /// <exception cref="ObjectDisposedException">The frame was released.</exception>
    public Mat CloneWritable() => Mat.Clone();

    /// <inheritdoc />
    protected override ILeaseReservation CreateReservation()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(MatFrameLease));
        }

        var reservation = new Reservation(this);

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
        lock (_reservations)
        {
            Volatile.Write(ref _outstandingAtRelease, _reservations.Count);
        }

        // The lease reports its own release to the ledger, exactly like it
        // reported its creation, so the counters are owned in one place only.
        Interlocked.Exchange(ref _mat, null)?.Dispose();
        _ledger.LeaseReleased();
    }

    private void Release(Reservation reservation)
    {
        lock (_reservations)
        {
            _reservations.Remove(reservation);
        }
    }

    private sealed class Reservation : ILeaseReservation
    {
        private readonly MatFrameLease _lease;
        private int _released;

        internal Reservation(MatFrameLease lease) => _lease = lease;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _lease.Release(this);
            }
        }
    }
}
