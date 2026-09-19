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
/// The Canny node on its own: the edge map it reports where the gradient of a frame
/// is strong enough to be seeded, the map it reports where it is not, the thresholds
/// and aperture it refuses, and the input it leaves untouched.
/// </summary>
public sealed class CannyExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_reports_a_step_the_thresholds_keep_as_a_line_one_pixel_wide()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepFrame.AcrossColumns(50), ledger);

        NodeExecutionResult result = await new CannyExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.EdgesPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        preview.Width.ShouldBe(8);
        preview.Height.ShouldBe(8);
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);

        // The step of fifty reads as a gradient of two hundred, which is above both
        // thresholds, so the whole line survives the hysteresis. The two columns the
        // step runs between carry the same gradient, and keeping one of a pair that
        // reads the same is what makes an edge a line rather than a band, so the map
        // holds the one on the dark side and nothing else.
        for (int row = 0; row < preview.Height; row++)
        {
            for (int column = 0; column < preview.Width; column++)
            {
                bool isTheEdge = column == 3;

                preview.Pixels[(row * preview.Width) + column].ShouldBe(isTheEdge ? (byte)255 : (byte)0);
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
    public async Task ExecuteAsync_of_a_colour_frame_reports_one_channel()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        var mat = new Mat(8, 8, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(4, 0, 4, 8), Scalar.All((byte)50), thickness: -1);
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new CannyExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.EdgesPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // An edge is a place, not a colour, so a frame of three channels is read as
        // one and reported as one. The colour conversion nodes are the ones that
        // move between layouts, and this node does not do their work on the side.
        preview.PixelFormat.ShouldBe(FramePixelFormat.Gray8);
        preview.Pixels[(3 * preview.Width) + 3].ShouldBe((byte)255);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_gradient_the_high_threshold_never_reaches_reports_no_edges()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepFrame.AcrossColumns(25), ledger);

        NodeExecutionResult result = await new CannyExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.EdgesPortId);
        PreviewFrame preview = FramePreviewConverter.Default.Convert(produced);

        // A step of twenty-five reads as a gradient of a hundred: above the low
        // threshold, so the pixel is a candidate, and below the high one, so it is
        // only an edge if an edge is beside it. Nothing in this frame is above the
        // high threshold, so nothing seeds a line and the map comes back empty.
        preview.Pixels.ShouldAllBe(pixel => pixel == 0);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Theory]
    [InlineData(50d, 50d)]
    [InlineData(150d, 50d)]
    public async Task ExecuteAsync_with_a_low_threshold_that_is_not_below_the_high_one_fails_with_a_diagnostic_naming_both(
        double thresholdLow,
        double thresholdHigh)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepFrame.AcrossColumns(50), ledger);

        NodeExecutionResult result = await new CannyExecutor(ledger).ExecuteAsync(
            Request(scope, input, thresholdLow, thresholdHigh),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);

        string message = result.Diagnostics.ShouldHaveSingleItem().Message;
        message.ShouldContain(OpenCvNodeIds.ThresholdLowParameter);
        message.ShouldContain(OpenCvNodeIds.ThresholdHighParameter);

        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Theory]
    [InlineData(-1d, 150d)]
    [InlineData(50d, OpenCvParameterBounds.MaxEdgeThreshold + 1d)]
    public async Task ExecuteAsync_with_a_threshold_outside_the_range_it_can_use_fails_with_a_diagnostic(
        double thresholdLow,
        double thresholdHigh)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepFrame.AcrossColumns(50), ledger);

        NodeExecutionResult result = await new CannyExecutor(ledger).ExecuteAsync(
            Request(scope, input, thresholdLow, thresholdHigh),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.ThresholdHighParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Theory]
    [InlineData(4)]
    [InlineData(OpenCvParameterBounds.MaxCannyApertureSize + 2)]
    public async Task ExecuteAsync_with_an_aperture_it_cannot_use_fails_with_a_diagnostic(int apertureSize)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(StepFrame.AcrossColumns(50), ledger);

        NodeExecutionResult result = await new CannyExecutor(ledger).ExecuteAsync(
            Request(scope, input, apertureSize: apertureSize),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.ApertureSizeParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_with_a_deeper_frame_fails_with_a_diagnostic_naming_it()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_16UC1, Scalar.All(20)), ledger);

        NodeExecutionResult result = await new CannyExecutor(ledger).ExecuteAsync(
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
        MatFrameLease input = MatFrameLease.Create(StepFrame.AcrossColumns(50), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new CannyExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    private static NodeExecutionRequest Request(
        IExecutionResourceScope scope,
        ImageFrameLease input,
        double thresholdLow = 50d,
        double thresholdHigh = 150d,
        int apertureSize = 3)
        => ExecutorTestRequest.For(
            OpenCvNodeIds.CannyTypeId,
            ExecutorTestRequest.Parameters(
                (OpenCvNodeIds.ThresholdLowParameter, thresholdLow),
                (OpenCvNodeIds.ThresholdHighParameter, thresholdHigh),
                (OpenCvNodeIds.ApertureSizeParameter, apertureSize),
                (OpenCvNodeIds.L2GradientParameter, false)),
            ExecutorTestRequest.ImageInput(input),
            scope);
}
