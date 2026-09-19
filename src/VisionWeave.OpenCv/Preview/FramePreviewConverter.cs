using System.Runtime.InteropServices;
using OpenCvSharp;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;

namespace VisionWeave.OpenCv.Preview;

/// <summary>
/// Converts an image frame lease into a managed preview. Every result is a copy:
/// the preview holds no native resource, so it survives the release of the lease
/// it was converted from and the runtime never has to fence it.
/// <para>
/// A frame larger than <see cref="FramePreviewOptions.MaxPixelArea"/> is
/// downscaled with area interpolation, which is the cheapest filter that keeps a
/// downscaled preview representative. A frame at or below the bound is copied at
/// full size, so a small frame is not silently resampled.
/// </para>
/// </summary>
public sealed class FramePreviewConverter
{
    /// <summary>
    /// Gets the converter that obeys <see cref="FramePreviewOptions.Default"/>.
    /// </summary>
    public static FramePreviewConverter Default { get; } = new(FramePreviewOptions.Default);

    private readonly FramePreviewOptions _options;

    /// <summary>
    /// Initializes a converter with the given limits.
    /// </summary>
    /// <param name="options">The limits a conversion obeys.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The maximum pixel area is not positive.</exception>
    public FramePreviewConverter(FramePreviewOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxPixelArea <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MaxPixelArea,
                "The maximum preview pixel area must be positive.");
        }

        _options = options;
    }

    /// <summary>
    /// Gets the greatest number of pixels this converter produces.
    /// </summary>
    public int MaxPixelArea => _options.MaxPixelArea;

    /// <summary>
    /// Converts a frame into a managed preview, downscaling it when it exceeds
    /// <see cref="MaxPixelArea"/>.
    /// </summary>
    /// <param name="frame">The frame to convert. It is only read, never disposed.</param>
    /// <returns>The managed preview.</returns>
    /// <exception cref="ArgumentNullException">The frame is null.</exception>
    /// <exception cref="NotSupportedException">The pixel format cannot be previewed as bytes.</exception>
    public PreviewFrame Convert(MatFrameLease frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        int channels = ToPreviewChannelCount(frame.PixelFormat);
        (int width, int height) = ComputeTargetSize(frame.Width, frame.Height, MaxPixelArea);
        bool downscales = width != frame.Width || height != frame.Height;

        Mat? scaled = null;

        try
        {
            Mat source = frame.Mat;

            if (downscales)
            {
                scaled = new Mat();
                Cv2.Resize(source, scaled, new Size(width, height), interpolation: InterpolationFlags.Area);
                source = scaled;
            }

            int stridePerRow = width * channels;
            var pixels = new byte[stridePerRow * height];
            CopyRows(source, pixels, stridePerRow);

            return new PreviewFrame(width, height, frame.PixelFormat, stridePerRow, pixels);
        }
        finally
        {
            // The scaled intermediate is a native buffer this conversion created,
            // so it is freed here rather than left to the caller or the finalizer.
            scaled?.Dispose();
        }
    }

    /// <summary>
    /// Computes the size a frame is converted to so that its pixel count does not
    /// exceed the limit while its aspect ratio is preserved.
    /// </summary>
    /// <param name="width">The source width in pixels.</param>
    /// <param name="height">The source height in pixels.</param>
    /// <param name="maxPixelArea">The greatest number of pixels a preview may have.</param>
    /// <returns>The preview size. A frame within the limit keeps its size.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive or the limit is not positive.</exception>
    public static (int Width, int Height) ComputeTargetSize(int width, int height, int maxPixelArea)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPixelArea);

        long area = (long)width * height;

        if (area <= maxPixelArea)
        {
            return (width, height);
        }

        double scale = Math.Sqrt(maxPixelArea / (double)area);

        // Rounding can land one pixel over the limit, so the result is clamped
        // rather than trusted, and a preview is never empty.
        int targetWidth = Math.Clamp((int)(width * scale), 1, width);
        int targetHeight = Math.Clamp((int)(height * scale), 1, height);

        while ((long)targetWidth * targetHeight > maxPixelArea)
        {
            if (targetWidth >= targetHeight && targetWidth > 1)
            {
                targetWidth--;
            }
            else if (targetHeight > 1)
            {
                targetHeight--;
            }
            else
            {
                break;
            }
        }

        return (targetWidth, targetHeight);
    }

    private static int ToPreviewChannelCount(FramePixelFormat pixelFormat)
        => pixelFormat switch
        {
            FramePixelFormat.Gray8 => 1,
            FramePixelFormat.Bgr24 => 3,
            FramePixelFormat.Bgra32 => 4,
            _ => throw new NotSupportedException(
                $"Pixel format '{pixelFormat}' cannot be previewed: a preview is made of 8-bit channels."),
        };

    private static void CopyRows(Mat source, byte[] pixels, int stridePerRow)
    {
        if (source.IsContinuous())
        {
            Marshal.Copy(source.Data, pixels, 0, pixels.Length);
            return;
        }

        // A frame that is not continuous (a region of interest, for example) has a
        // row stride that exceeds the copied row length, so rows are copied one by
        // one instead of assuming a packed buffer.
        int rows = source.Rows;

        for (int row = 0; row < rows; row++)
        {
            Marshal.Copy(source.Ptr(row), pixels, row * stridePerRow, stridePerRow);
        }
    }
}
