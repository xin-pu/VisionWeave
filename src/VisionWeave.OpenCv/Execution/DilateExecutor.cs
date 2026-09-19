using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Thickens the bright regions of an image by a structuring element: a pixel becomes
/// bright if any pixel of the element, placed with the pixel at its middle, is
/// already bright. A single bright pixel grows into the shape of the element, and a
/// frame of one value comes back unchanged, because there is nothing to grow into.
/// <para>
/// The element reads neighbours and writes the same type back, so the node accepts
/// every layout a frame can hold and reports the one it was given.
/// </para>
/// </summary>
public sealed class DilateExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public DilateExecutor(ILeaseLedger ledger)
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
            Cv2.Dilate(input.Mat, output, element, iterations: iterations);

            MatFrameLease frame = MatFrameLease.Create(output, _ledger);
            transferred = true;

            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.DilatedPortId] = new ImageFrameValue(frame),
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
