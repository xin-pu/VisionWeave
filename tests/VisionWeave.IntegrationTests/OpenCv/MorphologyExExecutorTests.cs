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
/// The morphology ex node on its own: what each of the five operations reports, the
/// parameters it refuses, and the input it leaves untouched. Opening, closing, and
/// the two hats are stated against the smallest frame each one acts on: a single
/// pixel that is smaller than the element, and a single gap that is.
/// </summary>
public sealed class MorphologyExExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_with_the_open_operation_removes_what_is_smaller_than_the_element()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = FeatureFrame.Spike(255);
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new MorphologyExExecutor(ledger).ExecuteAsync(
            Request(scope, input, OpenCvNodeIds.MorphologyOpen),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.MorphedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // An opening erodes and then dilates, so a bright pixel that the element
        // cannot fit around is removed by the first half and nothing is left for the
        // second half to grow back: the spike is gone.
        preview.Pixels.ShouldAllBe(pixel => pixel == 0);

        input.IsDisposed.ShouldBeFalse();
        mat.At<byte>(4, 4).ShouldBe((byte)255);
        scope.Owned.ShouldBeEmpty();

        produced.Dispose();
        input.Dispose();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_the_close_operation_fills_what_is_smaller_than_the_element()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Hole(0, field: 200), ledger);

        NodeExecutionResult result = await new MorphologyExExecutor(ledger).ExecuteAsync(
            Request(scope, input, OpenCvNodeIds.MorphologyClose),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.MorphedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // A closing dilates and then erodes, so a dark pixel that the element covers
        // entirely is filled by the first half and cannot be cut back out by the
        // second: the hole is closed at the value of the frame around it.
        preview.Pixels.ShouldAllBe(pixel => pixel == 200);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_the_gradient_operation_reports_the_outline_of_a_step()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(40), ledger);

        NodeExecutionResult result = await new MorphologyExExecutor(ledger).ExecuteAsync(
            Request(scope, input, OpenCvNodeIds.MorphologyGradient),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.MorphedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // The gradient is the dilation less the erosion, so it is the band the two
        // disagree about: the column the erosion removed beside the column the
        // dilation added, which is the step's outline two pixels wide.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isTheOutline = column is 3 or 4;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isTheOutline ? (byte)40 : (byte)0);
            }
        }

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_the_top_hat_operation_reports_what_the_opening_removed()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Spike(255), ledger);

        NodeExecutionResult result = await new MorphologyExExecutor(ledger).ExecuteAsync(
            Request(scope, input, OpenCvNodeIds.MorphologyTopHat),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.MorphedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // The top hat is the frame less its own opening, so the one thing the opening
        // removed is the one thing it reports.
        preview.Pixels[(4 * 8) + 4].ShouldBe((byte)255);
        preview.Pixels.Count(pixel => pixel != 0).ShouldBe(1);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_the_black_hat_operation_reports_what_the_closing_filled()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Hole(0, field: 200), ledger);

        NodeExecutionResult result = await new MorphologyExExecutor(ledger).ExecuteAsync(
            Request(scope, input, OpenCvNodeIds.MorphologyBlackHat),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.MorphedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // The black hat is the frame's closing less the frame, so the hole it filled
        // is reported at the value the filling gave it.
        preview.Pixels[(4 * 8) + 4].ShouldBe((byte)200);
        preview.Pixels.Count(pixel => pixel != 0).ShouldBe(1);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Theory]
    [InlineData("hit-miss")]
    [InlineData("erode")]
    public async Task ExecuteAsync_with_an_operation_it_does_not_know_fails_with_a_diagnostic_naming_it(string operation)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Spike(255), ledger);

        NodeExecutionResult result = await new MorphologyExExecutor(ledger).ExecuteAsync(
            Request(scope, input, operation),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);

        string message = result.Diagnostics.ShouldHaveSingleItem().Message;
        message.ShouldContain(OpenCvNodeIds.OperationParameter);
        message.ShouldContain(operation);

        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(OpenCvParameterBounds.MaxKernelSize + 1)]
    public async Task ExecuteAsync_with_a_kernel_size_it_cannot_use_fails_with_a_diagnostic(int kernelSize)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Spike(255), ledger);

        NodeExecutionResult result = await new MorphologyExExecutor(ledger).ExecuteAsync(
            Request(scope, input, OpenCvNodeIds.MorphologyOpen, size: kernelSize),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.KernelSizeParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(OpenCvParameterBounds.MaxIterations + 1)]
    public async Task ExecuteAsync_with_an_iteration_count_it_cannot_use_fails_with_a_diagnostic(int iterations)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Spike(255), ledger);

        NodeExecutionResult result = await new MorphologyExExecutor(ledger).ExecuteAsync(
            Request(scope, input, OpenCvNodeIds.MorphologyOpen, iterations: iterations),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.IterationsParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_with_a_shape_it_does_not_know_fails_with_a_diagnostic_naming_it()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Spike(255), ledger);

        NodeExecutionResult result = await new MorphologyExExecutor(ledger).ExecuteAsync(
            Request(scope, input, OpenCvNodeIds.MorphologyOpen, shape: "diamond"),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.KernelShapeParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_creates_no_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Spike(255), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new MorphologyExExecutor(ledger).ExecuteAsync(
            Request(scope, input, OpenCvNodeIds.MorphologyOpen),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    private static NodeExecutionRequest Request(
        IExecutionResourceScope scope,
        ImageFrameLease input,
        string operation,
        string shape = OpenCvNodeIds.KernelShapeRect,
        int size = 3,
        int iterations = OpenCvParameterBounds.MinIterations)
        => ExecutorTestRequest.For(
            OpenCvNodeIds.MorphologyExTypeId,
            ExecutorTestRequest.Parameters(
                (OpenCvNodeIds.OperationParameter, operation),
                (OpenCvNodeIds.KernelShapeParameter, shape),
                (OpenCvNodeIds.KernelSizeParameter, size),
                (OpenCvNodeIds.IterationsParameter, iterations)),
            ExecutorTestRequest.ImageInput(input),
            scope);
}
