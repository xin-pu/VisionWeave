using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
///     Applies a linear contrast multiplier and brightness offset, then converts the
///     result to an unsigned eight-bit image.
/// </summary>
public sealed class ConvertScaleAbsExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    ///     Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the output lease.</param>
    public ConvertScaleAbsExecutor(ILeaseLedger ledger)
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

        cancellationToken.ThrowIfCancellationRequested();
        var output = new Mat();
        bool transferred = false;

        try
        {
            Cv2.ConvertScaleAbs(
                input.Mat,
                output,
                request.Parameters.GetDouble(OpenCvNodeIds.AlphaParameter),
                request.Parameters.GetDouble(OpenCvNodeIds.BetaParameter));
            MatFrameLease frame = MatFrameLease.Create(output, _ledger);
            transferred = true;
            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.ConvertedPortId] = new ImageFrameValue(frame),
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
