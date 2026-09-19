using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Smooths an image by averaging every pixel with its neighbours, which is the
/// cheapest smoothing this layer offers and the one that weighs every neighbour
/// alike. The kernel is square, because one size is what the Gaussian and median
/// nodes declare and the inspector then reads the same parameter on all three, and
/// a size of one is accepted rather than refused: a box filter of one passes the
/// frame through, which is the same bargain the Gaussian node makes.
/// </summary>
public sealed class BlurExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public BlurExecutor(ILeaseLedger ledger)
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

        if (kernelSize < OpenCvParameterBounds.MinKernelSize || kernelSize > OpenCvParameterBounds.MaxKernelSize)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameter '{OpenCvNodeIds.KernelSizeParameter}' must be between " +
                    $"{OpenCvParameterBounds.MinKernelSize} and {OpenCvParameterBounds.MaxKernelSize}, but it is {kernelSize}.")));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // The result is a native buffer this executor creates. It is owned by the
        // returned lease once that exists, and by this method until then.
        var output = new Mat();
        bool transferred = false;

        try
        {
            Cv2.Blur(input.Mat, output, new Size(kernelSize, kernelSize));

            MatFrameLease frame = MatFrameLease.Create(output, _ledger);
            transferred = true;

            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.BlurredPortId] = new ImageFrameValue(frame),
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
