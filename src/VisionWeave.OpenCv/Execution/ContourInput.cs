using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Reads the contour set a node receives and reports what it cannot work with. It is
/// the counterpart of <see cref="FrameInput"/> for the other value these nodes pass
/// between them: a set the finding node published and a drawing node consumes.
/// <para>
/// A contour set is immutable and owns no buffer, so a read neither disposes it nor
/// hands out anything a consumer could write.
/// </para>
/// </summary>
internal static class ContourInput
{
    /// <summary>
    /// Tries to read the contour set bound to the node's contours input port.
    /// </summary>
    /// <param name="request">The request to read from.</param>
    /// <param name="contours">The contours when the input is bound to a set.</param>
    /// <param name="failure">The diagnostic to report when the input is unusable.</param>
    /// <returns><see langword="true"/> when the contours were read.</returns>
    internal static bool TryRead(
        NodeExecutionRequest request,
        out ContourCollection contours,
        out NodeDiagnostic? failure)
    {
        if (!request.Inputs.TryGetValue(OpenCvNodeIds.ContoursPortId, out PortValue? value)
            || value is not ContourCollectionValue collection)
        {
            contours = ContourCollection.Empty;
            failure = FrameInput.Rejected(
                request,
                $"The input port '{OpenCvNodeIds.ContoursPortId}' is not bound to a contour set.");
            return false;
        }

        contours = collection.Contours;
        failure = null;
        return true;
    }
}
