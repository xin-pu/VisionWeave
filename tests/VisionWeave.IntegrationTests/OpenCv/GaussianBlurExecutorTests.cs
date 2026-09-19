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
/// The Gaussian blur node on its own: the frame it produces, the input it must
/// leave untouched, the parameters it rejects, and the ownership it transfers.
/// </summary>
public sealed class GaussianBlurExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_smooths_the_frame_without_modifying_the_input()
    {
        LeaseLedger ledger = new();
        var mat = new Mat(8, 8, MatType.CV_8UC1, Scalar.Black);
        mat.Set(4, 4, (byte)255);
        MatFrameLease input = MatFrameLease.Create(mat, ledger);
        var scope = new TestResourceScope();
        var executor = new GaussianBlurExecutor(ledger);

        NodeExecutionResult result = await executor.ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.GaussianBlurTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.KernelSizeParameter, 3),
                    (OpenCvNodeIds.SigmaParameter, 0d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.BlurredPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(8);
        preview.Pixels[(4 * 8) + 4].ShouldBeLessThan((byte)255);
        preview.Pixels[(4 * 8) + 3].ShouldBeGreaterThan((byte)0);

        // The input is a frame the producer owns: the node read it, kept it alive,
        // and never wrote to it.
        input.IsDisposed.ShouldBeFalse();
        mat.At<byte>(4, 4).ShouldBe((byte)255);
        mat.At<byte>(4, 3).ShouldBe((byte)0);

        // The produced buffer belongs to the returned lease, not to the scope, so
        // the runtime does not free it twice.
        scope.Owned.ShouldBeEmpty();

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_an_even_kernel_size_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(4, 4, MatType.CV_8UC1, Scalar.All(20)), ledger);
        NodeExecutionRequest request = ExecutorTestRequest.For(
            OpenCvNodeIds.GaussianBlurTypeId,
            ExecutorTestRequest.Parameters((OpenCvNodeIds.KernelSizeParameter, 4)),
            ExecutorTestRequest.ImageInput(input),
            scope);

        NodeExecutionResult result = await new GaussianBlurExecutor(ledger)
            .ExecuteAsync(request, CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Outputs.ShouldBeEmpty();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.NodeExecutionFailed);
        result.Diagnostics[0].NodeInstanceId.ShouldBe(request.NodeInstanceId);
        result.Diagnostics[0].Message.ShouldContain("odd number");

        // A rejected node creates no frame, and the frame it consumed still belongs
        // to its producer.
        ledger.Created.ShouldBe(1);
        input.IsDisposed.ShouldBeFalse();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_sigma_out_of_range_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(4, 4, MatType.CV_8UC1, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new GaussianBlurExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.GaussianBlurTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.KernelSizeParameter, 3),
                    (OpenCvNodeIds.SigmaParameter, OpenCvParameterBounds.MaxSigma + 1d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.SigmaParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_without_the_required_input_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();

        NodeExecutionResult result = await new GaussianBlurExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.GaussianBlurTypeId,
                ExecutorTestRequest.Parameters((OpenCvNodeIds.KernelSizeParameter, 3)),
                new Dictionary<string, PortValue>(StringComparer.Ordinal),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.ImagePortId);
        ledger.Created.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_value_that_is_not_a_frame_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var inputs = new Dictionary<string, PortValue>(StringComparer.Ordinal)
        {
            [OpenCvNodeIds.ImagePortId] = new BooleanValue(true),
        };

        NodeExecutionResult result = await new GaussianBlurExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.GaussianBlurTypeId,
                ExecutorTestRequest.Parameters((OpenCvNodeIds.KernelSizeParameter, 3)),
                inputs,
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.NodeExecutionFailed);
        ledger.Created.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_creates_no_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(4, 4, MatType.CV_8UC1, Scalar.All(20)), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new GaussianBlurExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.GaussianBlurTypeId,
                ExecutorTestRequest.Parameters((OpenCvNodeIds.KernelSizeParameter, 3)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }
}
