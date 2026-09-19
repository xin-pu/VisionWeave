using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Reports how fast the change of an image changes, which is the second derivative
/// along both axes at once: a step between two flat regions reads as a pair of
/// opposite responses, and an isolated pixel reads as a response at the pixel and
/// around it. The result is signed, so the node reports the absolute value in the
/// layout it was given, exactly as the gradient nodes do.
/// <para>
/// The aperture the node names decides which kernel OpenCV measures with: an
/// aperture of one uses the five point kernel, which answers at a pixel and at its
/// four neighbours, and a wider one is the sum of the two second derivative Sobel
/// operators, which also answers at the four diagonals of a pixel.
/// </para>
/// </summary>
public sealed class LaplacianExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public LaplacianExecutor(ILeaseLedger ledger)
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

        int kernelSize = request.Parameters.GetInt32(OpenCvNodeIds.KernelSizeParameter);

        if (kernelSize % 2 == 0
            || kernelSize < OpenCvParameterBounds.MinKernelSize
            || kernelSize > OpenCvParameterBounds.MaxDerivativeKernelSize)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameter '{OpenCvNodeIds.KernelSizeParameter}' must be an odd number between " +
                    $"{OpenCvParameterBounds.MinKernelSize} and {OpenCvParameterBounds.MaxDerivativeKernelSize}, but it is {kernelSize}.")));
        }

        double scale = request.Parameters.GetDouble(OpenCvNodeIds.ScaleParameter);

        if (scale < OpenCvParameterBounds.MinScale || scale > OpenCvParameterBounds.MaxScale)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameter '{OpenCvNodeIds.ScaleParameter}' must be between " +
                    $"{OpenCvParameterBounds.MinScale} and {OpenCvParameterBounds.MaxScale}, but it is {scale}.")));
        }

        if (!DerivativeFrame.Accepts(input.PixelFormat))
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(request, DerivativeFrame.Refusal(input.PixelFormat))));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // The signed derivative is a temporary of this execution: it is released
        // here, and what the node publishes is the frame built from it.
        using var derivative = new Mat();
        Cv2.Laplacian(input.Mat, derivative, MatType.CV_16S, kernelSize, scale);

        return Task.FromResult(DerivativeFrame.Report(derivative, _ledger, OpenCvNodeIds.GradientPortId));
    }
}
