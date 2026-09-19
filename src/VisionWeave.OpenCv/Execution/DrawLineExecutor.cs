using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Marks a line on an image and reports the marked frame. The line runs between the
/// two points it is given at the width the outline is given, and one that reaches
/// past an edge is drawn up to it. It has no inside, so it declares no switch for
/// filling: a width is the only thing that decides how much of the frame it covers.
/// <para>
/// The frame is copied and marked rather than marked where it lies: a frame in this
/// build has one owner, and drawing into the one the node was given would change a
/// value another node may still read. Marking reads pixels and writes the same type
/// back, so every layout is accepted and the one it was given is reported.
/// </para>
/// </summary>
public sealed class DrawLineExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public DrawLineExecutor(ILeaseLedger ledger)
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

        if (!DrawingStyle.TryReadLine(request, out Point start, out Point end, out NodeDiagnostic? lineFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(lineFailure!));
        }

        if (!DrawingStyle.TryReadColour(request, out Scalar colour, out NodeDiagnostic? colourFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(colourFailure!));
        }

        if (!DrawingStyle.TryReadThickness(request, filled: false, out int thickness, out NodeDiagnostic? thicknessFailure))
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
            Cv2.Line(output, start, end, colour, thickness);

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
