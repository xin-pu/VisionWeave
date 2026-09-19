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
/// The erode node on its own: the pixel a bright region loses, the repeats that take
/// one more each time, the element that changes nothing, the parameters it refuses,
/// and the input it leaves untouched.
/// </summary>
public sealed class ErodeExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_removes_what_the_element_cannot_fit_inside_a_bright_region()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = FeatureFrame.AcrossColumns(40);
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new ErodeExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ErodedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(8);
        preview.Height.ShouldBe(8);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);

        // The bright side of the step starts at column four, and a pixel survives
        // only if the whole three by three around it is bright, so column four goes
        // dark and five onwards stay: the region loses the one column the element
        // cannot fit on. The right edge stays bright because the border repeats the
        // frame's own pixels rather than darkening it.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isBright = column >= 5;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isBright ? (byte)40 : (byte)0);
            }
        }

        input.IsDisposed.ShouldBeFalse();
        mat.At<byte>(0, 4).ShouldBe((byte)40);
        scope.Owned.ShouldBeEmpty();

        produced.Dispose();
        input.Dispose();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_more_iterations_takes_one_more_column_for_each_of_them()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(40), ledger);

        NodeExecutionResult result = await new ErodeExecutor(ledger).ExecuteAsync(
            Request(scope, input, iterations: 2),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ErodedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // An iteration is the element applied again to its own result, so the region
        // gives up a second column: what survives is what the element fits inside
        // twice.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isBright = column >= 6;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isBright ? (byte)40 : (byte)0);
            }
        }

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_an_element_of_one_pixel_reports_the_frame_unchanged()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(40), ledger);

        NodeExecutionResult result = await new ErodeExecutor(ledger).ExecuteAsync(
            Request(scope, input, size: OpenCvParameterBounds.MinKernelSize),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ErodedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // An element of one pixel is the pixel itself, and a pixel is always inside
        // its own element, so the frame comes back as it arrived.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isBright = column >= 4;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isBright ? (byte)40 : (byte)0);
            }
        }

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_of_a_frame_of_one_value_reports_it_unchanged()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC1, Scalar.All(120)), ledger);

        NodeExecutionResult result = await new ErodeExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ErodedPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // Every pixel has the whole element around it once the border repeats the
        // frame's own pixels, so nothing is removed. A border of black would have
        // eaten an edge off the frame that the user never asked it to touch.
        preview.Pixels.ShouldAllBe(pixel => pixel == 120);

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
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(40), ledger);

        NodeExecutionResult result = await new ErodeExecutor(ledger).ExecuteAsync(
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
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(40), ledger);

        NodeExecutionResult result = await new ErodeExecutor(ledger).ExecuteAsync(
            Request(scope, input, iterations: iterations),
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
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(40), ledger);

        NodeExecutionResult result = await new ErodeExecutor(ledger).ExecuteAsync(
            Request(scope, input, shape: "diamond"),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);

        string message = result.Diagnostics.ShouldHaveSingleItem().Message;
        message.ShouldContain(OpenCvNodeIds.KernelShapeParameter);
        message.ShouldContain("diamond");

        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_creates_no_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.AcrossColumns(40), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new ErodeExecutor(ledger).ExecuteAsync(
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
            OpenCvNodeIds.ErodeTypeId,
            ExecutorTestRequest.Parameters(
                (OpenCvNodeIds.KernelShapeParameter, shape),
                (OpenCvNodeIds.KernelSizeParameter, size),
                (OpenCvNodeIds.IterationsParameter, iterations)),
            ExecutorTestRequest.ImageInput(input),
            scope);
}
