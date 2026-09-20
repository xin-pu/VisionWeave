using OpenCvSharp;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>Publishes the shared image result of the compact Aries-derived operations.</summary>
internal static class ImageExecutorResult
{
    internal static NodeExecutionResult Success(Mat output, ILeaseLedger ledger)
    {
        MatFrameLease frame = MatFrameLease.Create(output, ledger);
        return NodeExecutionResult.Success(
            new Dictionary<string, PortValue>(StringComparer.Ordinal)
            {
                [OpenCvNodeIds.ResultPortId] = new ImageFrameValue(frame),
            });
    }
}
