using OpenCvSharp;
using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.IntegrationTests.Support;
using VisionWeave.OpenCv.Execution;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.IntegrationTests.OpenCv;

/// <summary>
/// The draw line node on its own: which pixels a line touches between its two points,
/// what a wider one covers, and the frame it leaves behind. Every case is stated on a
/// frame of one value, so the picture of the result is a picture of what the node
/// wrote.
/// </summary>
public sealed class DrawLineExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_marks_the_pixels_between_the_two_points_it_was_given()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        Mat plate = FeatureFrame.Flat();
        MatFrameLease input = MatFrameLease.Create(plate, ledger);

        NodeExecutionResult result = await new DrawLineExecutor(ledger).ExecuteAsync(
            Request(scope, input, startX: 1, startY: 1, endX: 6, endY: 6),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // A line at a forty-five degree angle touches one pixel per row between its
        // two ends, and both ends are among them.
        MarkedFrame.ShouldBe(
            produced,
            """
            ........
            .#......
            ..#.....
            ...#....
            ....#...
            .....#..
            ......#.
            ........
            """);

        // The frame the node was given is not the frame it marked, so the one its
        // owner still holds is untouched and the scope holds nothing of the node's.
        input.IsDisposed.ShouldBeFalse();
        Cv2.CountNonZero(plate).ShouldBe(0);
        scope.Owned.ShouldBeEmpty();

        produced.Dispose();
        input.Dispose();
        ledger.Created.ShouldBe(2);
        ledger.Released.ShouldBe(2);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_width_of_two_marks_the_band_the_width_covers()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawLineExecutor(ledger).ExecuteAsync(
            Request(scope, input, startX: 0, startY: 3, endX: 7, endY: 3, thickness: 2),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // A width of two marks three rows rather than two: the band is centred on the
        // line and every row it touches is covered, which for a line along the middle
        // of a row is the row itself and one on each side of it.
        MarkedFrame.ShouldBe(
            produced,
            """
            ........
            ........
            ########
            ########
            ########
            ........
            ........
            ........
            """);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_writes_the_colour_it_was_given_component_by_component()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        Mat plate = new(8, 8, MatType.CV_8UC3, Scalar.All(0));
        MatFrameLease input = MatFrameLease.Create(plate, ledger);

        NodeExecutionResult result = await new DrawLineExecutor(ledger).ExecuteAsync(
            Request(scope, input, startX: 0, startY: 0, endX: 7, endY: 0, blue: 10, green: 20, red: 30),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // The line is drawn on the frame the node was given a copy of, at the colour
        // the three components name, and nothing else on that frame changed.
        using Mat marked = produced.CloneWritable();
        Vec3b mark = marked.At<Vec3b>(0, 3);
        mark.Item0.ShouldBe((byte)10);
        mark.Item1.ShouldBe((byte)20);
        mark.Item2.ShouldBe((byte)30);
        marked.At<Vec3b>(1, 0).Item0.ShouldBe((byte)0);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_line_that_leaves_the_frame_marks_what_is_inside()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawLineExecutor(ledger).ExecuteAsync(
            Request(scope, input, startX: 5, startY: 3, endX: 12, endY: 3),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // The far end of the line is off the frame, and the part of it that the frame
        // does have is drawn up to the last column.
        MarkedFrame.ShouldBe(
            produced,
            """
            ........
            ........
            ........
            .....###
            ........
            ........
            ........
            ........
            """);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_line_entirely_outside_the_frame_marks_nothing()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawLineExecutor(ledger).ExecuteAsync(
            Request(scope, input, startX: 20, startY: 20, endX: 30, endY: 30),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // A place off the frame is a place with nothing to mark, so the frame comes
        // back as it was rather than the run stopping.
        using Mat marked = produced.CloneWritable();
        Cv2.CountNonZero(marked).ShouldBe(0);
        produced.Width.ShouldBe(8);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_an_end_past_its_bound_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawLineExecutor(ledger).ExecuteAsync(
            Request(scope, input, endX: OpenCvParameterBounds.MaxDimension + 1),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.EndXParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData((int)OpenCvParameterBounds.MaxLevel + 1)]
    public async Task ExecuteAsync_with_a_colour_component_it_cannot_use_fails_with_a_diagnostic(int blue)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawLineExecutor(ledger).ExecuteAsync(
            Request(scope, input, blue: blue),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.BlueParameter);
        input.Dispose();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(OpenCvParameterBounds.MaxThickness + 1)]
    public async Task ExecuteAsync_with_a_thickness_it_cannot_use_fails_with_a_diagnostic(int thickness)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawLineExecutor(ledger).ExecuteAsync(
            Request(scope, input, thickness: thickness),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.ThicknessParameter);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_creates_no_frame()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new DrawLineExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    private static NodeExecutionRequest Request(
        IExecutionResourceScope scope,
        ImageFrameLease input,
        int startX = 1,
        int startY = 1,
        int endX = 6,
        int endY = 6,
        int blue = 255,
        int green = 255,
        int red = 255,
        int thickness = OpenCvParameterBounds.MinThickness)
        => ExecutorTestRequest.For(
            OpenCvNodeIds.DrawLineTypeId,
            ExecutorTestRequest.Parameters(
                (OpenCvNodeIds.StartXParameter, startX),
                (OpenCvNodeIds.StartYParameter, startY),
                (OpenCvNodeIds.EndXParameter, endX),
                (OpenCvNodeIds.EndYParameter, endY),
                (OpenCvNodeIds.BlueParameter, blue),
                (OpenCvNodeIds.GreenParameter, green),
                (OpenCvNodeIds.RedParameter, red),
                (OpenCvNodeIds.ThicknessParameter, thickness)),
            ExecutorTestRequest.ImageInput(input),
            scope);
}
