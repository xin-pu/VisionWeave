using System.Windows.Media;
using System.Windows.Media.Imaging;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.App.Preview;

/// <summary>
/// Turns the managed preview the OpenCV layer produced into the bitmap WPF draws.
/// Every pixel format the converter produces has a drawing format of the same layout,
/// so a preview is drawn as the frame it came from was laid out.
/// </summary>
internal static class RunPreviewImage
{
    private const double Dpi = 96;

    internal static BitmapSource Create(PreviewFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        BitmapSource image = BitmapSource.Create(
            frame.Width,
            frame.Height,
            Dpi,
            Dpi,
            ToDrawingFormat(frame.PixelFormat),
            palette: null,
            frame.Pixels,
            frame.StridePerRow);

        // A bitmap built from bytes belongs to the thread that built it, and the
        // conversion runs where the run executes rather than where the window is drawn.
        image.Freeze();

        return image;
    }

    private static PixelFormat ToDrawingFormat(FramePixelFormat pixelFormat)
        => pixelFormat switch
        {
            FramePixelFormat.Gray8 => PixelFormats.Gray8,
            FramePixelFormat.Bgr24 => PixelFormats.Bgr24,
            FramePixelFormat.Bgra32 => PixelFormats.Bgra32,
            _ => throw new NotSupportedException(
                $"Pixel format '{pixelFormat}' cannot be drawn: a preview is made of 8-bit channels."),
        };
}
