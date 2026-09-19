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

namespace VisionWeave.IntegrationTests.OpenCv;

/// <summary>
/// The resize node on its own: the geometry it produces, the pixel layout it
/// preserves, and the parameters it rejects.
/// </summary>
public sealed class ResizeExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_produces_the_requested_size_and_keeps_the_pixel_format()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(32, 64, MatType.CV_8UC3, Scalar.All(30)), ledger);

        NodeExecutionResult result = await new ResizeExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.ResizeTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.WidthParameter, 16),
                    (OpenCvNodeIds.HeightParameter, 8),
                    (OpenCvNodeIds.InterpolationParameter, OpenCvNodeIds.InterpolationArea)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ResizedPortId);
        produced.Width.ShouldBe(16);
        produced.Height.ShouldBe(8);
        produced.PixelFormat.ShouldBe(FramePixelFormat.Bgr24);
        produced.IsDisposed.ShouldBeFalse();

        scope.Owned.ShouldBeEmpty();
        input.IsDisposed.ShouldBeFalse();

        produced.Dispose();
        input.Dispose();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_without_an_interpolation_option_uses_the_area_default()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC1, Scalar.All(30)), ledger);

        NodeExecutionResult result = await new ResizeExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.ResizeTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.WidthParameter, 4),
                    (OpenCvNodeIds.HeightParameter, 4)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ResizedPortId);
        produced.Width.ShouldBe(4);
        produced.Height.ShouldBe(4);

        produced.Dispose();
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_with_an_unknown_interpolation_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC1, Scalar.All(30)), ledger);

        NodeExecutionResult result = await new ResizeExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.ResizeTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.WidthParameter, 4),
                    (OpenCvNodeIds.HeightParameter, 4),
                    (OpenCvNodeIds.InterpolationParameter, "lanczos")),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.NodeExecutionFailed);
        result.Diagnostics[0].Message.ShouldContain(OpenCvNodeIds.InterpolationParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(OpenCvParameterBounds.MaxDimension + 1, 4)]
    public async Task ExecuteAsync_with_a_dimension_out_of_range_fails_with_a_diagnostic(int width, int height)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC1, Scalar.All(30)), ledger);

        NodeExecutionResult result = await new ResizeExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.ResizeTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.WidthParameter, width),
                    (OpenCvNodeIds.HeightParameter, height)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.WidthParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_creates_no_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC1, Scalar.All(30)), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new ResizeExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.ResizeTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.WidthParameter, 4),
                    (OpenCvNodeIds.HeightParameter, 4)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }
}
