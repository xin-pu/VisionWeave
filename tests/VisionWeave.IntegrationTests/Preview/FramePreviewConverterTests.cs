using OpenCvSharp;
using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.IntegrationTests.Preview;

/// <summary>
/// Preview conversion produces a managed copy that holds no native resource, and
/// it never exceeds the configured pixel area, which is what keeps a preview from
/// retaining a full-size frame.
/// </summary>
public sealed class FramePreviewConverterTests
{
    [Fact]
    public void Convert_of_a_small_frame_copies_every_pixel_at_full_size()
    {
        LeaseLedger ledger = new();
        MatFrameLease lease = MatFrameLease.Create(new Mat(2, 4, MatType.CV_8UC1, Scalar.All(200)), ledger);

        PreviewFrame preview = FramePreviewConverter.Default.Convert(lease);

        preview.Width.ShouldBe(4);
        preview.Height.ShouldBe(2);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);
        preview.StridePerRow.ShouldBe(4);
        preview.Pixels.Length.ShouldBe(8);
        preview.Pixels.ShouldAllBe(pixel => pixel == 200);

        // Converting copies the frame; it neither creates nor releases a lease.
        ledger.Created.ShouldBe(1);
        ledger.Outstanding.ShouldBe(1);
        lease.Dispose();
    }

    [Fact]
    public void Convert_of_a_three_channel_frame_keeps_the_channel_order()
    {
        LeaseLedger ledger = new();
        MatFrameLease lease = MatFrameLease.Create(
            new Mat(2, 3, MatType.CV_8UC3, new Scalar(10, 20, 30)),
            ledger);

        PreviewFrame preview = FramePreviewConverter.Default.Convert(lease);

        preview.PixelFormat.ShouldBe(FramePixelFormat.Bgr24);
        preview.StridePerRow.ShouldBe(9);
        preview.Pixels.Length.ShouldBe(18);
        preview.Pixels[0].ShouldBe((byte)10);
        preview.Pixels[1].ShouldBe((byte)20);
        preview.Pixels[2].ShouldBe((byte)30);

        lease.Dispose();
    }

    [Fact]
    public void Convert_of_a_frame_over_the_limit_downscales_within_the_limit()
    {
        LeaseLedger ledger = new();
        var converter = new FramePreviewConverter(new FramePreviewOptions { MaxPixelArea = 1024 });
        MatFrameLease lease = MatFrameLease.Create(new Mat(64, 64, MatType.CV_8UC1, Scalar.All(90)), ledger);

        PreviewFrame preview = converter.Convert(lease);

        preview.Width.ShouldBe(32);
        preview.Height.ShouldBe(32);
        (preview.Width * preview.Height).ShouldBeLessThanOrEqualTo(1024);
        preview.Pixels.Length.ShouldBe(1024);
        preview.Pixels.ShouldAllBe(pixel => pixel == 90);

        lease.Dispose();
    }

    [Fact]
    public void Convert_of_a_sixteen_bit_frame_is_rejected()
    {
        LeaseLedger ledger = new();
        MatFrameLease lease = MatFrameLease.Create(new Mat(2, 2, MatType.CV_16UC1, Scalar.All(9)), ledger);

        // The runtime turns this into the preview-conversion warning, so a frame
        // the renderer cannot show never fails the run.
        Should.Throw<NotSupportedException>(() => FramePreviewConverter.Default.Convert(lease));

        lease.IsDisposed.ShouldBeFalse();
        lease.Dispose();
    }

    [Fact]
    public void ComputeTargetSize_keeps_a_frame_within_the_limit()
    {
        (int width, int height) = FramePreviewConverter.ComputeTargetSize(640, 480, 1920 * 1080);

        width.ShouldBe(640);
        height.ShouldBe(480);
    }

    [Fact]
    public void ComputeTargetSize_downscales_a_frame_over_the_limit_and_keeps_the_aspect_ratio()
    {
        (int width, int height) = FramePreviewConverter.ComputeTargetSize(4000, 2000, 1000000);

        ((long)width * height).ShouldBeLessThanOrEqualTo(1000000);
        (width / (double)height).ShouldBe(2d, 0.01d);
    }

    [Fact]
    public void ComputeTargetSize_of_an_extreme_ratio_never_produces_an_empty_preview()
    {
        (int width, int height) = FramePreviewConverter.ComputeTargetSize(100000, 2, 16);

        width.ShouldBeGreaterThanOrEqualTo(1);
        height.ShouldBeGreaterThanOrEqualTo(1);
        ((long)width * height).ShouldBeLessThanOrEqualTo(16);
    }

    [Fact]
    public void ComputeTargetSize_rejects_a_limit_that_is_not_positive()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => FramePreviewConverter.ComputeTargetSize(10, 10, 0));
    }

    [Fact]
    public void Constructor_rejects_a_limit_that_is_not_positive()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new FramePreviewConverter(new FramePreviewOptions { MaxPixelArea = 0 }));
    }
}
