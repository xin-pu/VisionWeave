using OpenCvSharp;
using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.IntegrationTests.Support;
using VisionWeave.OpenCv.Execution;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.IntegrationTests.OpenCv;

/// <summary>
/// The median blur node on its own: the pixels it removes, the input it must
/// leave untouched, and the kernels it refuses.
/// </summary>
public sealed class MedianBlurExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_removes_a_single_pixel_spike_without_modifying_the_input()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = new Mat(8, 8, MatType.CV_8UC1, Scalar.Black);
        mat.Set(4, 4, (byte)255);
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new MedianBlurExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.MedianBlurTypeId,
                ExecutorTestRequest.Parameters((OpenCvNodeIds.KernelSizeParameter, 3)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.BlurredPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(8);
        preview.Height.ShouldBe(8);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);

        // Eight of the nine pixels around the spike are black, so the median is
        // black: an average would leave a grey pixel here and a median leaves none.
        preview.Pixels[(4 * 8) + 4].ShouldBe((byte)0);
        preview.Pixels.ShouldAllBe(pixel => pixel == (byte)0);

        input.IsDisposed.ShouldBeFalse();
        mat.At<byte>(4, 4).ShouldBe((byte)255);

        scope.Owned.ShouldBeEmpty();

        produced.Dispose();
        input.Dispose();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(OpenCvParameterBounds.MaxKernelSize + 2)]
    public async Task ExecuteAsync_with_a_kernel_size_it_cannot_use_fails_with_a_diagnostic(int kernelSize)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC1, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new MedianBlurExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.MedianBlurTypeId,
                ExecutorTestRequest.Parameters((OpenCvNodeIds.KernelSizeParameter, kernelSize)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.KernelSizeParameter);
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

        await Should.ThrowAsync<OperationCanceledException>(() => new MedianBlurExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.MedianBlurTypeId,
                ExecutorTestRequest.Parameters((OpenCvNodeIds.KernelSizeParameter, 3)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }
}
