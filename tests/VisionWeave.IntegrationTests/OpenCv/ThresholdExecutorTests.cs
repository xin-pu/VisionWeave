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
/// The threshold node on its own: the pixels each rule keeps, and the options,
/// levels, and frames it refuses. A pixel below the threshold and one above it
/// are both present in every case, so a rule that ignored either side of the
/// comparison would show up here.
/// </summary>
public sealed class ThresholdExecutorTests
{
    [Theory]
    [InlineData(OpenCvNodeIds.ThresholdBinary, 255, 0)]
    [InlineData(OpenCvNodeIds.ThresholdBinaryInverted, 0, 255)]
    [InlineData(OpenCvNodeIds.ThresholdTruncate, 150, 100)]
    [InlineData(OpenCvNodeIds.ThresholdToZero, 200, 0)]
    [InlineData(OpenCvNodeIds.ThresholdToZeroInverted, 0, 100)]
    public async Task ExecuteAsync_with_each_rule_keeps_the_pixels_the_rule_names(
        string rule,
        byte expectedAbove,
        byte expectedBelow)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = new Mat(1, 2, MatType.CV_8UC1, Scalar.Black);
        mat.Set(0, 0, (byte)100);
        mat.Set(0, 1, (byte)200);
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new ThresholdExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.ThresholdTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.ThresholdParameter, 150d),
                    (OpenCvNodeIds.MaxValueParameter, 255d),
                    (OpenCvNodeIds.ThresholdTypeParameter, rule)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ThresholdedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(2);
        preview.Height.ShouldBe(1);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);
        preview.Pixels[0].ShouldBe(expectedBelow);
        preview.Pixels[1].ShouldBe(expectedAbove);

        input.IsDisposed.ShouldBeFalse();
        mat.At<byte>(0, 0).ShouldBe((byte)100);
        mat.At<byte>(0, 1).ShouldBe((byte)200);

        scope.Owned.ShouldBeEmpty();

        produced.Dispose();
        input.Dispose();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_an_unknown_rule_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(4, 4, MatType.CV_8UC1, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new ThresholdExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.ThresholdTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.ThresholdParameter, 150d),
                    (OpenCvNodeIds.MaxValueParameter, 255d),
                    (OpenCvNodeIds.ThresholdTypeParameter, "otsu")),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.ThresholdTypeParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_with_a_16_bit_input_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(4, 4, MatType.CV_16UC1, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new ThresholdExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.ThresholdTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.ThresholdParameter, 150d),
                    (OpenCvNodeIds.MaxValueParameter, 255d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("8-bit");
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Theory]
    [InlineData(-1d, 255d)]
    [InlineData(300d, 255d)]
    [InlineData(150d, 300d)]
    public async Task ExecuteAsync_with_a_level_out_of_range_fails_with_a_diagnostic(double threshold, double maxValue)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(4, 4, MatType.CV_8UC1, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new ThresholdExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.ThresholdTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.ThresholdParameter, threshold),
                    (OpenCvNodeIds.MaxValueParameter, maxValue)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.MaxValueParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_creates_no_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(4, 4, MatType.CV_8UC1, Scalar.All(20)), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new ThresholdExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.ThresholdTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.ThresholdParameter, 150d),
                    (OpenCvNodeIds.MaxValueParameter, 255d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }
}
