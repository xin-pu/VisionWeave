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
/// The pyramid down node on its own: the size it derives, the values it keeps, the
/// layouts it takes, and the frame it releases when the run stops.
/// </summary>
public sealed class PyrDownExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_halves_a_frame_and_keeps_its_values_without_modifying_the_input()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC1, Scalar.All(120)), ledger);

        NodeExecutionResult result = await new PyrDownExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.PyrDownTypeId,
                ExecutorTestRequest.Parameters(),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ReducedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(4);
        preview.Height.ShouldBe(4);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);

        // The frame holds one value, and the smoothing a pyramid applies before it
        // decimates a flat frame leaves it flat.
        preview.Pixels.ShouldAllBe(pixel => pixel == (byte)120);

        input.IsDisposed.ShouldBeFalse();
        scope.Owned.ShouldBeEmpty();

        produced.Dispose();
        input.Dispose();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_halves_a_frame_with_an_odd_side_rounding_the_size_up()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(7, 7, MatType.CV_8UC1, Scalar.All(120)), ledger);

        NodeExecutionResult result = await new PyrDownExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.PyrDownTypeId,
                ExecutorTestRequest.Parameters(),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ReducedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // Seven texels cannot be halved into a whole number, so the size is the one
        // OpenCV derives: the smaller frame is never smaller than the input allows.
        preview.Width.ShouldBe(4);
        preview.Height.ShouldBe(4);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_keeps_the_layout_of_a_colour_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC3, Scalar.All(120)), ledger);

        NodeExecutionResult result = await new PyrDownExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.PyrDownTypeId,
                ExecutorTestRequest.Parameters(),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ReducedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.PixelFormat.ShouldBe(FramePixelFormat.Bgr24);
        preview.Pixels.Length.ShouldBe(4 * 4 * 3);
        preview.Pixels.ShouldAllBe(pixel => pixel == (byte)120);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_creates_no_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC1, Scalar.All(20)), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new PyrDownExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.PyrDownTypeId,
                ExecutorTestRequest.Parameters(),
                ExecutorTestRequest.ImageInput(input),
                scope),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }
}
