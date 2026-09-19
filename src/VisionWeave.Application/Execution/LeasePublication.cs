using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Execution;

/// <summary>
/// Keeps one published image frame alive for exactly as long as something still
/// needs it. The runtime creates the publication when a producer hands its lease
/// over, takes one reservation per scheduled consumer and one for the preview
/// conversion, seals it so that no further reservation can be taken, and the
/// lease is released as soon as the last reservation is released.
/// </summary>
internal sealed class LeasePublication
{
    private readonly ImageFrameLease _lease;
    private readonly ILeaseLedger _ledger;
    private readonly List<ILeaseReservation> _reservations = [];
    private int _remaining;
    private int _sealed;
    private int _released;

    internal LeasePublication(ImageFrameLease lease, ILeaseLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(ledger);

        _lease = lease;
        _ledger = ledger;
    }

    internal ImageFrameLease Lease => _lease;

    /// <summary>
    /// Gets the number of reservations that have been taken but not yet released.
    /// </summary>
    internal int RemainingReservations => Volatile.Read(ref _remaining);

    /// <summary>
    /// Takes a reservation on the lease.
    /// </summary>
    /// <returns>The reservation, which the caller must release.</returns>
    /// <exception cref="InvalidOperationException">The publication is sealed or released.</exception>
    internal ILeaseReservation Reserve()
    {
        if (Volatile.Read(ref _sealed) != 0 || Volatile.Read(ref _released) != 0)
        {
            throw new InvalidOperationException(
                "A published lease can only be reserved before the publication is sealed.");
        }

        ILeaseReservation reservation = _lease.AddConsumerReservation();
        _ledger.ReservationTaken();

        lock (_reservations)
        {
            _reservations.Add(reservation);
        }

        Interlocked.Increment(ref _remaining);
        return reservation;
    }

    /// <summary>
    /// States that every reservation has been taken, so the lease can be released
    /// as soon as the outstanding ones are gone.
    /// </summary>
    internal void Seal()
    {
        if (Interlocked.Exchange(ref _sealed, 1) != 0)
        {
            return;
        }

        if (Volatile.Read(ref _remaining) == 0)
        {
            Release();
        }
    }

    /// <summary>
    /// Releases a reservation taken from this publication.
    /// </summary>
    /// <param name="reservation">The reservation to release.</param>
    internal void ReleaseReservation(ILeaseReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        lock (_reservations)
        {
            if (!_reservations.Remove(reservation))
            {
                return;
            }
        }

        reservation.Dispose();
        _ledger.ReservationReleased();

        if (Interlocked.Decrement(ref _remaining) == 0 && Volatile.Read(ref _sealed) != 0)
        {
            Release();
        }
    }

    /// <summary>
    /// Releases the producer's ownership of the lease, together with any
    /// reservation that was never released by its consumer. The native frame is
    /// only freed once the last reservation is gone, which is what keeps a
    /// consumer from reading a disposed frame.
    /// </summary>
    internal void Release()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
        {
            return;
        }

        List<ILeaseReservation> outstanding;

        lock (_reservations)
        {
            outstanding = [.. _reservations];
            _reservations.Clear();
        }

        foreach (ILeaseReservation reservation in outstanding)
        {
            reservation.Dispose();
            _ledger.ReservationReleased();
            Interlocked.Decrement(ref _remaining);
        }

        // The lease reports its own release to the ledger, exactly like it
        // reported its creation, so the counters are owned in one place only.
        _lease.Dispose();
    }
}
