using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Reports how fast the pixels of an image change along one axis or both, which is
/// the first step of finding an edge in it. The result is signed and a frame is not,
/// so the node reports the absolute gradient saturated to eight bits, in the layout
/// it was given, and declares the scale that decides how much of the gradient
/// reaches that range: a derivative reported as it is computed would report the
/// half of itself that points the other way as black.
/// </summary>
public sealed class SobelExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public SobelExecutor(ILeaseLedger ledger)
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

        int xOrder = request.Parameters.GetInt32(OpenCvNodeIds.XOrderParameter);
        int yOrder = request.Parameters.GetInt32(OpenCvNodeIds.YOrderParameter);

        if (!IsAcceptedOrder(xOrder) || !IsAcceptedOrder(yOrder))
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameters '{OpenCvNodeIds.XOrderParameter}' and '{OpenCvNodeIds.YOrderParameter}' must be between " +
                    $"{OpenCvParameterBounds.MinDerivativeOrder} and {OpenCvParameterBounds.MaxDerivativeOrder}, " +
                    $"but they are {xOrder} and {yOrder}.")));
        }

        if (xOrder + yOrder == 0)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameters '{OpenCvNodeIds.XOrderParameter}' and '{OpenCvNodeIds.YOrderParameter}' cannot both be zero: " +
                    "the node would report a black frame rather than a derivative.")));
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
        Cv2.Sobel(input.Mat, derivative, MatType.CV_16S, xOrder, yOrder, kernelSize, scale);

        return Task.FromResult(DerivativeFrame.Report(derivative, _ledger, OpenCvNodeIds.GradientPortId));
    }

    private static bool IsAcceptedOrder(int order)
        => order >= OpenCvParameterBounds.MinDerivativeOrder && order <= OpenCvParameterBounds.MaxDerivativeOrder;
}
