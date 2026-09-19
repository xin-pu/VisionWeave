using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Binarises an image against its own neighbourhood instead of against one number,
/// so a frame lit unevenly still separates: every pixel is compared with the
/// average of the block around it, less the constant. A flat region passes
/// everywhere, because a pixel equals the average it is compared with, and the
/// first pixel of a step fails, because the average it is measured against is
/// raised by the brighter side of it.
/// <para>
/// The node states the one layout it binarises. OpenCV's adaptive threshold takes a
/// single 8-bit channel, so a colour frame is refused with the frame named rather
/// than thrown on, and the rules are the two OpenCV implements rather than the five
/// the plain threshold node declares.
/// </para>
/// </summary>
public sealed class AdaptiveThresholdExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public AdaptiveThresholdExecutor(ILeaseLedger ledger)
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

        double maxValue = request.Parameters.GetDouble(OpenCvNodeIds.MaxValueParameter);

        if (maxValue < OpenCvParameterBounds.MinLevel || maxValue > OpenCvParameterBounds.MaxLevel)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameter '{OpenCvNodeIds.MaxValueParameter}' must be between " +
                    $"{OpenCvParameterBounds.MinLevel} and {OpenCvParameterBounds.MaxLevel}, but it is {maxValue}.")));
        }

        if (!TryReadMethod(request, out AdaptiveThresholdTypes method, out NodeDiagnostic? methodFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(methodFailure!));
        }

        if (!TryReadRule(request, out ThresholdTypes rule, out NodeDiagnostic? ruleFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(ruleFailure!));
        }

        int blockSize = request.Parameters.GetInt32(OpenCvNodeIds.BlockSizeParameter);

        if (blockSize % 2 == 0
            || blockSize < OpenCvParameterBounds.MinBlockSize
            || blockSize > OpenCvParameterBounds.MaxKernelSize)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameter '{OpenCvNodeIds.BlockSizeParameter}' must be an odd number between " +
                    $"{OpenCvParameterBounds.MinBlockSize} and {OpenCvParameterBounds.MaxKernelSize}, but it is {blockSize}.")));
        }

        double constant = request.Parameters.GetDouble(OpenCvNodeIds.ConstantParameter);

        if (constant < OpenCvParameterBounds.MinConstant || constant > OpenCvParameterBounds.MaxConstant)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameter '{OpenCvNodeIds.ConstantParameter}' must be between " +
                    $"{OpenCvParameterBounds.MinConstant} and {OpenCvParameterBounds.MaxConstant}, but it is {constant}.")));
        }

        if (input.PixelFormat != FramePixelFormat.Gray8)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"This node binarises a single 8-bit channel, but the input is {input.PixelFormat}. "
                    + "Convert the frame to greyscale before it.")));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // The result is a native buffer this executor creates. It is owned by the
        // returned lease once that exists, and by this method until then.
        var output = new Mat();
        bool transferred = false;

        try
        {
            Cv2.AdaptiveThreshold(input.Mat, output, maxValue, method, rule, blockSize, constant);

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

    private static bool TryReadMethod(
        NodeExecutionRequest request,
        out AdaptiveThresholdTypes method,
        out NodeDiagnostic? failure)
    {
        string option = request.Parameters.Contains(OpenCvNodeIds.AdaptiveMethodParameter)
            ? request.Parameters.GetString(OpenCvNodeIds.AdaptiveMethodParameter)
            : OpenCvNodeIds.AdaptiveMethodMean;

        AdaptiveThresholdTypes? parsed = option switch
        {
            OpenCvNodeIds.AdaptiveMethodMean => AdaptiveThresholdTypes.MeanC,
            OpenCvNodeIds.AdaptiveMethodGaussian => AdaptiveThresholdTypes.GaussianC,
            _ => null,
        };

        if (parsed is null)
        {
            method = AdaptiveThresholdTypes.MeanC;
            failure = FrameInput.Rejected(
                request,
                $"Parameter '{OpenCvNodeIds.AdaptiveMethodParameter}' must be one of " +
                $"{string.Join(", ", OpenCvNodeIds.AdaptiveMethodOptions)}, but it is '{option}'.");
            return false;
        }

        method = parsed.Value;
        failure = null;
        return true;
    }

    private static bool TryReadRule(
        NodeExecutionRequest request,
        out ThresholdTypes rule,
        out NodeDiagnostic? failure)
    {
        string option = request.Parameters.Contains(OpenCvNodeIds.ThresholdTypeParameter)
            ? request.Parameters.GetString(OpenCvNodeIds.ThresholdTypeParameter)
            : OpenCvNodeIds.ThresholdBinary;

        ThresholdTypes? parsed = option switch
        {
            OpenCvNodeIds.ThresholdBinary => ThresholdTypes.Binary,
            OpenCvNodeIds.ThresholdBinaryInverted => ThresholdTypes.BinaryInv,
            _ => null,
        };

        if (parsed is null)
        {
            rule = ThresholdTypes.Binary;
            failure = FrameInput.Rejected(
                request,
                $"Parameter '{OpenCvNodeIds.ThresholdTypeParameter}' must be one of " +
                $"{string.Join(", ", OpenCvNodeIds.AdaptiveThresholdTypeOptions)}, but it is '{option}'.");
            return false;
        }

        rule = parsed.Value;
        failure = null;
        return true;
    }
}
