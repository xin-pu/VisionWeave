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

/// <summary>Covers the second group of image operations migrated from Aries.</summary>
public sealed class AriesImageOperationExecutorTests
{
    [Fact]
    public async Task InRange_with_two_intensities_produces_the_expected_mask()
    {
        LeaseLedger ledger = new();
        MatFrameLease input = GrayFrame(ledger, 10, 20);
        NodeExecutionResult result = await new InRangeExecutor(ledger).ExecuteAsync(
            Request(input, (OpenCvNodeIds.LowerParameter, 15d), (OpenCvNodeIds.UpperParameter, 20d)),
            CancellationToken.None);

        MatFrameLease output = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ResultPortId);
        Pixels(output).ShouldBe([0, 255]);
        output.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task BitwiseNot_with_byte_extremes_inverts_each_pixel()
    {
        LeaseLedger ledger = new();
        MatFrameLease input = GrayFrame(ledger, 0, 255);
        NodeExecutionResult result = await new BitwiseNotExecutor(ledger).ExecuteAsync(
            Request(input), CancellationToken.None);

        MatFrameLease output = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ResultPortId);
        Pixels(output).ShouldBe([255, 0]);
        output.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task EqualizeHist_with_two_levels_expands_the_histogram()
    {
        LeaseLedger ledger = new();
        MatFrameLease input = GrayFrame(ledger, 10, 20);
        NodeExecutionResult result = await new EqualizeHistExecutor(ledger).ExecuteAsync(
            Request(input), CancellationToken.None);

        MatFrameLease output = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ResultPortId);
        Pixels(output).ShouldBe([0, 255]);
        output.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Rotate_with_zero_angle_preserves_the_frame()
    {
        LeaseLedger ledger = new();
        MatFrameLease input = GrayFrame(ledger, 10, 20);
        NodeExecutionResult result = await new RotateExecutor(ledger).ExecuteAsync(
            Request(input, (OpenCvNodeIds.AngleParameter, 0d)), CancellationToken.None);

        MatFrameLease output = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ResultPortId);
        Pixels(output).ShouldBe([10, 20]);
        output.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Sharpen_with_a_flat_frame_preserves_the_flat_intensity()
    {
        LeaseLedger ledger = new();
        var mat = new Mat(3, 3, MatType.CV_8UC1, Scalar.All(20));
        MatFrameLease input = MatFrameLease.Create(mat, ledger);
        NodeExecutionResult result = await new SharpenExecutor(ledger).ExecuteAsync(
            Request(input), CancellationToken.None);

        MatFrameLease output = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ResultPortId);
        Pixels(output).ShouldAllBe(pixel => pixel == 20);
        output.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    private static MatFrameLease GrayFrame(LeaseLedger ledger, byte left, byte right)
    {
        var mat = new Mat(1, 2, MatType.CV_8UC1);
        mat.Set(0, 0, left);
        mat.Set(0, 1, right);
        return MatFrameLease.Create(mat, ledger);
    }

    private static byte[] Pixels(MatFrameLease frame)
        => FramePreviewConverter.Default.Convert(frame).Pixels;

    private static NodeExecutionRequest Request(
        MatFrameLease input,
        params (string Name, object? Value)[] parameters)
        => ExecutorTestRequest.For(
            "visionweave.test.aries-operation",
            ExecutorTestRequest.Parameters(parameters),
            ExecutorTestRequest.ImageInput(input),
            new TestResourceScope());
}
