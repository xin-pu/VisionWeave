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
/// The draw circle node on its own: which pixels the outline covers, what a filled
/// one covers that an outlined one does not, and the frame it leaves behind. Every
/// case is stated on a frame of one value, so the picture of the result is a picture
/// of what the node wrote.
/// </summary>
public sealed class DrawCircleExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_marks_the_outline_of_the_circle_it_was_given()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        Mat plate = FeatureFrame.Flat();
        MatFrameLease input = MatFrameLease.Create(plate, ledger);

        NodeExecutionResult result = await new DrawCircleExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // An outlined circle covers the pixels a radius of two reaches: the four
        // pixels a whole step from the centre on each axis, and the four a step along
        // one axis and a step along the other reach diagonally.
        MarkedFrame.ShouldBe(
            produced,
            """
            ........
            ........
            ....#...
            ...#.#..
            ..#...#.
            ...#.#..
            ....#...
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
    public async Task ExecuteAsync_with_a_filled_circle_marks_the_whole_of_it()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawCircleExecutor(ledger).ExecuteAsync(
            Request(scope, input, filled: true),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // Filling covers the pixels the outline encloses: the same shape with the row
        // it spans on each side of the centre run end to end.
        MarkedFrame.ShouldBe(
            produced,
            """
            ........
            ........
            ....#...
            ...###..
            ..#####.
            ...###..
            ....#...
            ........
            """);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_radius_of_zero_marks_the_pixel_the_centre_names()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawCircleExecutor(ledger).ExecuteAsync(
            Request(scope, input, x: 5, y: 6, radius: 0, filled: true),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // A circle with no radius is the pixel at its centre, which is the smallest
        // mark this node can make and therefore one a radius may ask for.
        MarkedFrame.ShouldBe(
            produced,
            """
            ........
            ........
            ........
            ........
            ........
            ........
            .....#..
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

        NodeExecutionResult result = await new DrawCircleExecutor(ledger).ExecuteAsync(
            Request(scope, input, x: 2, y: 2, radius: 1, filled: true, blue: 10, green: 20, red: 30),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // The mark carries the colour the three components name, in the order the
        // parameters state them, and the rest of the frame is what it was.
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
    public async Task ExecuteAsync_with_a_circle_that_reaches_past_the_edge_marks_what_is_inside()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawCircleExecutor(ledger).ExecuteAsync(
            Request(scope, input, x: 7, y: 7, radius: 3, filled: true),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // Three quarters of the circle are off the frame, and the quarter that is left
        // is drawn: the corner it sits in, with the curved side facing away from it.
        MarkedFrame.ShouldBe(
            produced,
            """
            ........
            ........
            ........
            ........
            .......#
            .....###
            .....###
            ....####
            """);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_circle_entirely_outside_the_frame_marks_nothing()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawCircleExecutor(ledger).ExecuteAsync(
            Request(scope, input, x: 20, y: 20, radius: 3, filled: true),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // A place off the frame is a place with nothing to mark, so the frame comes
        // back as it was rather than the run stopping.
        using Mat marked = produced.CloneWritable();
        Cv2.CountNonZero(marked).ShouldBe(0);
        produced.Height.ShouldBe(8);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_radius_past_its_bound_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawCircleExecutor(ledger).ExecuteAsync(
            Request(scope, input, radius: OpenCvParameterBounds.MaxDimension + 1),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.RadiusParameter);
        ledger.Created.ShouldBe(1);
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_with_a_centre_past_its_bound_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawCircleExecutor(ledger).ExecuteAsync(
            Request(scope, input, y: OpenCvParameterBounds.MaxDimension + 1),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.YParameter);
        input.Dispose();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData((int)OpenCvParameterBounds.MaxLevel + 1)]
    public async Task ExecuteAsync_with_a_colour_component_it_cannot_use_fails_with_a_diagnostic(int green)
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawCircleExecutor(ledger).ExecuteAsync(
            Request(scope, input, green: green),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.GreenParameter);
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

        NodeExecutionResult result = await new DrawCircleExecutor(ledger).ExecuteAsync(
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

        await Should.ThrowAsync<OperationCanceledException>(() => new DrawCircleExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    private static NodeExecutionRequest Request(
        IExecutionResourceScope scope,
        ImageFrameLease input,
        int x = 4,
        int y = 4,
        int radius = 2,
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
            (OpenCvNodeIds.RadiusParameter, radius),
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
            OpenCvNodeIds.DrawCircleTypeId,
            ExecutorTestRequest.Parameters([.. parameters]),
            ExecutorTestRequest.ImageInput(input),
            scope);
    }
}
