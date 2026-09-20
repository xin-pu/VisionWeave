using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>Separates a BGR image into independent blue, green, and red frames.</summary>
public sealed class SplitChannelsExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>Initializes the executor.</summary>
    public SplitChannelsExecutor(ILeaseLedger ledger) => _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

    /// <inheritdoc />
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!FrameInput.TryRead(request, out MatFrameLease input, out NodeDiagnostic? failure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(failure!));
        }

        if (input.Mat.Channels() < 3)
        {
            return Task.FromResult(NodeExecutionResult.Failure(FrameInput.Rejected(
                request,
                "Split Channels requires an image with at least three channels.")));
        }

        cancellationToken.ThrowIfCancellationRequested();
        Mat[] channels = Cv2.Split(input.Mat);
        MatFrameLease? blue = null;
        MatFrameLease? green = null;
        MatFrameLease? red = null;
        try
        {
            blue = MatFrameLease.Create(channels[0], _ledger);
            channels[0] = null!;
            green = MatFrameLease.Create(channels[1], _ledger);
            channels[1] = null!;
            red = MatFrameLease.Create(channels[2], _ledger);
            channels[2] = null!;
            NodeExecutionResult result = NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.BlueChannelPortId] = new ImageFrameValue(blue),
                    [OpenCvNodeIds.GreenChannelPortId] = new ImageFrameValue(green),
                    [OpenCvNodeIds.RedChannelPortId] = new ImageFrameValue(red),
                });
            blue = null;
            green = null;
            red = null;
            return Task.FromResult(result);
        }
        finally
        {
            blue?.Dispose();
            green?.Dispose();
            red?.Dispose();
            foreach (Mat? channel in channels)
            {
                channel?.Dispose();
            }
        }
    }
}
