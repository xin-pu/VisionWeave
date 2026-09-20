using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;

namespace VisionWeave.OpenCv.Execution;

/// <summary>Exchanges image rows and columns.</summary>
public sealed class TransposeExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>Initializes the executor.</summary>
    public TransposeExecutor(ILeaseLedger ledger) => _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

    /// <inheritdoc />
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!FrameInput.TryRead(request, out MatFrameLease input, out NodeDiagnostic? failure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(failure!));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var output = new Mat();
        try
        {
            Cv2.Transpose(input.Mat, output);
            return Task.FromResult(ImageExecutorResult.Success(output, _ledger));
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }
}
