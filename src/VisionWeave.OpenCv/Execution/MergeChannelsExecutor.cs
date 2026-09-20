using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>Combines blue, green, and red grayscale frames into one BGR frame.</summary>
public sealed class MergeChannelsExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>Initializes the executor.</summary>
    public MergeChannelsExecutor(ILeaseLedger ledger) => _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

    /// <inheritdoc />
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryReadChannels(request, out MatFrameLease[] channels, out NodeDiagnostic? failure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(failure!));
        }

        if (channels.Any(channel => channel.PixelFormat != FramePixelFormat.Gray8)
            || channels.Any(channel => channel.Width != channels[0].Width || channel.Height != channels[0].Height))
        {
            return Task.FromResult(NodeExecutionResult.Failure(FrameInput.Rejected(
                request,
                "Merge Channels requires three equally sized 8-bit grayscale images.")));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var output = new Mat();
        try
        {
            Cv2.Merge(channels.Select(channel => channel.Mat).ToArray(), output);
            return Task.FromResult(ImageExecutorResult.Success(output, _ledger));
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    private static bool TryReadChannels(
        NodeExecutionRequest request,
        out MatFrameLease[] channels,
        out NodeDiagnostic? failure)
    {
        string[] ids =
        [
            OpenCvNodeIds.BlueChannelPortId,
            OpenCvNodeIds.GreenChannelPortId,
            OpenCvNodeIds.RedChannelPortId,
        ];
        channels = new MatFrameLease[ids.Length];
        for (int index = 0; index < ids.Length; index++)
        {
            if (!FrameInput.TryRead(request, ids[index], out channels[index], out failure))
            {
                return false;
            }
        }

        failure = null;
        return true;
    }
}
