using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
///     Stretches pixel values into a requested range while preserving the frame's
///     dimensions and channel layout.
/// </summary>
public sealed class NormalizeExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    ///     Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the output lease.</param>
    public NormalizeExecutor(ILeaseLedger ledger)
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

        double minimum = request.Parameters.GetDouble(OpenCvNodeIds.AlphaParameter);
        double maximum = request.Parameters.GetDouble(OpenCvNodeIds.BetaParameter);

        if (minimum >= maximum)
        {
            return Task.FromResult(NodeExecutionResult.Failure(FrameInput.Rejected(
                request,
                $"Parameter '{OpenCvNodeIds.AlphaParameter}' must be smaller than '{OpenCvNodeIds.BetaParameter}'.")));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var output = new Mat();
        bool transferred = false;

        try
        {
            Cv2.Normalize(input.Mat, output, minimum, maximum, NormTypes.MinMax);
            MatFrameLease frame = MatFrameLease.Create(output, _ledger);
            transferred = true;
            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.NormalizedPortId] = new ImageFrameValue(frame),
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
