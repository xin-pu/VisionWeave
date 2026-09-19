using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Reads the structuring element a morphology node works with. A parameter is
/// document data, so the element is stated as a shape and the side of a square
/// rather than as a kernel the user assembles: the shape is an option of the three
/// OpenCV builds, and the size is the parameter the smoothing nodes already declare,
/// so one kernel size means the same thing wherever it is set.
/// </summary>
internal static class MorphologyKernel
{
    /// <summary>
    /// Reads the shape of the structuring element.
    /// </summary>
    /// <param name="request">The node request that carries the parameters.</param>
    /// <param name="shape">The shape, when the parameter holds one.</param>
    /// <param name="failure">The diagnostic that names the parameter, when it does not.</param>
    /// <returns><see langword="true"/> when the parameter names a shape.</returns>
    internal static bool TryReadShape(
        NodeExecutionRequest request,
        out MorphShapes shape,
        out NodeDiagnostic? failure)
    {
        string option = request.Parameters.Contains(OpenCvNodeIds.KernelShapeParameter)
            ? request.Parameters.GetString(OpenCvNodeIds.KernelShapeParameter)
            : OpenCvNodeIds.KernelShapeRect;

        MorphShapes? parsed = option switch
        {
            OpenCvNodeIds.KernelShapeRect => MorphShapes.Rect,
            OpenCvNodeIds.KernelShapeEllipse => MorphShapes.Ellipse,
            OpenCvNodeIds.KernelShapeCross => MorphShapes.Cross,
            _ => null,
        };

        if (parsed is null)
        {
            shape = MorphShapes.Rect;
            failure = FrameInput.Rejected(
                request,
                $"Parameter '{OpenCvNodeIds.KernelShapeParameter}' must be one of " +
                $"{string.Join(", ", OpenCvNodeIds.KernelShapeOptions)}, but it is '{option}'.");
            return false;
        }

        shape = parsed.Value;
        failure = null;
        return true;
    }

    /// <summary>
    /// Reads the side of the square structuring element.
    /// </summary>
    /// <param name="request">The node request that carries the parameters.</param>
    /// <param name="size">The side of the square, when the parameter holds one.</param>
    /// <param name="failure">The diagnostic that names the parameter, when it does not.</param>
    /// <returns><see langword="true"/> when the parameter is a usable size.</returns>
    internal static bool TryReadSize(
        NodeExecutionRequest request,
        out int size,
        out NodeDiagnostic? failure)
    {
        size = request.Parameters.GetInt32(OpenCvNodeIds.KernelSizeParameter);

        if (size < OpenCvParameterBounds.MinKernelSize || size > OpenCvParameterBounds.MaxKernelSize)
        {
            failure = FrameInput.Rejected(
                request,
                $"Parameter '{OpenCvNodeIds.KernelSizeParameter}' must be between " +
                $"{OpenCvParameterBounds.MinKernelSize} and {OpenCvParameterBounds.MaxKernelSize}, but it is {size}.");
            return false;
        }

        failure = null;
        return true;
    }

    /// <summary>
    /// Reads how many times the node applies its element.
    /// </summary>
    /// <param name="request">The node request that carries the parameters.</param>
    /// <param name="iterations">The count, when the parameter holds one.</param>
    /// <param name="failure">The diagnostic that names the parameter, when it does not.</param>
    /// <returns><see langword="true"/> when the parameter is a usable count.</returns>
    internal static bool TryReadIterations(
        NodeExecutionRequest request,
        out int iterations,
        out NodeDiagnostic? failure)
    {
        iterations = request.Parameters.GetInt32(OpenCvNodeIds.IterationsParameter);

        if (iterations < OpenCvParameterBounds.MinIterations || iterations > OpenCvParameterBounds.MaxIterations)
        {
            failure = FrameInput.Rejected(
                request,
                $"Parameter '{OpenCvNodeIds.IterationsParameter}' must be between " +
                $"{OpenCvParameterBounds.MinIterations} and {OpenCvParameterBounds.MaxIterations}, but it is {iterations}.");
            return false;
        }

        failure = null;
        return true;
    }

    /// <summary>
    /// Builds the structuring element, which the caller owns.
    /// </summary>
    /// <param name="shape">The shape of the element.</param>
    /// <param name="size">The side of the square the element fills.</param>
    /// <returns>The element, which the caller releases.</returns>
    internal static Mat Create(MorphShapes shape, int size) => Cv2.GetStructuringElement(shape, new Size(size, size));
}
