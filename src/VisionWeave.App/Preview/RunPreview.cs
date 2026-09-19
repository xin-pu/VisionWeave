using System.Windows.Media.Imaging;
using VisionWeave.Contracts.Values;

namespace VisionWeave.App.Preview;

/// <summary>
/// One image a run published, as something the shell can draw and keep after the run is
/// over. It carries the copy rather than the frame the copy was made from, so a preview
/// is never a claim on a lease the runtime has released.
/// </summary>
/// <param name="Image">The bitmap to draw, frozen: it is built where the frame was converted and read where the window is drawn.</param>
/// <param name="NodeTitle">The node that published it, named the way the catalogue names it.</param>
/// <param name="Width">The bitmap's width, which is the frame's own unless it was larger than a preview may be.</param>
/// <param name="Height">The bitmap's height.</param>
/// <param name="PixelFormat">The pixel layout the node produced.</param>
internal sealed record RunPreview(
    BitmapSource Image,
    string NodeTitle,
    int Width,
    int Height,
    FramePixelFormat PixelFormat);
