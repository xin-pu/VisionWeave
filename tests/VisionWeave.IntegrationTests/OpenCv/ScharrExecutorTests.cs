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
/// The Scharr node on its own: the gradient it reports along one axis, the orders it
/// refuses, and the input it leaves untouched. It is the Sobel node with the three by
/// three kernel that weights the neighbours of a step most, so the reading it takes at
/// a step of one value is four times the reading Sobel takes at the same step.
/// </summary>
public sealed class ScharrExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_weights_a_step_by_sixteen_and_reports_it_where_the_step_is()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(5), ledger);

        NodeExecutionResult result = await new ScharrExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.GradientPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(8);
        preview.Height.ShouldBe(8);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);

        // The Scharr kernel weights the two columns a step runs between by three and
        // by ten, so sixteen weights apply to the brighter side and a step of five
        // makes eighty, at both of the columns the step runs between.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isTheStep = column is 3 or 4;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isTheStep ? (byte)80 : (byte)0);
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
    public async Task ExecuteAsync_with_the_orders_swapped_reports_the_gradient_down_the_rows()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.DownRows(5), ledger);

        NodeExecutionResult result = await new ScharrExecutor(ledger).ExecuteAsync(
            Request(scope, input, xOrder: 0, yOrder: 1),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.GradientPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isTheStep = row is 3 or 4;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isTheStep ? (byte)80 : (byte)0);
            }
        }

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(0, 0)]
    public async Task ExecuteAsync_with_orders_that_are_not_one_apart_fails_with_a_diagnostic_naming_both(
        int xOrder,
        int yOrder)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(5), ledger);

        NodeExecutionResult result = await new ScharrExecutor(ledger).ExecuteAsync(
            Request(scope, input, xOrder, yOrder),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);

        string message = result.Diagnostics.ShouldHaveSingleItem().Message;
        message.ShouldContain(OpenCvNodeIds.XOrderParameter);
        message.ShouldContain(OpenCvNodeIds.YOrderParameter);

        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(OpenCvParameterBounds.MaxScale + 1d)]
    public async Task ExecuteAsync_with_a_scale_it_cannot_use_fails_with_a_diagnostic(double scale)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(5), ledger);

        NodeExecutionResult result = await new ScharrExecutor(ledger).ExecuteAsync(
            Request(scope, input, scale: scale),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.ScaleParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_with_a_deeper_frame_fails_with_a_diagnostic_naming_it()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_16UC1, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new ScharrExecutor(ledger).ExecuteAsync(
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
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(5), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new ScharrExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    private static NodeExecutionRequest Request(
        IExecutionResourceScope scope,
        ImageFrameLease input,
        int xOrder = 1,
        int yOrder = 0,
        double scale = 1d)
        => ExecutorTestRequest.For(
            OpenCvNodeIds.ScharrTypeId,
            ExecutorTestRequest.Parameters(
                (OpenCvNodeIds.XOrderParameter, xOrder),
                (OpenCvNodeIds.YOrderParameter, yOrder),
                (OpenCvNodeIds.ScaleParameter, scale)),
            ExecutorTestRequest.ImageInput(input),
            scope);
}
