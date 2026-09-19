using System.Globalization;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.OpenCv.Preview;

/// <summary>
/// The limits a preview conversion obeys. They are explicit so that a preview can
/// never retain a full-size native frame and so that a test pins the bound
/// instead of depending on the machine it runs on.
/// </summary>
public sealed record FramePreviewOptions
{
    /// <summary>
    /// Gets the smallest pixel area a preview may allow. A preview still has to
    /// be shown, so a bound of zero would make every conversion meaningless.
    /// </summary>
    public const int MinPixelArea = 1;

    /// <summary>
    /// Gets the largest pixel area a preview may allow. It covers an 8K frame, so
    /// the bound only rejects a typo that would otherwise disable downscaling
    /// altogether.
    /// </summary>
    public const int MaxPixelAreaLimit = 7680 * 4320;

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

    /// <summary>
    /// Checks that the preview bound is one the converter can honor.
    /// </summary>
    /// <returns>The problems found, or an empty list.</returns>
    public IReadOnlyList<NodeDiagnostic> Validate()
    {
        if (MaxPixelArea is >= MinPixelArea and <= MaxPixelAreaLimit)
        {
            return [];
        }

        return
        [
            new NodeDiagnostic(
                DiagnosticCodes.InvalidSetting,
                DiagnosticSeverity.Error,
                $"Setting '{nameof(FramePreviewOptions)}.{nameof(MaxPixelArea)}' is {MaxPixelArea.ToString(CultureInfo.InvariantCulture)}, but it must be a pixel count between {MinPixelArea.ToString(CultureInfo.InvariantCulture)} and {MaxPixelAreaLimit.ToString(CultureInfo.InvariantCulture)}.",
                null),
        ];
    }
}
