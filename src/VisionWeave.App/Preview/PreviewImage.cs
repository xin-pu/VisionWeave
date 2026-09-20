using System.Windows.Media.Imaging;

namespace VisionWeave.App.Preview;

/// <summary>
/// One image the preview region draws: the bitmap, what the full-size viewer calls it, and
/// the name of the output port it arrived on. A node that published a single image needs no
/// port to tell it apart, so its label stays empty and the tile draws no name for it. The
/// members are public because the markup binds to them, which WPF does by reflection.
/// </summary>
/// <param name="Image">The bitmap to draw.</param>
/// <param name="Caption">What the viewer calls the image, which names the port when the node published several.</param>
/// <param name="Label">The port name the tile shows, or an empty string when the node published one image.</param>
internal sealed record PreviewImage(BitmapSource Image, string Caption, string Label)
{
    /// <summary>Gets a value indicating whether the tile shows the name of the port.</summary>
    public bool ShowsLabel => Label.Length > 0;
}
