using System.Diagnostics;
using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Resizes an image to an explicit pixel size. The target size is stated in
/// pixels rather than as a scale factor, so a workflow reproduces the same frame
/// on any input resolution, and the interpolation is chosen by the document
/// instead of by a default the user cannot see.
/// </summary>
public sealed class ResizeExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public ResizeExecutor(ILeaseLedger ledger)
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

        Stopwatch stopwatch = Stopwatch.StartNew();

        if (!FrameInput.TryRead(request, out MatFrameLease input, out NodeDiagnostic? failure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(failure!, stopwatch.Elapsed));
        }

        int width = request.Parameters.GetInt32(OpenCvNodeIds.WidthParameter);
        int height = request.Parameters.GetInt32(OpenCvNodeIds.HeightParameter);

        if (!IsAcceptedDimension(width) || !IsAcceptedDimension(height))
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameters '{OpenCvNodeIds.WidthParameter}' and '{OpenCvNodeIds.HeightParameter}' must be between " +
                    $"{OpenCvParameterBounds.MinDimension} and {OpenCvParameterBounds.MaxDimension}, " +
                    $"but they are {width} and {height}."),
                stopwatch.Elapsed));
        }

        if (!TryReadInterpolation(request, out InterpolationFlags interpolation, out NodeDiagnostic? interpolationFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(interpolationFailure!, stopwatch.Elapsed));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // The result is a native buffer this executor creates. It is owned by the
        // returned lease once that exists, and by this method until then.
        var output = new Mat();
        bool transferred = false;

        try
        {
            Cv2.Resize(input.Mat, output, new Size(width, height), interpolation: interpolation);

            MatFrameLease produced = MatFrameLease.Create(output, _ledger);
            transferred = true;
            stopwatch.Stop();

            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.ResizedPortId] = new ImageFrameValue(produced),
                },
                stopwatch.Elapsed));
        }
        finally
        {
            if (!transferred)
            {
                output.Dispose();
            }
        }
    }

    private static bool IsAcceptedDimension(int value)
        => value >= OpenCvParameterBounds.MinDimension && value <= OpenCvParameterBounds.MaxDimension;

    private static bool TryReadInterpolation(
        NodeExecutionRequest request,
        out InterpolationFlags interpolation,
        out NodeDiagnostic? failure)
    {
        string option = request.Parameters.Contains(OpenCvNodeIds.InterpolationParameter)
            ? request.Parameters.GetString(OpenCvNodeIds.InterpolationParameter)
            : OpenCvNodeIds.InterpolationArea;

        InterpolationFlags? parsed = option switch
        {
            OpenCvNodeIds.InterpolationNearest => InterpolationFlags.Nearest,
            OpenCvNodeIds.InterpolationLinear => InterpolationFlags.Linear,
            OpenCvNodeIds.InterpolationCubic => InterpolationFlags.Cubic,
            OpenCvNodeIds.InterpolationArea => InterpolationFlags.Area,
            _ => null,
        };

        if (parsed is null)
        {
            interpolation = InterpolationFlags.Area;
            failure = FrameInput.Rejected(
                request,
                $"Parameter '{OpenCvNodeIds.InterpolationParameter}' must be one of " +
                $"{string.Join(", ", OpenCvNodeIds.InterpolationOptions)}, but it is '{option}'.");
            return false;
        }

        interpolation = parsed.Value;
        failure = null;
        return true;
    }
}
