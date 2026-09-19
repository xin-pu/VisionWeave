using OpenCvSharp;
using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.IntegrationTests.Support;
using VisionWeave.OpenCv.Execution;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.IntegrationTests.OpenCv;

/// <summary>
/// The crop node on its own: the rectangle it keeps, the frame it owns rather
/// than shares, and the rectangles it refuses.
/// </summary>
public sealed class CropExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_keeps_the_rectangle_the_parameters_name()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = new Mat(8, 8, MatType.CV_8UC1, Scalar.Black);

        // Two markers of different values, so the test can tell the kept rectangle
        // from one that was shifted, mirrored, or scaled: the first pixel of the
        // rectangle is the input's (2, 3), and the fourth is its (5, 3).
        mat.Set(3, 2, (byte)255);
        mat.Set(3, 5, (byte)77);

        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new CropExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.CropTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.XParameter, 2),
                    (OpenCvNodeIds.YParameter, 3),
                    (OpenCvNodeIds.WidthParameter, 4),
                    (OpenCvNodeIds.HeightParameter, 4)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.CroppedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(4);
        preview.Height.ShouldBe(4);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);
        preview.Pixels[0].ShouldBe((byte)255);
        preview.Pixels[3].ShouldBe((byte)77);

        // The producer's frame is the producer's: the node read it and never wrote to it.
        input.IsDisposed.ShouldBeFalse();
        mat.At<byte>(3, 2).ShouldBe((byte)255);

        // The rectangle is copied, not published as a view of the producer's buffer,
        // because a view would share memory the run does not own. Writing to the
        // producer's frame after the node ran is what tells the two apart. If the
        // rectangle is the frame the lease owns, it still reads the values the
        // producer did not overwrite, and it keeps the one it did.
        mat.Set(3, 2, (byte)0);

        PreviewFrame afterTheProducerWrote = FramePreviewConverter.Default.Convert(produced);
        afterTheProducerWrote.Pixels[0].ShouldBe((byte)255);
        afterTheProducerWrote.Pixels[3].ShouldBe((byte)77);

        scope.Owned.ShouldBeEmpty();

        produced.Dispose();
        input.Dispose();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_rectangle_that_leaves_the_frame_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC1, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new CropExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.CropTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.XParameter, 4),
                    (OpenCvNodeIds.YParameter, 4),
                    (OpenCvNodeIds.WidthParameter, 8),
                    (OpenCvNodeIds.HeightParameter, 8)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("does not fit");
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Theory]
    [InlineData(-1, 0, 4, 4)]
    [InlineData(0, -1, 4, 4)]
    [InlineData(0, 0, 0, 4)]
    [InlineData(0, 0, 4, 0)]
    [InlineData(0, 0, OpenCvParameterBounds.MaxDimension + 1, 4)]
    public async Task ExecuteAsync_with_an_extent_out_of_range_fails_with_a_diagnostic(int x, int y, int width, int height)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC1, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new CropExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.CropTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.XParameter, x),
                    (OpenCvNodeIds.YParameter, y),
                    (OpenCvNodeIds.WidthParameter, width),
                    (OpenCvNodeIds.HeightParameter, height)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.WidthParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_creates_no_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC1, Scalar.All(20)), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new CropExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.CropTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.XParameter, 1),
                    (OpenCvNodeIds.YParameter, 1),
                    (OpenCvNodeIds.WidthParameter, 4),
                    (OpenCvNodeIds.HeightParameter, 4)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }
}
