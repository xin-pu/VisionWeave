namespace VisionWeave.Contracts.Values;

/// <summary>
/// Describes the channel layout and element size of an image frame.
/// </summary>
public enum FramePixelFormat
{
    /// <summary>Single 8-bit channel.</summary>
    Gray8,

    /// <summary>Three 8-bit channels ordered blue, green, red.</summary>
    Bgr24,

    /// <summary>Four 8-bit channels ordered blue, green, red, alpha.</summary>
    Bgra32,

    /// <summary>Single 16-bit channel.</summary>
    Gray16,

    /// <summary>Single 32-bit floating point channel.</summary>
    Float32,
}
