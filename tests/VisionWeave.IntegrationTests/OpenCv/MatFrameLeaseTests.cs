using OpenCvSharp;
using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;

namespace VisionWeave.IntegrationTests.OpenCv;

/// <summary>
/// The lifetime rules of a native frame lease: it reports its own creation and
/// release, it tracks the consumer reservations that keep the mat alive, and it
/// frees the native buffer exactly once. The lease owns the mat, so a test never
/// disposes both.
/// </summary>
public sealed class MatFrameLeaseTests
{
    [Fact]
    public void Create_reports_the_lease_to_the_ledger()
    {
        LeaseLedger ledger = new();
        MatFrameLease lease = MatFrameLease.Create(new Mat(4, 4, MatType.CV_8UC1, Scalar.Black), ledger);

        ledger.Created.ShouldBe(1);
        ledger.Outstanding.ShouldBe(1);
        lease.Width.ShouldBe(4);
        lease.Height.ShouldBe(4);
        lease.IsDisposed.ShouldBeFalse();

        lease.Dispose();

        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
        lease.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void Create_with_an_empty_mat_rejects_the_frame_and_keeps_the_ledger_clean()
    {
        LeaseLedger ledger = new();

        Should.Throw<ArgumentException>(() => MatFrameLease.Create(new Mat(), ledger));

        ledger.Created.ShouldBe(0);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public void Create_with_a_null_ledger_throws()
    {
        using var mat = new Mat(2, 2, MatType.CV_8UC1, Scalar.Black);

        Should.Throw<ArgumentNullException>(() => MatFrameLease.Create(mat, null!));

        // The rejected registration never took ownership of the mat, so the caller
        // still owns it and disposes it here.
        mat.Rows.ShouldBe(2);
    }

    [Fact]
    public void AddConsumerReservation_keeps_the_reservation_outstanding_until_it_is_released()
    {
        LeaseLedger ledger = new();
        MatFrameLease lease = MatFrameLease.Create(new Mat(2, 2, MatType.CV_8UC1, Scalar.Black), ledger);

        ILeaseReservation first = lease.AddConsumerReservation();
        ILeaseReservation second = lease.AddConsumerReservation();

        lease.ReservationsTaken.ShouldBe(2);
        lease.OutstandingReservations.ShouldBe(2);

        second.Dispose();
        lease.OutstandingReservations.ShouldBe(1);

        first.Dispose();
        lease.OutstandingReservations.ShouldBe(0);

        lease.Dispose();
    }

    [Fact]
    public void Dispose_records_the_reservations_that_were_still_outstanding()
    {
        LeaseLedger ledger = new();
        MatFrameLease lease = MatFrameLease.Create(new Mat(2, 2, MatType.CV_8UC1, Scalar.Black), ledger);
        ILeaseReservation leaked = lease.AddConsumerReservation();

        lease.Dispose();

        // The defect this records is real: a consumer that still reads a frame its
        // producer released would be reading freed native memory.
        lease.OutstandingReservationsAtRelease.ShouldBe(1);
        leaked.Dispose();
    }

    [Fact]
    public void CloneWritable_returns_a_copy_that_survives_the_lease()
    {
        LeaseLedger ledger = new();
        MatFrameLease lease = MatFrameLease.Create(new Mat(2, 2, MatType.CV_8UC1, Scalar.All(120)), ledger);

        using Mat copy = lease.CloneWritable();
        copy.Set(0, 0, (byte)7);

        lease.Dispose();

        copy.Rows.ShouldBe(2);
        copy.Cols.ShouldBe(2);
        copy.At<byte>(0, 0).ShouldBe((byte)7);

        // The copy is not a lease, so freeing it does not move the ledger.
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public void CloneWritable_after_the_lease_was_released_throws()
    {
        LeaseLedger ledger = new();
        MatFrameLease lease = MatFrameLease.Create(new Mat(2, 2, MatType.CV_8UC1, Scalar.Black), ledger);

        lease.Dispose();

        Should.Throw<ObjectDisposedException>(() => lease.CloneWritable());
    }

    [Fact]
    public void AddConsumerReservation_after_the_lease_was_released_throws()
    {
        LeaseLedger ledger = new();
        MatFrameLease lease = MatFrameLease.Create(new Mat(2, 2, MatType.CV_8UC1, Scalar.Black), ledger);

        lease.Dispose();

        Should.Throw<ObjectDisposedException>(() => lease.AddConsumerReservation());
    }
}
