using OpenCvSharp;
using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.IntegrationTests.Support;
using VisionWeave.OpenCv.Execution;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;
using DrawingPoint = System.Drawing.Point;

namespace VisionWeave.IntegrationTests.OpenCv;

/// <summary>
/// The draw contours node on its own: which pixels a set of contours marks, what a
/// filled draw covers that an outlined one does not, the colour it writes, and the frame
/// it leaves behind. The contours are stated as points rather than found, so a test says
/// what it draws and reads the picture back.
/// </summary>
public sealed class DrawContoursExecutorTests
{
    private const string NothingMarked =
        """
        ........
        ........
        ........
        ........
        ........
        ........
        ........
        ........
        """;

    [Fact]
    public async Task ExecuteAsync_marks_the_outline_of_every_contour_it_was_given()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        Mat plate = FeatureFrame.Flat();
        MatFrameLease input = MatFrameLease.Create(plate, ledger);

        NodeExecutionResult result = await new DrawContoursExecutor(ledger).ExecuteAsync(
            Request(
                scope,
                input,
                Contours(Square(x: 2, y: 2, right: 5, bottom: 4), Square(x: 5, y: 5, right: 7, bottom: 7))),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // Both contours are marked, each as its own closed path: a set of found shapes is
        // drawn as the shapes, rather than as one path through all of their points.
        MarkedFrame.ShouldBe(
            produced,
            """
            ........
            ........
            ..####..
            ..#..#..
            ..####..
            .....###
            .....#.#
            .....###
            """);

        // The frame the node was given is not the frame it marked, so the one its owner
        // still holds is untouched and the scope holds nothing of the node's.
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
    public async Task ExecuteAsync_with_a_filled_draw_marks_the_inside_of_every_contour()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawContoursExecutor(ledger).ExecuteAsync(
            Request(
                scope,
                input,
                Contours(Square(x: 2, y: 2, right: 5, bottom: 4), Square(x: 5, y: 5, right: 7, bottom: 7)),
                filled: true),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // The same points with the switch turned on cover the inside of each shape: the
        // difference between the two pictures is what the switch is for.
        MarkedFrame.ShouldBe(
            produced,
            """
            ........
            ........
            ..####..
            ..####..
            ..####..
            .....###
            .....###
            .....###
            """);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_filled_draw_marks_a_contour_that_is_one_pixel_as_that_pixel()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawContoursExecutor(ledger).ExecuteAsync(
            Request(scope, input, Contours(new Contour([new DrawingPoint(3, 3)])), filled: true),
            CancellationToken.None);

        // A mask that holds a single pixel is an ordinary answer from the node that finds
        // contours, and a shape of one pixel has no inside: it is marked where it is
        // rather than the run stopping on a polygon that cannot be filled.
        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        MarkedFrame.ShouldBe(
            produced,
            """
            ........
            ........
            ........
            ...#....
            ........
            ........
            ........
            ........
            """);

        produced.Dispose();
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_writes_the_colour_it_was_given_component_by_component()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC3, Scalar.All(0)), ledger);

        NodeExecutionResult result = await new DrawContoursExecutor(ledger).ExecuteAsync(
            Request(scope, input, filled: true, blue: 10, green: 20, red: 30),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // The three components are written where the parameters name them, so the first
        // is the blue channel of a frame that holds blue, green, and red.
        using Mat marked = produced.CloneWritable();
        Vec3b mark = marked.At<Vec3b>(3, 3);
        mark.Item0.ShouldBe((byte)10);
        mark.Item1.ShouldBe((byte)20);
        mark.Item2.ShouldBe((byte)30);
        marked.At<Vec3b>(0, 0).Item0.ShouldBe((byte)0);

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

        NodeExecutionResult result = await new DrawContoursExecutor(ledger).ExecuteAsync(
            Request(scope, input, filled: true),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);

        // Marking reads pixels and writes the same type back, so the layout that comes out
        // is the layout that went in.
        produced.PixelFormat.ShouldBe(FramePixelFormat.Bgra32);

        produced.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_set_of_no_contours_marks_nothing()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        Mat plate = FeatureFrame.Flat();
        MatFrameLease input = MatFrameLease.Create(plate, ledger);

        NodeExecutionResult result = await new DrawContoursExecutor(ledger).ExecuteAsync(
            Request(scope, input, ContourCollection.Empty),
            CancellationToken.None);

        // A run whose finding node reported no shape still draws, and what it draws is
        // nothing: the frame comes back as it was rather than the node reporting a
        // condition nobody asked it about.
        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.DrawnPortId);
        MarkedFrame.ShouldBe(produced, NothingMarked);

        produced.Dispose();
        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_without_contours_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new DrawContoursExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.DrawContoursTypeId,
                ExecutorTestRequest.Parameters(),
                ExecutorTestRequest.ImageInput(input),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.ContoursPortId);

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

        NodeExecutionResult result = await new DrawContoursExecutor(ledger).ExecuteAsync(
            Request(scope, input, thickness: thickness),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.ThicknessParameter);

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

        NodeExecutionResult result = await new DrawContoursExecutor(ledger).ExecuteAsync(
            Request(scope, input, green: green),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.GreenParameter);

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

        await Should.ThrowAsync<OperationCanceledException>(() => new DrawContoursExecutor(ledger).ExecuteAsync(
            Request(scope, input),
            cancellation.Token));

        ledger.Created.ShouldBe(1);
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    /// <summary>
    /// States one contour as the four corners of an axis-aligned rectangle, which is the
    /// shape a test can read a picture of back.
    /// </summary>
    private static Contour Square(int x, int y, int right, int bottom)
        => new(
        [
            new DrawingPoint(x, y),
            new DrawingPoint(right, y),
            new DrawingPoint(right, bottom),
            new DrawingPoint(x, bottom),
        ]);

    private static ContourCollection Contours(params Contour[] contours) => new(contours);

    /// <summary>
    /// States the square every test that does not care about the shape draws.
    /// </summary>
    private static ContourCollection DefaultContours() => Contours(Square(x: 2, y: 2, right: 5, bottom: 4));

    private static NodeExecutionRequest Request(
        IExecutionResourceScope scope,
        ImageFrameLease input,
        ContourCollection? contours = null,
        bool? filled = null,
        int blue = 255,
        int green = 255,
        int red = 255,
        int thickness = OpenCvParameterBounds.MinThickness)
    {
        List<(string Name, object? Value)> parameters =
        [
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
            OpenCvNodeIds.DrawContoursTypeId,
            ExecutorTestRequest.Parameters([.. parameters]),
            ExecutorTestRequest.ImageInput(input, contours ?? DefaultContours()),
            scope);
    }
}
