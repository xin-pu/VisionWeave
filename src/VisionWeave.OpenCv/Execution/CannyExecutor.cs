using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Reports the edges of an image as a black and white map: a pixel is kept when the
/// gradient there is stronger than the high threshold, kept when it is weaker than
/// that but stronger than the low one and connected to a pixel that was kept, and
/// dropped otherwise. The two thresholds are what make it more than a gradient
/// comparison, which is why the node refuses a low threshold that is not below the
/// high one instead of quietly reporting a plain threshold at the high value.
/// <para>
/// The node takes the layouts OpenCV takes, a colour frame among them, and reports
/// one channel either way: an edge is a place, not a colour.
/// </para>
/// </summary>
public sealed class CannyExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public CannyExecutor(ILeaseLedger ledger)
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

        double thresholdLow = request.Parameters.GetDouble(OpenCvNodeIds.ThresholdLowParameter);
        double thresholdHigh = request.Parameters.GetDouble(OpenCvNodeIds.ThresholdHighParameter);

        if (!IsAcceptedThreshold(thresholdLow) || !IsAcceptedThreshold(thresholdHigh))
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameters '{OpenCvNodeIds.ThresholdLowParameter}' and '{OpenCvNodeIds.ThresholdHighParameter}' must be between " +
                    $"{OpenCvParameterBounds.MinLevel} and {OpenCvParameterBounds.MaxEdgeThreshold}, " +
                    $"but they are {thresholdLow} and {thresholdHigh}.")));
        }

        if (thresholdLow >= thresholdHigh)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameter '{OpenCvNodeIds.ThresholdLowParameter}' must be lower than " +
                    $"'{OpenCvNodeIds.ThresholdHighParameter}', but they are {thresholdLow} and {thresholdHigh}. "
                    + "An edge is an edge because a weak pixel beside it is connected to a strong one, and two thresholds "
                    + "that do not leave a weak range below the strong one compare like a single threshold.")));
        }

        int apertureSize = request.Parameters.GetInt32(OpenCvNodeIds.ApertureSizeParameter);

        if (apertureSize % 2 == 0
            || apertureSize < OpenCvParameterBounds.MinCannyApertureSize
            || apertureSize > OpenCvParameterBounds.MaxCannyApertureSize)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameter '{OpenCvNodeIds.ApertureSizeParameter}' must be an odd number between " +
                    $"{OpenCvParameterBounds.MinCannyApertureSize} and {OpenCvParameterBounds.MaxCannyApertureSize}, but it is {apertureSize}.")));
        }

        bool l2Gradient = request.Parameters.Contains(OpenCvNodeIds.L2GradientParameter)
            && request.Parameters.GetBoolean(OpenCvNodeIds.L2GradientParameter);

        if (!DerivativeFrame.Accepts(input.PixelFormat))
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(request, DerivativeFrame.Refusal(input.PixelFormat))));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // The result is a native buffer this executor creates. It is owned by the
        // returned lease once that exists, and by this method until then.
        var output = new Mat();
        bool transferred = false;

        try
        {
            Cv2.Canny(input.Mat, output, thresholdLow, thresholdHigh, apertureSize, l2Gradient);

            MatFrameLease frame = MatFrameLease.Create(output, _ledger);
            transferred = true;

            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.EdgesPortId] = new ImageFrameValue(frame),
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

    private static bool IsAcceptedThreshold(double value)
        => value >= OpenCvParameterBounds.MinLevel && value <= OpenCvParameterBounds.MaxEdgeThreshold;
}
