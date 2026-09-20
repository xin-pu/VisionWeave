using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>Adds a constant, replicated, or reflected border around an image.</summary>
public sealed class CopyMakeBorderExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>Initializes the executor.</summary>
    public CopyMakeBorderExecutor(ILeaseLedger ledger) => _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

    /// <inheritdoc />
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!FrameInput.TryRead(request, out MatFrameLease input, out NodeDiagnostic? failure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(failure!));
        }

        string border = request.Parameters.GetString(OpenCvNodeIds.BorderTypeParameter);
        BorderTypes? borderType = border switch
        {
            OpenCvNodeIds.BorderConstant => BorderTypes.Constant,
            OpenCvNodeIds.BorderReplicate => BorderTypes.Replicate,
            OpenCvNodeIds.BorderReflect => BorderTypes.Reflect,
            _ => null,
        };
        if (borderType is null)
        {
            return Task.FromResult(NodeExecutionResult.Failure(FrameInput.Rejected(
                request,
                $"Border type '{border}' is not supported.")));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var output = new Mat();
        try
        {
            Cv2.CopyMakeBorder(
                input.Mat,
                output,
                request.Parameters.GetInt32(OpenCvNodeIds.TopParameter),
                request.Parameters.GetInt32(OpenCvNodeIds.BottomParameter),
                request.Parameters.GetInt32(OpenCvNodeIds.LeftParameter),
                request.Parameters.GetInt32(OpenCvNodeIds.RightParameter),
                borderType.Value,
                Scalar.All(request.Parameters.GetDouble(OpenCvNodeIds.BorderValueParameter)));
            return Task.FromResult(ImageExecutorResult.Success(output, _ledger));
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }
}
