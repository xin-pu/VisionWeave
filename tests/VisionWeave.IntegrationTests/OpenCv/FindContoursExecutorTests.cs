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
/// The find contours node on its own: which shapes a mask is read as holding, where the
/// points of a contour lie, what the two retrieval options differ in, and the frame the
/// node leaves behind. The masks are built from rectangles, so a test can state the
/// boundary it expects from the shape it drew.
/// </summary>
public sealed class FindContoursExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_reports_a_contour_along_the_boundary_of_the_shape()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(Mask(new Rect(2, 2, 4, 3)), ledger);

        NodeExecutionResult result = await new FindContoursExecutor().ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        ContourCollection found = ExecutorTestRequest.ContourOutput(result, OpenCvNodeIds.ContoursPortId);

        // One shape, and the contour is its border: the four corners of the rectangle,
        // because the approximation leaves the ends of a straight run rather than every
        // pixel along it.
        found.Count.ShouldBe(1);
        found.Contours[0].Points.ShouldBe(
            [
                new DrawingPoint(2, 2),
                new DrawingPoint(2, 4),
                new DrawingPoint(5, 2),
                new DrawingPoint(5, 4),
            ],
            ignoreOrder: true);

        input.IsDisposed.ShouldBeFalse();
        input.Dispose();
        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_leaves_the_frame_it_was_given_untouched()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        Mat mask = Mask(new Rect(2, 2, 4, 3));
        MatFrameLease input = MatFrameLease.Create(mask, ledger);

        NodeExecutionResult result = await new FindContoursExecutor().ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        // The call rewrites the image it is given in some OpenCV builds, so the node
        // hands it a copy: the frame its producer still holds is the mask that went in,
        // pixel for pixel, and the node created no lease of its own.
        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);
        Cv2.CountNonZero(mask).ShouldBe(4 * 3);

        input.Dispose();
        ledger.Created.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_reports_the_outer_boundary_of_a_shape_that_has_a_hole_by_default()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(ShapeWithHole(), ledger);

        NodeExecutionResult result = await new FindContoursExecutor().ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        ContourCollection found = ExecutorTestRequest.ContourOutput(result, OpenCvNodeIds.ContoursPortId);

        // The default answers "where is the object", and the hole is part of the shape
        // that surrounds it rather than an object of its own.
        found.Count.ShouldBe(1);
        found.Contours[0].Points.Min(point => point.X).ShouldBe(1);
        found.Contours[0].Points.Max(point => point.X).ShouldBe(6);

        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_reports_the_boundary_inside_a_shape_as_well_when_it_is_asked_for()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(ShapeWithHole(), ledger);

        NodeExecutionResult result = await new FindContoursExecutor().ExecuteAsync(
            Request(scope, input, retrieval: OpenCvNodeIds.RetrievalList),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        ContourCollection found = ExecutorTestRequest.ContourOutput(result, OpenCvNodeIds.ContoursPortId);

        // Two contours over one shape: the boundary of the shape, which reaches its own
        // left edge, and the one the hole put inside it, which starts a pixel in from
        // that edge. The order they arrive in is the order the call found them, so the
        // test reads them as a set rather than as a first and a second.
        found.Count.ShouldBe(2);
        found.Contours
            .Select(contour => contour.Points.Min(point => point.X))
            .Order()
            .ShouldBe([1, 2]);

        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_reports_nothing_for_a_mask_that_holds_no_shape()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new FindContoursExecutor().ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        // A frame with nothing in it is a mask without shapes, which is an answer rather
        // than a failure: a user watching a threshold find nothing has learned something
        // about the picture.
        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);
        ExecutorTestRequest.ContourOutput(result, OpenCvNodeIds.ContoursPortId).Count.ShouldBe(0);

        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_with_a_frame_of_more_than_one_channel_names_the_layout_it_needs()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(new Mat(8, 8, MatType.CV_8UC3, Scalar.All(0)), ledger);

        NodeExecutionResult result = await new FindContoursExecutor().ExecuteAsync(
            Request(scope, input),
            CancellationToken.None);

        // The node states what it works on rather than letting the call fail on it,
        // because the layout a mask has is a thing the document can be checked against.
        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("Bgr24");

        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_without_an_image_fails_with_a_diagnostic()
    {
        var scope = new TestResourceScope();

        NodeExecutionResult result = await new FindContoursExecutor().ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.FindContoursTypeId,
                ExecutorTestRequest.Parameters(),
                new Dictionary<string, PortValue>(StringComparer.Ordinal),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.ImagePortId);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_retrieval_it_cannot_use_fails_with_a_diagnostic()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(FeatureFrame.Flat(), ledger);

        NodeExecutionResult result = await new FindContoursExecutor().ExecuteAsync(
            Request(scope, input, retrieval: "tree"),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.RetrievalParameter);

        input.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws()
    {
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = MatFrameLease.Create(Mask(new Rect(2, 2, 4, 3)), ledger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new FindContoursExecutor().ExecuteAsync(
            Request(scope, input),
            cancellation.Token));

        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    /// <summary>
    /// Builds the mask the tests read: a frame of no shape, with the rectangles that
    /// are drawn into it as filled regions.
    /// </summary>
    private static Mat Mask(Rect? shape = null, Rect? hole = null)
    {
        var mask = new Mat(8, 8, MatType.CV_8UC1, Scalar.All(0));

        if (shape is not null)
        {
            Cv2.Rectangle(mask, shape.Value, Scalar.All(255), thickness: -1);
        }

        if (hole is not null)
        {
            Cv2.Rectangle(mask, hole.Value, Scalar.All(0), thickness: -1);
        }

        return mask;
    }

    /// <summary>
    /// Builds a shape with a hole in it, which is what tells the two retrieval options
    /// apart: the hole is a boundary inside the shape rather than a shape of its own.
    /// </summary>
    private static Mat ShapeWithHole() => Mask(new Rect(1, 1, 6, 6), new Rect(3, 3, 2, 2));

    private static NodeExecutionRequest Request(
        IExecutionResourceScope scope,
        ImageFrameLease input,
        string? retrieval = null)
    {
        List<(string Name, object? Value)> parameters = [];

        if (retrieval is not null)
        {
            parameters.Add((OpenCvNodeIds.RetrievalParameter, retrieval));
        }

        return ExecutorTestRequest.For(
            OpenCvNodeIds.FindContoursTypeId,
            ExecutorTestRequest.Parameters([.. parameters]),
            ExecutorTestRequest.ImageInput(input),
            scope);
    }
}
