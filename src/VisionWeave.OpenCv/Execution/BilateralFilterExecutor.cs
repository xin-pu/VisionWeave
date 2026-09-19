using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Smooths an image while keeping its edges: a neighbour is weighted by how close
/// its value is as well as by how close it lies, so a step steeper than the
/// intensity sigma contributes almost nothing to the average taken across it, and
/// a flat region is smoothed like any blur would. The diameter sets the
/// neighbourhood and the two sigmas set the weighting, and OpenCV reads all three
/// when the diameter is positive, so all three are declared rather than one being
/// derived from another.
/// <para>
/// This node states the layouts it smooths. OpenCV's bilateral filter takes one or
/// three 8-bit channels or one or three 32-bit float ones, and a layout it does not
/// take arrives as an OpenCV exception, which names no parameter a user can act
/// on; the refusal here names the frame instead.
/// </para>
/// </summary>
public sealed class BilateralFilterExecutor : INodeExecutor
{
    private static readonly FramePixelFormat[] AcceptedFormats =
    [
        FramePixelFormat.Gray8,
        FramePixelFormat.Bgr24,
    ];

    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the leases this executor creates.</param>
    public BilateralFilterExecutor(ILeaseLedger ledger)
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

        int diameter = request.Parameters.GetInt32(OpenCvNodeIds.DiameterParameter);
        double sigmaColor = request.Parameters.GetDouble(OpenCvNodeIds.SigmaColorParameter);
        double sigmaSpace = request.Parameters.GetDouble(OpenCvNodeIds.SigmaSpaceParameter);

        if (diameter < OpenCvParameterBounds.MinKernelSize || diameter > OpenCvParameterBounds.MaxKernelSize)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameter '{OpenCvNodeIds.DiameterParameter}' must be between " +
                    $"{OpenCvParameterBounds.MinKernelSize} and {OpenCvParameterBounds.MaxKernelSize}, but it is {diameter}.")));
        }

        if (!IsAcceptedSigma(sigmaColor) || !IsAcceptedSigma(sigmaSpace))
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"Parameters '{OpenCvNodeIds.SigmaColorParameter}' and '{OpenCvNodeIds.SigmaSpaceParameter}' must be between " +
                    $"{OpenCvParameterBounds.MinSigma} and {OpenCvParameterBounds.MaxBilateralSigma}, " +
                    $"but they are {sigmaColor} and {sigmaSpace}.")));
        }

        if (!AcceptedFormats.Contains(input.PixelFormat))
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"This node smooths an 8-bit greyscale or colour frame, but the input is {input.PixelFormat}.")));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // The result is a native buffer this executor creates. It is owned by the
        // returned lease once that exists, and by this method until then.
        var output = new Mat();
        bool transferred = false;

        try
        {
            Cv2.BilateralFilter(input.Mat, output, diameter, sigmaColor, sigmaSpace);

            MatFrameLease frame = MatFrameLease.Create(output, _ledger);
            transferred = true;

            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.BlurredPortId] = new ImageFrameValue(frame),
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

    private static bool IsAcceptedSigma(double value)
        => value >= OpenCvParameterBounds.MinSigma && value <= OpenCvParameterBounds.MaxBilateralSigma;
}
