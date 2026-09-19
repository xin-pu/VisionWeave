using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Separates an image into foreground and background by comparing every pixel
/// with a threshold. The node states the depth it works on and refuses a frame
/// that is not 8-bit, because a threshold of 122 means one thing on an 8-bit
/// frame and another on a 16-bit one, and a rule that silently meant something
/// else would be worse than a node that names what it needs.
/// </summary>
public sealed class ThresholdExecutor : INodeExecutor
{
    private static readonly FramePixelFormat[] AcceptedFormats =
    [
        FramePixelFormat.Gray8,
        FramePixelFormat.Bgr24,
        FramePixelFormat.Bgra32,
    ];

    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public ThresholdExecutor(ILeaseLedger ledger)
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

        double threshold = request.Parameters.GetDouble(OpenCvNodeIds.ThresholdParameter);
        double maxValue = request.Parameters.GetDouble(OpenCvNodeIds.MaxValueParameter);

        if (!IsAcceptedLevel(threshold) || !IsAcceptedLevel(maxValue))
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameters '{OpenCvNodeIds.ThresholdParameter}' and '{OpenCvNodeIds.MaxValueParameter}' must be between " +
                    $"{OpenCvParameterBounds.MinLevel} and {OpenCvParameterBounds.MaxLevel}, but they are {threshold} and {maxValue}.")));
        }

        if (!TryReadThresholdType(request, out ThresholdTypes type, out NodeDiagnostic? typeFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(typeFailure!));
        }

        if (!AcceptedFormats.Contains(input.PixelFormat))
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"This node thresholds 8-bit images, but the input is {input.PixelFormat}. "
                    + "Connect the frame an Image Source reads with its default colour format.")));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // The result is a native buffer this executor creates. It is owned by the
        // returned lease once that exists, and by this method until then.
        var output = new Mat();
        bool transferred = false;

        try
        {
            Cv2.Threshold(input.Mat, output, threshold, maxValue, type);

            MatFrameLease frame = MatFrameLease.Create(output, _ledger);
            transferred = true;

            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.ThresholdedPortId] = new ImageFrameValue(frame),
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

    private static bool IsAcceptedLevel(double value)
        => value >= OpenCvParameterBounds.MinLevel && value <= OpenCvParameterBounds.MaxLevel;

    private static bool TryReadThresholdType(
        NodeExecutionRequest request,
        out ThresholdTypes type,
        out NodeDiagnostic? failure)
    {
        string option = request.Parameters.Contains(OpenCvNodeIds.ThresholdTypeParameter)
            ? request.Parameters.GetString(OpenCvNodeIds.ThresholdTypeParameter)
            : OpenCvNodeIds.ThresholdBinary;

        ThresholdTypes? parsed = option switch
        {
            OpenCvNodeIds.ThresholdBinary => ThresholdTypes.Binary,
            OpenCvNodeIds.ThresholdBinaryInverted => ThresholdTypes.BinaryInv,
            OpenCvNodeIds.ThresholdTruncate => ThresholdTypes.Trunc,
            OpenCvNodeIds.ThresholdToZero => ThresholdTypes.Tozero,
            OpenCvNodeIds.ThresholdToZeroInverted => ThresholdTypes.TozeroInv,
            _ => null,
        };

        if (parsed is null)
        {
            type = ThresholdTypes.Binary;
            failure = FrameInput.Rejected(
                request,
                $"Parameter '{OpenCvNodeIds.ThresholdTypeParameter}' must be one of " +
                $"{string.Join(", ", OpenCvNodeIds.ThresholdTypeOptions)}, but it is '{option}'.");
            return false;
        }

        type = parsed.Value;
        failure = null;
        return true;
    }
}
