using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>Produces a mask whose pixels fall inside an inclusive intensity range.</summary>
public sealed class InRangeExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>Initializes the executor.</summary>
    public InRangeExecutor(ILeaseLedger ledger) => _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

    /// <inheritdoc />
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!FrameInput.TryRead(request, out MatFrameLease input, out NodeDiagnostic? failure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(failure!));
        }

        double lower = request.Parameters.GetDouble(OpenCvNodeIds.LowerParameter);
        double upper = request.Parameters.GetDouble(OpenCvNodeIds.UpperParameter);
        if (lower > upper)
        {
            return Task.FromResult(NodeExecutionResult.Failure(FrameInput.Rejected(
                request,
                $"Parameter '{OpenCvNodeIds.LowerParameter}' must not exceed '{OpenCvNodeIds.UpperParameter}'.")));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var output = new Mat();
        try
        {
            Cv2.InRange(input.Mat, Scalar.All(lower), Scalar.All(upper), output);
            return Task.FromResult(ImageExecutorResult.Success(output, _ledger));
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }
}
