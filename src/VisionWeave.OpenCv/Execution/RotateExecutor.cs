using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>Rotates a frame around its centre while preserving its dimensions.</summary>
public sealed class RotateExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>Initializes the executor.</summary>
    public RotateExecutor(ILeaseLedger ledger) => _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

    /// <inheritdoc />
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!FrameInput.TryRead(request, out MatFrameLease input, out NodeDiagnostic? failure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(failure!));
        }

        cancellationToken.ThrowIfCancellationRequested();
        double angle = request.Parameters.GetDouble(OpenCvNodeIds.AngleParameter);
        var centre = new Point2f((input.Width - 1) / 2f, (input.Height - 1) / 2f);
        using Mat transform = Cv2.GetRotationMatrix2D(centre, angle, 1);
        var output = new Mat();
        try
        {
            Cv2.WarpAffine(input.Mat, output, transform, new Size(input.Width, input.Height));
            return Task.FromResult(ImageExecutorResult.Success(output, _ledger));
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }
}
