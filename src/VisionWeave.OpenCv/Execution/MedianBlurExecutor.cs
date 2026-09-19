using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Smooths an image with a median filter, which keeps an edge sharp instead of
/// averaging across it. The kernel is odd, as OpenCV requires, and the smallest
/// one that smooths anything is three, so a kernel of one is refused rather than
/// passed on as a node that did nothing.
/// </summary>
public sealed class MedianBlurExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public MedianBlurExecutor(ILeaseLedger ledger)
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
            || kernelSize < OpenCvParameterBounds.MinMedianKernelSize
            || kernelSize > OpenCvParameterBounds.MaxKernelSize)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameter '{OpenCvNodeIds.KernelSizeParameter}' must be an odd number between " +
                    $"{OpenCvParameterBounds.MinMedianKernelSize} and {OpenCvParameterBounds.MaxKernelSize}, but it is {kernelSize}.")));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // The result is a native buffer this executor creates. It is owned by the
        // returned lease once that exists, and by this method until then.
        var output = new Mat();
        bool transferred = false;

        try
        {
            Cv2.MedianBlur(input.Mat, output, kernelSize);

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
