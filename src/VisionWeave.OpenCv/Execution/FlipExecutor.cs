using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
///     Mirrors a frame around the horizontal axis, vertical axis, or both axes.
/// </summary>
public sealed class FlipExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    ///     Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the output lease.</param>
    public FlipExecutor(ILeaseLedger ledger)
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

        string mode = request.Parameters.GetString(OpenCvNodeIds.FlipModeParameter);
        FlipMode? parsed = mode switch
        {
            OpenCvNodeIds.FlipHorizontal => FlipMode.Y,
            OpenCvNodeIds.FlipVertical => FlipMode.X,
            OpenCvNodeIds.FlipBoth => FlipMode.XY,
            _ => null,
        };

        if (parsed is null)
        {
            return Task.FromResult(NodeExecutionResult.Failure(FrameInput.Rejected(
                request,
                $"Parameter '{OpenCvNodeIds.FlipModeParameter}' must be one of "
                + $"{string.Join(", ", OpenCvNodeIds.FlipModeOptions)}, but it is '{mode}'.")));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var output = new Mat();
        bool transferred = false;

        try
        {
            Cv2.Flip(input.Mat, output, parsed.Value);
            MatFrameLease frame = MatFrameLease.Create(output, _ledger);
            transferred = true;
            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.FlippedPortId] = new ImageFrameValue(frame),
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
