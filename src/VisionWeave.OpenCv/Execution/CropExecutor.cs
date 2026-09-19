using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Keeps one rectangle of an image. The rectangle is four numbers rather than an
/// interactive selection, because a node's parameters are part of the document
/// and a run has to reproduce without asking anything, and the kept region is
/// copied into a frame of its own rather than published as a view of the input:
/// a view would share the producer's buffer, so the lease the run owns would not
/// be the only owner of that memory.
/// </summary>
public sealed class CropExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public CropExecutor(ILeaseLedger ledger)
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

        int x = request.Parameters.GetInt32(OpenCvNodeIds.XParameter);
        int y = request.Parameters.GetInt32(OpenCvNodeIds.YParameter);
        int width = request.Parameters.GetInt32(OpenCvNodeIds.WidthParameter);
        int height = request.Parameters.GetInt32(OpenCvNodeIds.HeightParameter);

        if (!IsAcceptedExtent(width) || !IsAcceptedExtent(height) || x < OpenCvParameterBounds.MinOrigin || y < OpenCvParameterBounds.MinOrigin)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameters '{OpenCvNodeIds.XParameter}', '{OpenCvNodeIds.YParameter}', '{OpenCvNodeIds.WidthParameter}', and '{OpenCvNodeIds.HeightParameter}' " +
                    $"must be positive and at most {OpenCvParameterBounds.MaxDimension}, but they are {x}, {y}, {width}, and {height}.")));
        }

        if (x + width > input.Width || y + height > input.Height)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"The rectangle ({x}, {y}) {width} by {height} does not fit inside the {input.Width} by {input.Height} input image.")));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var rectangle = new Rect(x, y, width, height);

        // The result is a native buffer this executor creates. It is owned by the
        // returned lease once that exists, and by this method until then.
        var output = new Mat();
        bool transferred = false;

        try
        {
            input.Mat[rectangle].CopyTo(output);

            MatFrameLease frame = MatFrameLease.Create(output, _ledger);
            transferred = true;

            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.CroppedPortId] = new ImageFrameValue(frame),
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

    private static bool IsAcceptedExtent(int value)
        => value >= OpenCvParameterBounds.MinDimension && value <= OpenCvParameterBounds.MaxDimension;
}
