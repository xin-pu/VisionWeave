using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Converts an image between colour spaces. The conversion is one the definition
/// declares rather than any OpenCV code, so which conversions exist is stated to
/// the editor and the input a conversion needs can be checked before it runs: a
/// conversion that expects colour reports a diagnostic when it is handed a
/// frame it cannot convert, instead of failing with a native exception.
/// </summary>
public sealed class CvtColorExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public CvtColorExecutor(ILeaseLedger ledger)
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

        if (!TryReadConversion(request, out ColorConversionCodes conversion, out NodeDiagnostic? conversionFailure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(conversionFailure!));
        }

        if (input.PixelFormat != FramePixelFormat.Bgr24)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"This node converts a BGR image, but the input is {input.PixelFormat}. "
                    + "Connect the frame an Image Source reads with its default colour format.")));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // The result is a native buffer this executor creates. It is owned by the
        // returned lease once that exists, and by this method until then.
        var output = new Mat();
        bool transferred = false;

        try
        {
            Cv2.CvtColor(input.Mat, output, conversion);

            MatFrameLease frame = MatFrameLease.Create(output, _ledger);
            transferred = true;

            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.ConvertedPortId] = new ImageFrameValue(frame),
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

    private static bool TryReadConversion(
        NodeExecutionRequest request,
        out ColorConversionCodes conversion,
        out NodeDiagnostic? failure)
    {
        string option = request.Parameters.Contains(OpenCvNodeIds.ConversionParameter)
            ? request.Parameters.GetString(OpenCvNodeIds.ConversionParameter)
            : OpenCvNodeIds.ConversionGray;

        ColorConversionCodes? parsed = option switch
        {
            OpenCvNodeIds.ConversionGray => ColorConversionCodes.BGR2GRAY,
            OpenCvNodeIds.ConversionRgb => ColorConversionCodes.BGR2RGB,
            OpenCvNodeIds.ConversionHsv => ColorConversionCodes.BGR2HSV,
            OpenCvNodeIds.ConversionLab => ColorConversionCodes.BGR2Lab,
            _ => null,
        };

        if (parsed is null)
        {
            conversion = ColorConversionCodes.BGR2GRAY;
            failure = FrameInput.Rejected(
                request,
                $"Parameter '{OpenCvNodeIds.ConversionParameter}' must be one of " +
                $"{string.Join(", ", OpenCvNodeIds.ConversionOptions)}, but it is '{option}'.");
            return false;
        }

        conversion = parsed.Value;
        failure = null;
        return true;
    }
}
