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
/// The colour conversion node on its own: the colour space it produces, the
/// input it must leave untouched, and the frames and options it refuses.
/// </summary>
public sealed class CvtColorExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_with_a_colour_frame_converts_it_to_greyscale()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = new Mat(8, 8, MatType.CV_8UC3, Scalar.All(200));
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new CvtColorExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.CvtColorTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.ConversionParameter, OpenCvNodeIds.ConversionGray)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ConvertedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // The three channels carry the same value, so the greyscale value is that
        // value whatever weights the conversion applies.
        preview.Width.ShouldBe(8);
        preview.Height.ShouldBe(8);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);
        preview.Pixels.ShouldAllBe(pixel => pixel == (byte)200);

        // The input is a frame the producer owns: the node read it and never wrote to it.
        input.IsDisposed.ShouldBeFalse();
        mat.At<Vec3b>(0, 0).Item0.ShouldBe((byte)200);

        scope.Owned.ShouldBeEmpty();

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_greyscale_input_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(4, 4, MatType.CV_8UC1, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new CvtColorExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.CvtColorTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.ConversionParameter, OpenCvNodeIds.ConversionGray)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("Gray8");
        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_an_unknown_conversion_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(4, 4, MatType.CV_8UC3, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new CvtColorExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.CvtColorTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.ConversionParameter, "yuv")),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.NodeExecutionFailed);
        result.Diagnostics[0].Message.ShouldContain(OpenCvNodeIds.ConversionParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_creates_no_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(4, 4, MatType.CV_8UC3, Scalar.All(20)), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new CvtColorExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.CvtColorTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.ConversionParameter, OpenCvNodeIds.ConversionGray)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }
}
