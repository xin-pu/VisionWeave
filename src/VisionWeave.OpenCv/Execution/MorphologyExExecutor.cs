using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Reshapes the bright regions of an image by a structuring element, with the
/// operation deciding which of the two erode and dilate nodes, or which difference
/// between them, the node reports: opening removes what is smaller than the element,
/// closing fills what is smaller than it, the gradient reports the outline the two
/// disagree about, and the two hats report what the opening removed or the closing
/// filled. The operations are the five OpenCV implements for this call.
/// <para>
/// The element reads neighbours and writes the same type back, so the node accepts
/// every layout a frame can hold and reports the one it was given.
/// </para>
/// </summary>
public sealed class MorphologyExExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public MorphologyExExecutor(ILeaseLedger ledger)
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

        if (!TryReadOperation(request, out MorphTypes operation, out NodeDiagnostic? operationFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(operationFailure!));
        }

        if (!MorphologyKernel.TryReadShape(request, out MorphShapes shape, out NodeDiagnostic? shapeFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(shapeFailure!));
        }

        if (!MorphologyKernel.TryReadSize(request, out int size, out NodeDiagnostic? sizeFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(sizeFailure!));
        }

        if (!MorphologyKernel.TryReadIterations(request, out int iterations, out NodeDiagnostic? iterationFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(iterationFailure!));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // The result is a native buffer this executor creates. It is owned by the
        // returned lease once that exists, and by this method until then.
        var output = new Mat();
        bool transferred = false;

        try
        {
            using Mat element = MorphologyKernel.Create(shape, size);
            Cv2.MorphologyEx(input.Mat, output, operation, element, iterations: iterations);

            MatFrameLease frame = MatFrameLease.Create(output, _ledger);
            transferred = true;

            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.MorphedPortId] = new ImageFrameValue(frame),
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

    private static bool TryReadOperation(
        NodeExecutionRequest request,
        out MorphTypes operation,
        out NodeDiagnostic? failure)
    {
        string option = request.Parameters.Contains(OpenCvNodeIds.OperationParameter)
            ? request.Parameters.GetString(OpenCvNodeIds.OperationParameter)
            : OpenCvNodeIds.MorphologyOpen;

        MorphTypes? parsed = option switch
        {
            OpenCvNodeIds.MorphologyOpen => MorphTypes.Open,
            OpenCvNodeIds.MorphologyClose => MorphTypes.Close,
            OpenCvNodeIds.MorphologyGradient => MorphTypes.Gradient,
            OpenCvNodeIds.MorphologyTopHat => MorphTypes.TopHat,
            OpenCvNodeIds.MorphologyBlackHat => MorphTypes.BlackHat,
            _ => null,
        };

        if (parsed is null)
        {
            operation = MorphTypes.Open;
            failure = FrameInput.Rejected(
                request,
                $"Parameter '{OpenCvNodeIds.OperationParameter}' must be one of " +
                $"{string.Join(", ", OpenCvNodeIds.MorphologyOperationOptions)}, but it is '{option}'.");
            return false;
        }

        operation = parsed.Value;
        failure = null;
        return true;
    }
}
