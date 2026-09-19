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
/// The dilate node on its own: the shape a single pixel grows into, the repeats that
/// grow it again, the three structuring elements told apart by what they leave, the
/// layout it reports back, the parameters it refuses, and the input it leaves
/// untouched.
/// </summary>
public sealed class DilateExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_grows_a_single_pixel_into_the_shape_of_the_element()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = FeatureFrame.Spike(255);
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new DilateExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DilatedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(8);
        preview.Height.ShouldBe(8);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);

        // A pixel becomes bright once any pixel of the element is already bright, so
        // the one bright pixel of the frame writes the element itself into the frame:
        // a three by three square, and nothing anywhere else.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isTheElement = Math.Abs(row - 4) <= 1 && Math.Abs(column - 4) <= 1;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isTheElement ? (byte)255 : (byte)0);
            }
        }

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
    public async Task ExecuteAsync_with_more_iterations_grows_the_pixel_by_the_element_each_time()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Spike(255), ledger);

        NodeExecutionResult result = await new DilateExecutor(ledger).ExecuteAsync(
            Request(scope, input, iterations: 2),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DilatedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // An iteration is the element applied to its own result, so the square gains
        // a ring for each one: two iterations of a three by three element make a five
        // by five square.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isTheElement = Math.Abs(row - 4) <= 2 && Math.Abs(column - 4) <= 2;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isTheElement ? (byte)255 : (byte)0);
            }
        }

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Theory]
    [InlineData(OpenCvNodeIds.KernelShapeRect, 25)]
    [InlineData(OpenCvNodeIds.KernelShapeEllipse, 17)]
    [InlineData(OpenCvNodeIds.KernelShapeCross, 9)]
    public async Task ExecuteAsync_of_the_three_shapes_grows_a_pixel_by_a_different_shape(string shape, int expected)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Spike(255), ledger);

        NodeExecutionResult result = await new DilateExecutor(ledger).ExecuteAsync(
            Request(scope, input, shape, size: 5),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DilatedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // The shape of the element is what a single pixel grows into, and the three
        // shapes of the same size fill a different number of pixels: the square
        // fills all twenty-five, the ellipse seventeen, and the cross the nine of
        // its two arms.
        preview.Pixels.Count(pixel => pixel == 255).ShouldBe(expected);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_the_cross_shape_grows_a_pixel_into_a_plus_sign()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Spike(255), ledger);

        NodeExecutionResult result = await new DilateExecutor(ledger).ExecuteAsync(
            Request(scope, input, OpenCvNodeIds.KernelShapeCross, size: 5),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DilatedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // A cross of five is one row and one column of five, which is what the pixel
        // becomes: the corners the square would have filled stay empty.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isTheCross = (row == 4 && Math.Abs(column - 4) <= 2)
                    || (column == 4 && Math.Abs(row - 4) <= 2);

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isTheCross ? (byte)255 : (byte)0);
            }
        }

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_of_a_colour_frame_reports_the_layout_it_was_given()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = new Mat(8, 8, MatType.CV_8UC3, Scalar.Black);
        mat.Set(4, 4, new Vec3b(60, 120, 200));
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new DilateExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DilatedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // A structuring element reads neighbours and writes the type back, so the
        // node neither converts nor narrows: a colour frame comes back a colour
        // frame, with the element grown in every channel, which is three bytes for
        // each of the nine pixels.
        preview.PixelFormat.ShouldBe(FramePixelFormat.Bgr24);
        preview.Pixels.Count(pixel => pixel != 0).ShouldBe(27);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(OpenCvParameterBounds.MaxKernelSize + 1)]
    public async Task ExecuteAsync_with_a_kernel_size_it_cannot_use_fails_with_a_diagnostic(int kernelSize)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Spike(255), ledger);

        NodeExecutionResult result = await new DilateExecutor(ledger).ExecuteAsync(
            Request(scope, input, size: kernelSize),
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

        NodeExecutionResult result = await new DilateExecutor(ledger).ExecuteAsync(
            Request(scope, input, iterations: iterations),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.IterationsParameter);
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

        await Should.ThrowAsync<OperationCanceledException>(() => new DilateExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    private static NodeExecutionRequest Request(
        IExecutionResourceScope scope,
        ImageFrameLease input,
        string shape = OpenCvNodeIds.KernelShapeRect,
        int size = 3,
        int iterations = OpenCvParameterBounds.MinIterations)
        => ExecutorTestRequest.For(
            OpenCvNodeIds.DilateTypeId,
            ExecutorTestRequest.Parameters(
                (OpenCvNodeIds.KernelShapeParameter, shape),
                (OpenCvNodeIds.KernelSizeParameter, size),
                (OpenCvNodeIds.IterationsParameter, iterations)),
            ExecutorTestRequest.ImageInput(input),
            scope);
}
