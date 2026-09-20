using System.Windows.Media.Imaging;

namespace VisionWeave.App.Tests.Support;

/// <summary>Reads pixels back out of a bitmap a preview produced.</summary>
internal static class BitmapPixels
{
    /// <summary>
    /// The first pixel of an image, which is how one tile is told from its siblings when
    /// the tiles share a shape and a layout. The row stride is the one the bitmap's own
    /// pixel layout needs, because a copy reads whole rows.
    /// </summary>
    /// <param name="image">The image to read.</param>
    /// <returns>The first byte of the first row.</returns>
    internal static byte First(BitmapSource image)
    {
        ArgumentNullException.ThrowIfNull(image);

        int stride = (image.PixelWidth * image.Format.BitsPerPixel + 7) / 8;
        var pixels = new byte[stride * image.PixelHeight];

        image.CopyPixels(pixels, stride, 0);

        return pixels[0];
    }
}
