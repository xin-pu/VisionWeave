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
/// The Sobel node on its own: the gradient it reports at a step, the scale it
/// applies, the orders it refuses, and the input it leaves untouched.
/// </summary>
public sealed class SobelExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_reports_a_gradient_at_a_step_and_nothing_where_the_frame_is_flat()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepFrame.AcrossColumns(10), ledger);

        NodeExecutionResult result = await new SobelExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.GradientPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(8);
        preview.Height.ShouldBe(8);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);

        // The horizontal kernel is minus one, zero, one down the middle row and
        // minus one, zero, one across the outer ones, so a step of ten between two
        // columns is read as ten times the sum of the positive weights: forty, at
        // the two columns the step runs between.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isTheStep = column is 3 or 4;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isTheStep ? (byte)40 : (byte)0);
            }
        }

        input.IsDisposed.ShouldBeFalse();
        scope.Owned.ShouldBeEmpty();

        produced.Dispose();
        input.Dispose();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_scale_reports_the_gradient_multiplied_by_it()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepFrame.AcrossColumns(10), ledger);

        NodeExecutionResult result = await new SobelExecutor(ledger).ExecuteAsync(
            Request(scope, input, scale: 2d),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.GradientPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // The scale is the whole reason the node declares one: it decides how much
        // of the gradient reaches the eight bits a frame reports.
        preview.Pixels[(0 * 8) + 3].ShouldBe((byte)80);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_the_orders_swapped_reports_the_gradient_down_the_rows()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepFrame.DownRows(10), ledger);

        NodeExecutionResult result = await new SobelExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.SobelTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.XOrderParameter, 0),
                    (OpenCvNodeIds.YOrderParameter, 1),
                    (OpenCvNodeIds.KernelSizeParameter, 3),
                    (OpenCvNodeIds.ScaleParameter, 1d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.GradientPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // The frame now steps down its rows, so the response lies on the two rows
        // the step runs between and the columns are flat.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isTheStep = row is 3 or 4;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isTheStep ? (byte)40 : (byte)0);
            }
        }

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_both_orders_zero_fails_with_a_diagnostic_naming_both()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepFrame.AcrossColumns(10), ledger);

        NodeExecutionResult result = await new SobelExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.SobelTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.XOrderParameter, 0),
                    (OpenCvNodeIds.YOrderParameter, 0),
                    (OpenCvNodeIds.KernelSizeParameter, 3),
                    (OpenCvNodeIds.ScaleParameter, 1d)),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);

        string message = result.Diagnostics.ShouldHaveSingleItem().Message;
        message.ShouldContain(OpenCvNodeIds.XOrderParameter);
        message.ShouldContain(OpenCvNodeIds.YOrderParameter);

        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(OpenCvParameterBounds.MaxDerivativeKernelSize + 2)]
    public async Task ExecuteAsync_with_a_kernel_size_it_cannot_use_fails_with_a_diagnostic(int kernelSize)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepFrame.AcrossColumns(10), ledger);

        NodeExecutionResult result = await new SobelExecutor(ledger).ExecuteAsync(
            Request(scope, input, kernelSize: kernelSize),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.KernelSizeParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_with_a_deeper_frame_fails_with_a_diagnostic_naming_it()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_16UC1, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new SobelExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(nameof(FramePixelFormat.Gray16));
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_creates_no_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepFrame.AcrossColumns(10), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new SobelExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    private static NodeExecutionRequest Request(
        IExecutionResourceScope scope,
        ImageFrameLease input,
        int kernelSize = 3,
        double scale = 1d)
        => ExecutorTestRequest.For(
            OpenCvNodeIds.SobelTypeId,
            ExecutorTestRequest.Parameters(
                (OpenCvNodeIds.XOrderParameter, 1),
                (OpenCvNodeIds.YOrderParameter, 0),
                (OpenCvNodeIds.KernelSizeParameter, kernelSize),
                (OpenCvNodeIds.ScaleParameter, scale)),
            ExecutorTestRequest.ImageInput(input),
            scope);
}
