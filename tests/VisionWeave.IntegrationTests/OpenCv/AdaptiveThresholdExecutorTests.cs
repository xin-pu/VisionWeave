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
/// The adaptive threshold node on its own: the pixels its own neighbourhood
/// separates, the rule it applies, the input it leaves untouched, and the
/// parameters and frames it refuses.
/// </summary>
public sealed class AdaptiveThresholdExecutorTests
{
    [Theory]
    [InlineData(OpenCvNodeIds.AdaptiveMethodMean)]
    [InlineData(OpenCvNodeIds.AdaptiveMethodGaussian)]
    public async Task ExecuteAsync_fails_the_first_column_of_a_step_and_passes_the_flat_regions(string method)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepMat(), ledger);

        NodeExecutionResult result = await new AdaptiveThresholdExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.AdaptiveThresholdTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.MaxValueParameter, 255d),
                    (OpenCvNodeIds.AdaptiveMethodParameter, method),
                    (OpenCvNodeIds.ThresholdTypeParameter, OpenCvNodeIds.ThresholdBinary),
                    (OpenCvNodeIds.BlockSizeParameter, 3),
                    (OpenCvNodeIds.ConstantParameter, 5d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ThresholdedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(8);
        preview.Height.ShouldBe(8);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);

        // A flat region passes, because a pixel equals the average it is measured
        // against, and the dark column beside the step fails, because the average it
        // is measured against is pulled up by the brighter side of the step.
        for (int row = 0; row < preview.Height; row++)
        {
            preview.Pixels[(row * 8) + 3].ShouldBe((byte)0, $"row {row} is the dark column beside the step");
            preview.Pixels[(row * 8) + 2].ShouldBe((byte)255, $"row {row} is the last dark column the step does not reach");
            preview.Pixels[(row * 8) + 4].ShouldBe((byte)255, $"row {row} is the first bright column");
        }

        input.IsDisposed.ShouldBeFalse();
        produced.Dispose();
        input.Dispose();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_the_inverted_rule_swaps_which_side_is_kept()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepMat(), ledger);

        NodeExecutionResult result = await new AdaptiveThresholdExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.AdaptiveThresholdTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.MaxValueParameter, 255d),
                    (OpenCvNodeIds.ThresholdTypeParameter, OpenCvNodeIds.ThresholdBinaryInverted),
                    (OpenCvNodeIds.BlockSizeParameter, 3),
                    (OpenCvNodeIds.ConstantParameter, 5d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ThresholdedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Pixels[(0 * 8) + 3].ShouldBe((byte)255);
        preview.Pixels[(0 * 8) + 2].ShouldBe((byte)0);
        preview.Pixels[(0 * 8) + 4].ShouldBe((byte)0);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(OpenCvParameterBounds.MaxKernelSize + 2)]
    public async Task ExecuteAsync_with_a_block_size_it_cannot_use_fails_with_a_diagnostic(int blockSize)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepMat(), ledger);

        NodeExecutionResult result = await new AdaptiveThresholdExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.AdaptiveThresholdTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.MaxValueParameter, 255d),
                    (OpenCvNodeIds.BlockSizeParameter, blockSize),
                    (OpenCvNodeIds.ConstantParameter, 5d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.BlockSizeParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Theory]
    [InlineData(300d)]
    [InlineData(-300d)]
    public async Task ExecuteAsync_with_a_constant_it_cannot_use_fails_with_a_diagnostic(double constant)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepMat(), ledger);

        NodeExecutionResult result = await new AdaptiveThresholdExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.AdaptiveThresholdTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.MaxValueParameter, 255d),
                    (OpenCvNodeIds.BlockSizeParameter, 3),
                    (OpenCvNodeIds.ConstantParameter, constant)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.ConstantParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_with_a_colour_frame_fails_with_a_diagnostic_naming_the_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC3, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new AdaptiveThresholdExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.AdaptiveThresholdTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.MaxValueParameter, 255d),
                    (OpenCvNodeIds.BlockSizeParameter, 3),
                    (OpenCvNodeIds.ConstantParameter, 5d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(nameof(FramePixelFormat.Bgr24));
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

        await Should.ThrowAsync<OperationCanceledException>(() => new AdaptiveThresholdExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.AdaptiveThresholdTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.MaxValueParameter, 255d),
                    (OpenCvNodeIds.BlockSizeParameter, 3),
                    (OpenCvNodeIds.ConstantParameter, 5d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    /// <summary>
    /// Builds the frame every test in this class binarises: four dark columns beside
    /// four bright ones, which is the smallest image with a background, a foreground,
    /// and a border between them.
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
