using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;
using DrawingPoint = System.Drawing.Point;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Finds the boundaries of the shapes a mask holds. What comes back is an ordered
/// sequence of points per shape and nothing else: the call also reports which shape
/// encloses which, and the value this node publishes has no field for it, so the
/// retrieval modes that exist to express nesting are not offered rather than accepted
/// and flattened.
/// <para>
/// The node states the layout it works on and refuses anything else with a diagnostic
/// naming it, because a contour is found on the image a decision was expressed in: one
/// channel of eight bits. The call also rewrites the image it is given in some OpenCV
/// builds, so the node hands it a copy of its own and leaves the frame it received
/// untouched, which is what every other node in this layer promises its producer.
/// </para>
/// </summary>
public sealed class FindContoursExecutor : INodeExecutor
{
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

        if (input.PixelFormat != FramePixelFormat.Gray8)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"This node finds contours in a single-channel 8-bit image, but the input is {input.PixelFormat}. "
                    + "Connect the frame a Colour Conversion node reports or the one a Threshold node produces.")));
        }

        if (!TryReadRetrieval(request, out RetrievalModes retrieval, out NodeDiagnostic? retrievalFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(retrievalFailure!));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // The temporary is a copy this executor owns, because the call may rewrite the
        // image it is given and the frame it came from belongs to its producer.
        using var working = new Mat();
        input.Mat.CopyTo(working);

        Cv2.FindContours(working, out Point[][] found, out _, retrieval, ContourApproximationModes.ApproxSimple);

        return Task.FromResult(NodeExecutionResult.Success(
            new Dictionary<string, PortValue>(StringComparer.Ordinal)
            {
                [OpenCvNodeIds.ContoursPortId] = new ContourCollectionValue(ToCollection(found)),
            }));
    }

    /// <summary>
    /// Reports what the call found as the value a document carries. The points are
    /// copied into the contract's own type, so no contour holds a buffer of the call
    /// that produced it.
    /// </summary>
    /// <param name="found">The contours the call reported.</param>
    /// <returns>The immutable contour set.</returns>
    private static ContourCollection ToCollection(Point[][] found)
        => new(found.Select(points => new Contour([.. points.Select(point => new DrawingPoint(point.X, point.Y))])));

    private static bool TryReadRetrieval(
        NodeExecutionRequest request,
        out RetrievalModes retrieval,
        out NodeDiagnostic? failure)
    {
        string option = request.Parameters.Contains(OpenCvNodeIds.RetrievalParameter)
            ? request.Parameters.GetString(OpenCvNodeIds.RetrievalParameter)
            : OpenCvNodeIds.RetrievalExternal;

        RetrievalModes? parsed = option switch
        {
            OpenCvNodeIds.RetrievalExternal => RetrievalModes.External,
            OpenCvNodeIds.RetrievalList => RetrievalModes.List,
            _ => null,
        };

        if (parsed is null)
        {
            retrieval = RetrievalModes.External;
            failure = FrameInput.Rejected(
                request,
                $"Parameter '{OpenCvNodeIds.RetrievalParameter}' must be one of " +
                $"{string.Join(", ", OpenCvNodeIds.RetrievalOptions)}, but it is '{option}'.");
            return false;
        }

        retrieval = parsed.Value;
        failure = null;
        return true;
    }
}
