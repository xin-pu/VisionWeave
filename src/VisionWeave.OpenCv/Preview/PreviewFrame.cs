using VisionWeave.Contracts.Values;

namespace VisionWeave.OpenCv.Preview;

/// <summary>
/// A managed, downscaled copy of an image frame. It holds no native resource, so
/// a preview can outlive the lease it was converted from; the rendering layer
/// turns it into the bitmap type of its UI framework.
/// </summary>
/// <param name="Width">The preview width in pixels.</param>
/// <param name="Height">The preview height in pixels.</param>
/// <param name="PixelFormat">The pixel layout of the copied pixels.</param>
/// <param name="StridePerRow">The number of bytes one row occupies.</param>
/// <param name="Pixels">The copied pixel bytes, top-down and without padding.</param>
public sealed record PreviewFrame(
    int Width,
    int Height,
    FramePixelFormat PixelFormat,
    int StridePerRow,
    byte[] Pixels);
