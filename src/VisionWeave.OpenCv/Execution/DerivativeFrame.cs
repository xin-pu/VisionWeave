using OpenCvSharp;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Reports a signed derivative as the frame a node publishes. A derivative is
/// signed and a frame is not, so what a node reports is the absolute value of every
/// pixel, saturated to eight bits, in the layout of the frame it was computed from:
/// the pair of calls OpenCV's own documentation uses to make a derivative visible,
/// and the reason a derivative node declares a scale.
/// <para>
/// The temporary the caller computed the derivative into is the caller's to
/// release; this reports the absolute value in a frame of its own.
/// </para>
/// </summary>
internal static class DerivativeFrame
{
    private static readonly FramePixelFormat[] AcceptedFormats =
    [
        FramePixelFormat.Gray8,
        FramePixelFormat.Bgr24,
        FramePixelFormat.Bgra32,
    ];

    /// <summary>
    /// Determines whether a frame is one the derivative nodes measure. The report is
    /// an absolute value in eight bits, so a deeper frame would need a range these
    /// nodes do not declare.
    /// </summary>
    /// <param name="pixelFormat">The format of the input frame.</param>
    /// <returns><see langword="true"/> when the layout is measured.</returns>
    internal static bool Accepts(FramePixelFormat pixelFormat) => AcceptedFormats.Contains(pixelFormat);

    /// <summary>
    /// Names the frame a derivative node refuses.
    /// </summary>
    /// <param name="pixelFormat">The format of the input frame.</param>
    /// <returns>The user-facing explanation.</returns>
    internal static string Refusal(FramePixelFormat pixelFormat)
        => $"This node measures the gradient of an 8-bit frame, but the input is {pixelFormat}.";

    /// <summary>
    /// Reports the absolute value of a derivative as the frame of the given port.
    /// </summary>
    /// <param name="derivative">The signed result of a derivative call.</param>
    /// <param name="ledger">The ledger that observes the lease the frame creates.</param>
    /// <param name="portId">The output port that carries the frame.</param>
    /// <returns>The successful result of the node.</returns>
    internal static NodeExecutionResult Report(Mat derivative, ILeaseLedger ledger, string portId)
    {
        // The result is a native buffer this method creates. It is owned by the
        // returned lease once that exists, and by this method until then.
        var absolute = new Mat();
        bool transferred = false;

        try
        {
            Cv2.ConvertScaleAbs(derivative, absolute);

            MatFrameLease frame = MatFrameLease.Create(absolute, ledger);
            transferred = true;

            return NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [portId] = new ImageFrameValue(frame),
                });
        }
        finally
        {
            if (!transferred)
            {
                absolute.Dispose();
            }
        }
    }
}
