using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Marks a contour set on an image and reports the marked frame. It is the second half
/// of the pair the finding node begins: what one node reports as points, this one puts
/// back on a picture, which is how a run shows what it found.
/// <para>
/// The frame is copied and marked rather than marked where it lies, because a frame in
/// this build has one owner and drawing into the one the node was given would change a
/// value another node may still read. Marking reads pixels and writes the same type
/// back, so every layout is accepted and the one it was given is reported. The colour
/// and the width are the vocabulary the other drawing nodes use, so a mark means the
/// same on all of them.
/// </para>
/// <para>
/// Every contour in the set is marked, because the value is a flat list with no
/// statement about which contour encloses which. A filled draw therefore paints the
/// inside of each contour it is given and cannot leave a hole empty; leaving one empty
/// is a question about nesting, and the finding node does not report nesting.
/// </para>
/// </summary>
public sealed class DrawContoursExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public DrawContoursExecutor(ILeaseLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        _ledger = ledger;
    }

    /// <inheritdoc />
    public Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!FrameInput.TryRead(request, out MatFrameLease input, out NodeDiagnostic? failure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(failure!));
        }

        if (!ContourInput.TryRead(request, out ContourCollection contours, out NodeDiagnostic? contourFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(contourFailure!));
        }

        if (!DrawingStyle.TryReadColour(request, out Scalar colour, out NodeDiagnostic? colourFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(colourFailure!));
        }

        bool filled = DrawingStyle.ReadFilled(request);

        if (!DrawingStyle.TryReadThickness(request, filled, out int thickness, out NodeDiagnostic? thicknessFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(thicknessFailure!));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // The result is a native buffer this executor creates. It is owned by the
        // returned lease once that exists, and by this method until then.
        var output = new Mat();
        bool transferred = false;

        try
        {
            input.Mat.CopyTo(output);

            Point[][] paths = AsPaths(contours);

            if (filled)
            {
                // A filled draw paints the inside of a polygon, so it is one call per
                // contour rather than one call for the set. A polygon needs three points
                // to have an inside, and the set may hold a contour that is a single
                // pixel — an ordinary answer from the node that finds contours — so what
                // cannot be filled is marked as the point it is.
                foreach (Point[] path in paths)
                {
                    if (path.Length >= 3)
                    {
                        Cv2.FillPoly(output, [path], colour);
                    }
                    else
                    {
                        Cv2.DrawContours(output, [path], contourIdx: -1, colour, thickness: 1);
                    }
                }
            }
            else if (paths.Length > 0)
            {
                // A negative index is how a drawing call is told to take the whole set,
                // and no hierarchy is passed because the value has none to pass.
                Cv2.DrawContours(output, paths, contourIdx: -1, colour, thickness);
            }

            MatFrameLease frame = MatFrameLease.Create(output, _ledger);
            transferred = true;

            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.DrawnPortId] = new ImageFrameValue(frame),
                }));
        }
        finally
        {
            if (!transferred)
            {
                output.Dispose();
            }
        }
    }

    /// <summary>
    /// Reads the contour set as the paths a drawing call takes. The call reads points
    /// of its own type, so each contour is copied into the buffer the call walks rather
    /// than the value being reshaped where it is stored.
    /// </summary>
    /// <param name="contours">The contour set to read.</param>
    /// <returns>One path per contour.</returns>
    private static Point[][] AsPaths(ContourCollection contours)
        => [.. contours.Contours.Select(contour => contour.Points.Select(point => new Point(point.X, point.Y)).ToArray())];
}
