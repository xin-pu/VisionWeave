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
/// The draw rectangle node on its own: which pixels the rectangle covers, what a
/// filled one covers that an outlined one does not, the colour it writes, and the
/// frame it leaves behind. Every case is stated on a frame of one value, so the
/// picture of the result is a picture of what the node wrote.
/// </summary>
public sealed class DrawRectangleExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_marks_the_outline_of_the_rectangle_it_was_given()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        Mat plate = FeatureFrame.Flat();
        MatFrameLease input = MatFrameLease.Create(plate, ledger);

        NodeExecutionResult result = await new DrawRectangleExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // An outlined rectangle is its perimeter: the whole of the top and bottom rows
        // the four numbers name, and the two ends of the row between them.
        MarkedFrame.ShouldBe(
            produced,
            """
            ........
            ........
            ..####..
            ..#..#..
            ..####..
            ........
            ........
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
    public async Task ExecuteAsync_with_a_filled_rectangle_marks_the_whole_of_it()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawRectangleExecutor(ledger).ExecuteAsync(
            Request(scope, input, filled: true),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // The same four numbers, with the switch turned on, cover the inside as well:
        // the difference between the two pictures is what the switch is for.
        MarkedFrame.ShouldBe(
            produced,
            """
            ........
            ........
            ..####..
            ..####..
            ..####..
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

        NodeExecutionResult result = await new DrawRectangleExecutor(ledger).ExecuteAsync(
            Request(scope, input, filled: true, blue: 10, green: 20, red: 30),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // The three components are written where the parameters name them, so the
        // first is the blue channel of a frame that holds blue, green, and red.
        using Mat marked = produced.CloneWritable();
        Vec3b mark = marked.At<Vec3b>(2, 2);
        mark.Item0.ShouldBe((byte)10);
        mark.Item1.ShouldBe((byte)20);
        mark.Item2.ShouldBe((byte)30);
        marked.At<Vec3b>(0, 0).Item0.ShouldBe((byte)0);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_on_a_frame_of_one_channel_writes_the_first_component_of_the_colour()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawRectangleExecutor(ledger).ExecuteAsync(
            Request(scope, input, filled: true, blue: 200, green: 20, red: 30),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // A drawing call reads as many components as the frame has channels, so a
        // frame of one channel takes the first: mixing the three into a grey value
        // would be an operation nobody asked for, called drawing.
        using Mat marked = produced.CloneWritable();
        marked.At<byte>(2, 2).ShouldBe((byte)200);
        marked.At<byte>(0, 0).ShouldBe((byte)0);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_reports_a_frame_of_the_layout_it_was_given()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC4, Scalar.All(0)), ledger);

        NodeExecutionResult result = await new DrawRectangleExecutor(ledger).ExecuteAsync(
            Request(scope, input, filled: true),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // Marking reads pixels and writes the same type back, so the layout that comes
        // out is the layout that went in. A mark on a frame that carries an alpha
        // channel is opaque, which is what marking something means; the three colour
        // parameters say nothing about it.
        produced.PixelFormat.ShouldBe(FramePixelFormat.Bgra32);
        using Mat marked = produced.CloneWritable();
        marked.At<Vec4b>(2, 2).Item3.ShouldBe((byte)255);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_rectangle_that_reaches_past_the_edge_marks_what_is_inside()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawRectangleExecutor(ledger).ExecuteAsync(
            Request(scope, input, x: 5, y: 5, width: 6, height: 6),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // The rectangle the four numbers name would end at column ten and row ten,
        // which an eight by eight frame does not have, and the part of it that the
        // frame does have is drawn: the top edge reaches the last column and the two
        // sides stop at the last row.
        MarkedFrame.ShouldBe(
            produced,
            """
            ........
            ........
            ........
            ........
            ........
            .....###
            .....#..
            .....#..
            """);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_rectangle_entirely_outside_the_frame_marks_nothing()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawRectangleExecutor(ledger).ExecuteAsync(
            Request(scope, input, x: 20, y: 20, width: 5, height: 5, filled: true),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // A place off the frame is a place with nothing to mark, so the frame comes
        // back as it was rather than the run stopping: a user who is still choosing
        // where the mark goes is not making a mistake.
        using Mat marked = produced.CloneWritable();
        Cv2.CountNonZero(marked).ShouldBe(0);
        produced.Width.ShouldBe(8);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(OpenCvParameterBounds.MaxDimension + 1)]
    public async Task ExecuteAsync_with_a_width_it_cannot_use_fails_with_a_diagnostic(int width)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawRectangleExecutor(ledger).ExecuteAsync(
            Request(scope, input, width: width),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.WidthParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_with_a_coordinate_past_its_bound_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawRectangleExecutor(ledger).ExecuteAsync(
            Request(scope, input, x: OpenCvParameterBounds.MaxDimension + 1),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.XParameter);
        input.Dispose();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData((int)OpenCvParameterBounds.MaxLevel + 1)]
    public async Task ExecuteAsync_with_a_colour_component_it_cannot_use_fails_with_a_diagnostic(int red)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawRectangleExecutor(ledger).ExecuteAsync(
            Request(scope, input, red: red),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.RedParameter);
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

        NodeExecutionResult result = await new DrawRectangleExecutor(ledger).ExecuteAsync(
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

        await Should.ThrowAsync<OperationCanceledException>(() => new DrawRectangleExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    private static NodeExecutionRequest Request(
        IExecutionResourceScope scope,
        ImageFrameLease input,
        int x = 2,
        int y = 2,
        int width = 4,
        int height = 3,
        int blue = 255,
        int green = 255,
        int red = 255,
        int thickness = OpenCvParameterBounds.MinThickness,
        bool? filled = null)
    {
        List<(string Name, object? Value)> parameters =
        [
            (OpenCvNodeIds.XParameter, x),
            (OpenCvNodeIds.YParameter, y),
            (OpenCvNodeIds.WidthParameter, width),
            (OpenCvNodeIds.HeightParameter, height),
            (OpenCvNodeIds.BlueParameter, blue),
            (OpenCvNodeIds.GreenParameter, green),
            (OpenCvNodeIds.RedParameter, red),
            (OpenCvNodeIds.ThicknessParameter, thickness),
        ];

        if (filled is not null)
        {
            parameters.Add((OpenCvNodeIds.FilledParameter, filled));
        }

        return ExecutorTestRequest.For(
            OpenCvNodeIds.DrawRectangleTypeId,
            ExecutorTestRequest.Parameters([.. parameters]),
            ExecutorTestRequest.ImageInput(input),
            scope);
    }
}
