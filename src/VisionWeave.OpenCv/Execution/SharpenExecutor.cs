using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>Sharpens a frame with the kernel used by the Aries sharpen block.</summary>
public sealed class SharpenExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>Initializes the executor.</summary>
    public SharpenExecutor(ILeaseLedger ledger) => _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

    /// <inheritdoc />
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!FrameInput.TryRead(request, out MatFrameLease input, out NodeDiagnostic? failure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(failure!));
        }

        cancellationToken.ThrowIfCancellationRequested();
        using Mat kernel = Mat.FromArray(new float[,] { { 0, -1, 0 }, { -1, 5, -1 }, { 0, -1, 0 } });
        var output = new Mat();
        try
        {
            Cv2.Filter2D(input.Mat, output, input.Mat.Type(), kernel);
            return Task.FromResult(ImageExecutorResult.Success(output, _ledger));
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }
}
