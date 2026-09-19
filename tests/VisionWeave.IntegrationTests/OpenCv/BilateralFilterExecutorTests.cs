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
/// The bilateral filter node on its own: the step it keeps, the noise it smooths,
/// the input it leaves untouched, and the parameters and frames it refuses.
/// </summary>
public sealed class BilateralFilterExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_keeps_a_step_it_is_given_and_the_input_it_read()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepMat(), ledger);

        NodeExecutionResult result = await new BilateralFilterExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.BilateralFilterTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.DiameterParameter, 5),
                    (OpenCvNodeIds.SigmaColorParameter, 20d),
                    (OpenCvNodeIds.SigmaSpaceParameter, 20d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.BlurredPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(8);
        preview.Height.ShouldBe(8);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);

        // The step is wider than the intensity sigma, so a neighbour across it is
        // weighted by almost nothing and every pixel keeps the value it had. A blur
        // that averaged the neighbourhood regardless would draw a grey band here.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                preview.Pixels[(row * preview.Width) + column].ShouldBe(column < 4 ? (byte)40 : (byte)200);
            }
        }

        input.IsDisposed.ShouldBeFalse();
        produced.Dispose();
        input.Dispose();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_smooths_a_spike_that_is_within_the_intensity_sigma()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = new Mat(8, 8, MatType.CV_8UC1, Scalar.All(100));
        mat.Set(4, 4, (byte)255);
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new BilateralFilterExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.BilateralFilterTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.DiameterParameter, 5),
                    (OpenCvNodeIds.SigmaColorParameter, 100d),
                    (OpenCvNodeIds.SigmaSpaceParameter, 20d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.BlurredPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // The spike differs from its neighbours by less than the intensity sigma, so
        // it is averaged away: the centre comes down and the ring around it comes up.
        byte centre = preview.Pixels[(4 * 8) + 4];
        centre.ShouldBeLessThan((byte)255);
        centre.ShouldBeGreaterThan((byte)100);
        preview.Pixels[(3 * 8) + 3].ShouldBeGreaterThan((byte)100);

        // What the read is not allowed to change is the input itself.
        mat.At<byte>(4, 4).ShouldBe((byte)255);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(OpenCvParameterBounds.MaxKernelSize + 1)]
    public async Task ExecuteAsync_with_a_diameter_it_cannot_use_fails_with_a_diagnostic(int diameter)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepMat(), ledger);

        NodeExecutionResult result = await new BilateralFilterExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.BilateralFilterTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.DiameterParameter, diameter),
                    (OpenCvNodeIds.SigmaColorParameter, 20d),
                    (OpenCvNodeIds.SigmaSpaceParameter, 20d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.DiameterParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Theory]
    [InlineData(-1d, 20d)]
    [InlineData(20d, -1d)]
    [InlineData(OpenCvParameterBounds.MaxBilateralSigma + 1, 20d)]
    public async Task ExecuteAsync_with_a_sigma_it_cannot_use_fails_with_a_diagnostic(double sigmaColor, double sigmaSpace)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepMat(), ledger);

        NodeExecutionResult result = await new BilateralFilterExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.BilateralFilterTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.DiameterParameter, 5),
                    (OpenCvNodeIds.SigmaColorParameter, sigmaColor),
                    (OpenCvNodeIds.SigmaSpaceParameter, sigmaSpace)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);

        string message = result.Diagnostics.ShouldHaveSingleItem().Message;
        message.ShouldContain(OpenCvNodeIds.SigmaColorParameter);
        message.ShouldContain(OpenCvNodeIds.SigmaSpaceParameter);

        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_with_a_layout_it_does_not_smooth_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_16UC1, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new BilateralFilterExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.BilateralFilterTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.DiameterParameter, 5),
                    (OpenCvNodeIds.SigmaColorParameter, 20d),
                    (OpenCvNodeIds.SigmaSpaceParameter, 20d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(nameof(FramePixelFormat.Gray16));
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_creates_no_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepMat(), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new BilateralFilterExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.BilateralFilterTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.DiameterParameter, 5),
                    (OpenCvNodeIds.SigmaColorParameter, 20d),
                    (OpenCvNodeIds.SigmaSpaceParameter, 20d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    /// <summary>
    /// Builds the frame every test in this class smooths: four dark columns beside
    /// four bright ones, which is the smallest image with an edge in it.
    /// </summary>
    private static Mat StepMat()
    {
        var mat = new Mat(8, 8, MatType.CV_8UC1, Scalar.All(40));

        using (Mat bright = mat.ColRange(4, 8))
        {
            bright.SetTo(new Scalar(200));
        }

        return mat;
    }
}
