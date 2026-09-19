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
/// The Laplacian node on its own: the response it reports at an isolated pixel, the
/// wider response the apertures above one measure, the scale it applies, the frames
/// and kernel sizes it refuses, and the input it leaves untouched.
/// </summary>
public sealed class LaplacianExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_with_the_narrowest_aperture_reports_a_spike_at_the_pixel_and_its_neighbours()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = Spike(20);
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new LaplacianExecutor(ledger).ExecuteAsync(
            Request(scope, input, kernelSize: OpenCvParameterBounds.MinKernelSize),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.GradientPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(8);
        preview.Height.ShouldBe(8);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);

        // The five point kernel takes four times the middle of the window away from
        // its four neighbours, so a spike of twenty that stands out of a flat frame
        // reads as four times twenty at the pixel itself and as twenty beside it,
        // which is what the absolute value of a signed response looks like.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                int distance = Math.Abs(row - 4) + Math.Abs(column - 4);
                byte expected = distance switch
                {
                    0 => 80,
                    1 => 20,
                    _ => 0,
                };

                preview.Pixels[(row * preview.Width) + column].ShouldBe(expected);
            }
        }

        input.IsDisposed.ShouldBeFalse();
        mat.At<byte>(4, 4).ShouldBe((byte)20);
        scope.Owned.ShouldBeEmpty();

        produced.Dispose();
        input.Dispose();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_wider_aperture_reports_a_spike_at_its_diagonals_too()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(Spike(20), ledger);

        NodeExecutionResult result = await new LaplacianExecutor(ledger).ExecuteAsync(
            Request(scope, input, kernelSize: 3),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.GradientPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // OpenCV measures an aperture above one as the two second derivative Sobel
        // operators added together, which is a kernel of two at each diagonal, eight
        // at the middle and nothing beside it: the same spike reads as eight times
        // twenty at the pixel and as twice twenty at its diagonals.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                int rowDistance = Math.Abs(row - 4);
                int columnDistance = Math.Abs(column - 4);
                byte expected = (rowDistance, columnDistance) switch
                {
                    (0, 0) => 160,
                    (1, 1) => 40,
                    _ => 0,
                };

                preview.Pixels[(row * preview.Width) + column].ShouldBe(expected);
            }
        }

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_reports_a_step_at_both_of_the_columns_it_runs_between()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(40), ledger);

        NodeExecutionResult result = await new LaplacianExecutor(ledger).ExecuteAsync(
            Request(scope, input, kernelSize: OpenCvParameterBounds.MinKernelSize),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.GradientPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // A step of forty finds a response of plus forty on one side of it and of
        // minus forty on the other, so a frame that could hold the sign would show
        // the step as a dark line beside a bright one. The eight bits the node
        // reports are unsigned, and the line it reports is the same weight on both
        // sides of the step, which is what taking the absolute value does.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isTheStep = column is 3 or 4;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isTheStep ? (byte)40 : (byte)0);
            }
        }

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_scale_reports_the_response_multiplied_by_it()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(Spike(20), ledger);

        NodeExecutionResult result = await new LaplacianExecutor(ledger).ExecuteAsync(
            Request(scope, input, kernelSize: OpenCvParameterBounds.MinKernelSize, scale: 2d),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.GradientPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Pixels[(4 * 8) + 4].ShouldBe((byte)160);
        preview.Pixels[(4 * 8) + 3].ShouldBe((byte)40);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(OpenCvParameterBounds.MaxDerivativeKernelSize + 2)]
    public async Task ExecuteAsync_with_a_kernel_size_it_cannot_use_fails_with_a_diagnostic(int kernelSize)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(20), ledger);

        NodeExecutionResult result = await new LaplacianExecutor(ledger).ExecuteAsync(
            Request(scope, input, kernelSize),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.KernelSizeParameter);
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
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(20), ledger);

        NodeExecutionResult result = await new LaplacianExecutor(ledger).ExecuteAsync(
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

        NodeExecutionResult result = await new LaplacianExecutor(ledger).ExecuteAsync(
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
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(20), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new LaplacianExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    private static NodeExecutionRequest Request(
        IExecutionResourceScope scope,
        ImageFrameLease input,
        int kernelSize = OpenCvParameterBounds.MinKernelSize,
        double scale = 1d)
        => ExecutorTestRequest.For(
            OpenCvNodeIds.LaplacianTypeId,
            ExecutorTestRequest.Parameters(
                (OpenCvNodeIds.KernelSizeParameter, kernelSize),
                (OpenCvNodeIds.ScaleParameter, scale)),
            ExecutorTestRequest.ImageInput(input),
            scope);

    /// <summary>
    /// Builds a frame that is flat and black but for one pixel in the middle, which
    /// is what a second derivative answers at most sharply.
    /// </summary>
    private static Mat Spike(byte value)
    {
        var mat = new Mat(8, 8, MatType.CV_8UC1, Scalar.Black);
        mat.Set(4, 4, value);

        return mat;
    }
}
