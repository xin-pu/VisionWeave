using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>Extracts one colour channel as an independent grayscale frame.</summary>
public sealed class ExtractChannelExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>Initializes the executor.</summary>
    public ExtractChannelExecutor(ILeaseLedger ledger) => _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

    /// <inheritdoc />
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!FrameInput.TryRead(request, out MatFrameLease input, out NodeDiagnostic? failure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(failure!));
        }

        string channel = request.Parameters.GetString(OpenCvNodeIds.ChannelParameter);
        int index = channel switch
        {
            OpenCvNodeIds.ChannelBlue => 0,
            OpenCvNodeIds.ChannelGreen => 1,
            OpenCvNodeIds.ChannelRed => 2,
            _ => -1,
        };
        if (index < 0 || input.Mat.Channels() <= index)
        {
            return Task.FromResult(NodeExecutionResult.Failure(FrameInput.Rejected(
                request,
                $"Channel '{channel}' is not available in this image.")));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var output = new Mat();
        try
        {
            Cv2.ExtractChannel(input.Mat, output, index);
            return Task.FromResult(ImageExecutorResult.Success(output, _ledger));
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }
}
