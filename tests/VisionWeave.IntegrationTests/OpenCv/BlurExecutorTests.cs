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
/// The box blur node on its own: the average it writes, the frame a kernel of one
/// passes through, the input it leaves untouched, and the kernels it refuses.
/// </summary>
public sealed class BlurExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_averages_a_spike_over_its_neighbourhood_without_modifying_the_input()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = new Mat(8, 8, MatType.CV_8UC1, Scalar.Black);
        mat.Set(4, 4, (byte)255);
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new BlurExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.BlurTypeId,
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

        // Every three by three window that can see the spike divides 255 by nine and
        // every other window sees only black, because the border repeats the frame's
        // own pixels rather than extending it with a value nobody chose.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool seesTheSpike = Math.Abs(row - 4) <= 1 && Math.Abs(column - 4) <= 1;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(seesTheSpike ? (byte)28 : (byte)0);
            }
        }

        input.IsDisposed.ShouldBeFalse();
        mat.At<byte>(4, 4).ShouldBe((byte)255);

        scope.Owned.ShouldBeEmpty();

        produced.Dispose();
        input.Dispose();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_kernel_of_one_passes_the_frame_through_unchanged()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = new Mat(8, 8, MatType.CV_8UC1, Scalar.Black);
        mat.Set(4, 4, (byte)255);
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new BlurExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.BlurTypeId,
                ExecutorTestRequest.Parameters((OpenCvNodeIds.KernelSizeParameter, OpenCvParameterBounds.MinKernelSize)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.BlurredPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // A kernel of one passes the frame through: the spike is still a spike and
        // every other pixel is still black.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isTheSpike = row == 4 && column == 4;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isTheSpike ? (byte)255 : (byte)0);
            }
        }

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(OpenCvParameterBounds.MaxKernelSize + 1)]
    public async Task ExecuteAsync_with_a_kernel_size_it_cannot_use_fails_with_a_diagnostic(int kernelSize)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC1, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new BlurExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.BlurTypeId,
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

        await Should.ThrowAsync<OperationCanceledException>(() => new BlurExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.BlurTypeId,
                ExecutorTestRequest.Parameters((OpenCvNodeIds.KernelSizeParameter, 3)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }
}
