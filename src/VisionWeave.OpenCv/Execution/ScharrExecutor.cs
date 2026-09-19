using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Reports how fast the pixels of an image change along one axis, with the three by
/// three kernel that weights the pixels nearest a step most. It is the same reading
/// the Sobel node takes with a fixed kernel size, and it declares one order less:
/// OpenCV measures a Scharr gradient along exactly one axis, so the node states that
/// rather than passing two orders on and letting the call refuse them.
/// </summary>
public sealed class ScharrExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public ScharrExecutor(ILeaseLedger ledger)
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

        if (xOrder + yOrder != 1 || xOrder < OpenCvParameterBounds.MinDerivativeOrder || yOrder < OpenCvParameterBounds.MinDerivativeOrder)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameters '{OpenCvNodeIds.XOrderParameter}' and '{OpenCvNodeIds.YOrderParameter}' must be one apart in total: " +
                    $"either 1 and 0, or 0 and 1, but they are {xOrder} and {yOrder}. "
                    + "This node measures the gradient along one axis at a time.")));
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
        Cv2.Scharr(input.Mat, derivative, MatType.CV_16S, xOrder, yOrder, scale);

        return Task.FromResult(DerivativeFrame.Report(derivative, _ledger, OpenCvNodeIds.GradientPortId));
    }
}
