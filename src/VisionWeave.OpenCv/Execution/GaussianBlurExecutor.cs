using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Smooths an image with a Gaussian kernel. The node reads its input through a
/// lease and returns a new lease for the frame it produced, so the input is never
/// modified and the runtime releases the input when its last consumer is done.
/// <para>
/// The kernel size must be odd and positive, which OpenCV does not check; a
/// rejected value ends the node with a diagnostic instead of a native assertion.
/// An unexpected OpenCV failure is not caught, because the runtime records the
/// original exception with the node's failure diagnostic.
/// </para>
/// </summary>
public sealed class GaussianBlurExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public GaussianBlurExecutor(ILeaseLedger ledger)
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
            || kernelSize > OpenCvParameterBounds.MaxKernelSize)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameter '{OpenCvNodeIds.KernelSizeParameter}' must be an odd number between " +
                    $"{OpenCvParameterBounds.MinKernelSize} and {OpenCvParameterBounds.MaxKernelSize}, but it is {kernelSize}.")));
        }

        double sigma = request.Parameters.Contains(OpenCvNodeIds.SigmaParameter)
            ? request.Parameters.GetDouble(OpenCvNodeIds.SigmaParameter)
            : 0d;

        if (sigma < OpenCvParameterBounds.MinSigma || sigma > OpenCvParameterBounds.MaxSigma)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameter '{OpenCvNodeIds.SigmaParameter}' must be between " +
                    $"{OpenCvParameterBounds.MinSigma} and {OpenCvParameterBounds.MaxSigma}, but it is {sigma}.")));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // The result is a native buffer this executor creates. It is owned by the
        // returned lease once that exists, and by this method until then.
        var output = new Mat();
        bool transferred = false;

        try
        {
            Cv2.GaussianBlur(input.Mat, output, new Size(kernelSize, kernelSize), sigma, sigma);

            MatFrameLease produced = MatFrameLease.Create(output, _ledger);
            transferred = true;

            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.BlurredPortId] = new ImageFrameValue(produced),
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
