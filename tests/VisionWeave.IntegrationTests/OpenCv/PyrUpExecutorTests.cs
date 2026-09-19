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
/// The pyramid up node on its own: the size it derives, the values it keeps, the
/// layouts it takes, and the round trip it does not promise.
/// </summary>
public sealed class PyrUpExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_doubles_a_frame_and_keeps_its_values_without_modifying_the_input()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(4, 4, MatType.CV_8UC1, Scalar.All(120)), ledger);

        NodeExecutionResult result = await new PyrUpExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.PyrUpTypeId,
                ExecutorTestRequest.Parameters(),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.EnlargedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(8);
        preview.Height.ShouldBe(8);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);

        // The interpolation weights a flat frame back to the value it already had,
        // borders included.
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
    public async Task ExecuteAsync_keeps_the_layout_of_a_colour_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(4, 4, MatType.CV_8UC3, Scalar.All(120)), ledger);

        NodeExecutionResult result = await new PyrUpExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.PyrUpTypeId,
                ExecutorTestRequest.Parameters(),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.EnlargedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.PixelFormat.ShouldBe(FramePixelFormat.Bgr24);
        preview.Pixels.Length.ShouldBe(8 * 8 * 3);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_bring_back_the_frame_halving_reduced()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = new Mat(8, 8, MatType.CV_8UC1, Scalar.Black);
        mat.Set(4, 4, (byte)255);
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult reduced = await new PyrDownExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.PyrDownTypeId,
                ExecutorTestRequest.Parameters(),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        MatFrameLease half = ExecutorTestRequest.ImageOutput(reduced, OpenCvNodeIds.ReducedPortId);

        NodeExecutionResult enlarged = await new PyrUpExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.PyrUpTypeId,
                ExecutorTestRequest.Parameters(),
                ExecutorTestRequest.ImageInput(half),
                scope),
            CancellationToken.None);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(enlarged, OpenCvNodeIds.EnlargedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(8);
        preview.Height.ShouldBe(8);

        // Halving smoothed the spike into its neighbourhood and doubling interpolated
        // that smoothed frame, so the round trip is larger but not the same: the pixel
        // it brightened is now the value the smoothing left there.
        preview.Pixels[(4 * 8) + 4].ShouldBeLessThan((byte)255);
        preview.Pixels[(4 * 8) + 4].ShouldBeGreaterThan((byte)0);

        produced.Dispose();
        half.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_creates_no_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(4, 4, MatType.CV_8UC1, Scalar.All(20)), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new PyrUpExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.PyrUpTypeId,
                ExecutorTestRequest.Parameters(),
                ExecutorTestRequest.ImageInput(input),
                scope),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }
}
