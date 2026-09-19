namespace VisionWeave.OpenCv.Preview;

/// <summary>
/// The limits a preview conversion obeys. They are explicit so that a preview can
/// never retain a full-size native frame and so that a test pins the bound
/// instead of depending on the machine it runs on.
/// </summary>
public sealed record FramePreviewOptions
{
    /// <summary>
    /// Gets the default options, which bound a preview to the pixels of a
    /// full-HD frame.
    /// </summary>
    public static FramePreviewOptions Default { get; } = new();

    /// <summary>
    /// Gets the greatest number of pixels a converted preview may have. A larger
    /// frame is downscaled to fit.
    /// </summary>
    public int MaxPixelArea { get; init; } = 1920 * 1080;
}
