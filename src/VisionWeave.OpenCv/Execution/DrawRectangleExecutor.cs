using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Marks a rectangle on an image and reports the marked frame. The rectangle is
/// stated the way the crop node states one, so the region a user keeps and the region
/// a user marks are the same four numbers, and one that reaches past an edge is drawn
/// up to it, which is what marking a region at the border means.
/// <para>
/// The frame is copied and marked rather than marked where it lies: a frame in this
/// build has one owner, and drawing into the one the node was given would change a
/// value another node may still read. Marking reads pixels and writes the same type
/// back, so every layout is accepted and the one it was given is reported.
/// </para>
/// </summary>
public sealed class DrawRectangleExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public DrawRectangleExecutor(ILeaseLedger ledger)
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

        if (!DrawingStyle.TryReadRectangle(request, out Rect rectangle, out NodeDiagnostic? rectangleFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(rectangleFailure!));
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
            Cv2.Rectangle(output, rectangle, colour, thickness);

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
}
