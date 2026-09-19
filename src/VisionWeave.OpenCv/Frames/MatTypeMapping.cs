using OpenCvSharp;
using VisionWeave.Contracts.Values;

namespace VisionWeave.OpenCv.Frames;

/// <summary>
/// Maps between the contract pixel formats and the OpenCV mat types that carry
/// them. The mapping is explicit rather than inferred from element size, so an
/// unsupported layout is rejected instead of being reinterpreted silently.
/// </summary>
public static class MatTypeMapping
{
    /// <summary>
    /// Maps a mat type to the pixel format of an image frame.
    /// </summary>
    /// <param name="matType">The mat type to map.</param>
    /// <returns>The pixel format.</returns>
    /// <exception cref="NotSupportedException">The mat type has no frame pixel format.</exception>
    public static FramePixelFormat ToFramePixelFormat(MatType matType)
        => TryToFramePixelFormat(matType, out FramePixelFormat pixelFormat)
            ? pixelFormat
            : throw new NotSupportedException(
                $"Mat type '{matType}' has no image frame pixel format. Supported types are CV_8UC1, CV_8UC3, CV_8UC4, CV_16UC1, and CV_32FC1.");

    /// <summary>
    /// Tries to map a mat type to the pixel format of an image frame.
    /// </summary>
    /// <param name="matType">The mat type to map.</param>
    /// <param name="pixelFormat">The pixel format when the type is supported.</param>
    /// <returns><see langword="true"/> when the mat type is supported.</returns>
    public static bool TryToFramePixelFormat(MatType matType, out FramePixelFormat pixelFormat)
    {
        if (matType == MatType.CV_8UC1)
        {
            pixelFormat = FramePixelFormat.Gray8;
            return true;
        }

        if (matType == MatType.CV_8UC3)
        {
            pixelFormat = FramePixelFormat.Bgr24;
            return true;
        }

        if (matType == MatType.CV_8UC4)
        {
            pixelFormat = FramePixelFormat.Bgra32;
            return true;
        }

        if (matType == MatType.CV_16UC1)
        {
            pixelFormat = FramePixelFormat.Gray16;
            return true;
        }

        if (matType == MatType.CV_32FC1)
        {
            pixelFormat = FramePixelFormat.Float32;
            return true;
        }

        pixelFormat = default;
        return false;
    }

    /// <summary>
    /// Maps a pixel format to the mat type that carries it.
    /// </summary>
    /// <param name="pixelFormat">The pixel format to map.</param>
    /// <returns>The mat type.</returns>
    /// <exception cref="NotSupportedException">The pixel format has no mat type.</exception>
    public static MatType ToMatType(FramePixelFormat pixelFormat)
        => pixelFormat switch
        {
            FramePixelFormat.Gray8 => MatType.CV_8UC1,
            FramePixelFormat.Bgr24 => MatType.CV_8UC3,
            FramePixelFormat.Bgra32 => MatType.CV_8UC4,
            FramePixelFormat.Gray16 => MatType.CV_16UC1,
            FramePixelFormat.Float32 => MatType.CV_32FC1,
            _ => throw new NotSupportedException($"Pixel format '{pixelFormat}' has no mat type."),
        };

    /// <summary>
    /// Gets the number of channels a pixel format stores per pixel.
    /// </summary>
    /// <param name="pixelFormat">The pixel format.</param>
    /// <returns>The channel count.</returns>
    /// <exception cref="NotSupportedException">The pixel format has no channel layout.</exception>
    public static int ToChannelCount(FramePixelFormat pixelFormat)
        => pixelFormat switch
        {
            FramePixelFormat.Gray8 => 1,
            FramePixelFormat.Bgr24 => 3,
            FramePixelFormat.Bgra32 => 4,
            FramePixelFormat.Gray16 => 1,
            FramePixelFormat.Float32 => 1,
            _ => throw new NotSupportedException($"Pixel format '{pixelFormat}' has no channel layout."),
        };
}
