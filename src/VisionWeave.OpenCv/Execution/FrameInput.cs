using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Reads the image an OpenCV node receives and reports the inputs it cannot work
/// with. The input is a lease owned by the producer, so a read never disposes it
/// and never writes it.
/// </summary>
internal static class FrameInput
{
    /// <summary>
    /// Tries to read the image frame bound to the node's image input port.
    /// </summary>
    /// <param name="request">The request to read from.</param>
    /// <param name="frame">The frame when the input is bound to one.</param>
    /// <param name="failure">The diagnostic to report when the input is unusable.</param>
    /// <returns><see langword="true"/> when the frame was read.</returns>
    internal static bool TryRead(NodeExecutionRequest request, out MatFrameLease frame, out NodeDiagnostic? failure)
        => TryRead(request, OpenCvNodeIds.ImagePortId, out frame, out failure);

    /// <summary>Tries to read an image frame from a specifically named input port.</summary>
    internal static bool TryRead(
        NodeExecutionRequest request,
        string portId,
        out MatFrameLease frame,
        out NodeDiagnostic? failure)
    {
        if (!request.Inputs.TryGetValue(portId, out PortValue? value)
            || value is not ImageFrameValue image)
        {
            frame = null!;
            failure = Rejected(
                request,
                $"The input port '{portId}' is not bound to an image frame.");
            return false;
        }

        if (image.Lease is not MatFrameLease lease)
        {
            frame = null!;
            failure = Rejected(
                request,
                $"The image frame of input port '{portId}' was not created by the OpenCV layer.");
            return false;
        }

        frame = lease;
        failure = null;
        return true;
    }

    /// <summary>
    /// Creates the failure a node reports when it rejects an input or a parameter.
    /// An executor reports an expected failure as a diagnostic rather than throwing
    /// it, so the node ends as failed with a message that names the instance.
    /// </summary>
    /// <param name="request">The request being executed.</param>
    /// <param name="message">The user-facing explanation.</param>
    /// <returns>The diagnostic.</returns>
    internal static NodeDiagnostic Rejected(NodeExecutionRequest request, string message)
        => new(
            DiagnosticCodes.NodeExecutionFailed,
            DiagnosticSeverity.Error,
            message,
            request.NodeInstanceId);

    /// <summary>
    /// Creates the failure a node reports when it rejects an input or a parameter
    /// for a reason the user cannot read off the document, such as a write the
    /// platform refused.
    /// </summary>
    /// <param name="request">The request being executed.</param>
    /// <param name="message">The user-facing explanation, which carries no path or platform detail.</param>
    /// <param name="exception">The original failure, retained for structured logs only.</param>
    /// <returns>The diagnostic.</returns>
    internal static NodeDiagnostic Rejected(NodeExecutionRequest request, string message, Exception exception)
        => new(
            DiagnosticCodes.NodeExecutionFailed,
            DiagnosticSeverity.Error,
            message,
            request.NodeInstanceId,
            exception);
}
