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
///     Covers the image-value and geometry transformations migrated from the Aries
///     workflow vocabulary.
/// </summary>
public sealed class ImageTransformExecutorTests
{
    [Fact]
    public async Task Normalize_stretches_values_into_the_requested_range()
    {
        LeaseLedger ledger = new();
        MatFrameLease input = GrayFrame(ledger, 10, 20);
        var scope = new TestResourceScope();

        NodeExecutionResult result = await new NormalizeExecutor(ledger).ExecuteAsync(
            Request(
                OpenCvNodeIds.NormalizeTypeId,
                input,
                scope,
                (OpenCvNodeIds.AlphaParameter, 0d),
                (OpenCvNodeIds.BetaParameter, 100d)),
            CancellationToken.None);

        MatFrameLease output = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.NormalizedPortId);
        Pixels(output).ShouldBe([0, 100]);
        Pixels(input).ShouldBe([10, 20]);

        output.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ConvertScaleAbs_applies_contrast_and_brightness()
    {
        LeaseLedger ledger = new();
        MatFrameLease input = GrayFrame(ledger, 10, 20);
        var scope = new TestResourceScope();

        NodeExecutionResult result = await new ConvertScaleAbsExecutor(ledger).ExecuteAsync(
            Request(
                OpenCvNodeIds.ConvertScaleAbsTypeId,
                input,
                scope,
                (OpenCvNodeIds.AlphaParameter, 2d),
                (OpenCvNodeIds.BetaParameter, 5d)),
            CancellationToken.None);

        MatFrameLease output = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ConvertedPortId);
        Pixels(output).ShouldBe([25, 45]);

        output.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Flip_horizontal_reverses_columns()
    {
        LeaseLedger ledger = new();
        MatFrameLease input = GrayFrame(ledger, 10, 20);
        var scope = new TestResourceScope();

        NodeExecutionResult result = await new FlipExecutor(ledger).ExecuteAsync(
            Request(
                OpenCvNodeIds.FlipTypeId,
                input,
                scope,
                (OpenCvNodeIds.FlipModeParameter, OpenCvNodeIds.FlipHorizontal)),
            CancellationToken.None);

        MatFrameLease output = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.FlippedPortId);
        Pixels(output).ShouldBe([20, 10]);

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
        string typeId,
        MatFrameLease input,
        TestResourceScope scope,
        params (string Name, object? Value)[] parameters)
        => ExecutorTestRequest.For(
            typeId,
            ExecutorTestRequest.Parameters(parameters),
            ExecutorTestRequest.ImageInput(input),
            scope);
}
