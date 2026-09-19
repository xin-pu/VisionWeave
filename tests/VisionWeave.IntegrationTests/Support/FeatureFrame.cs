using OpenCvSharp;

namespace VisionWeave.IntegrationTests.Support;

/// <summary>
/// Builds the smallest frames that hold a feature: a step between two flat regions,
/// across the columns or down the rows, a single brighter pixel in a flat frame, and
/// a single darker one. A node that reads a neighbourhood has to be tested against a
/// frame with something in it, and these are the features a test can state the result
/// of by hand. A frame of one value is the fourth, and it is what a node that marks a
/// shape rather than measures one is tested against: every pixel that is not the value
/// after the run is a pixel the node wrote.
/// </summary>
internal static class FeatureFrame
{
    private const int DefaultSize = 8;

    /// <summary>
    /// Builds a frame whose right half holds a brighter value than its left half.
    /// </summary>
    /// <param name="bright">The value of the right half.</param>
    /// <param name="dark">The value of the left half.</param>
    /// <param name="size">The width and height of the frame, which is square.</param>
    /// <returns>The frame, which the caller owns.</returns>
    internal static Mat AcrossColumns(byte bright, byte dark = 0, int size = DefaultSize)
        => Stepped(bright, dark, size, reachesDown: false);

    /// <summary>
    /// Builds a frame whose lower half holds a brighter value than its upper half.
    /// </summary>
    /// <param name="bright">The value of the lower half.</param>
    /// <param name="dark">The value of the upper half.</param>
    /// <param name="size">The width and height of the frame, which is square.</param>
    /// <returns>The frame, which the caller owns.</returns>
    internal static Mat DownRows(byte bright, byte dark = 0, int size = DefaultSize)
        => Stepped(bright, dark, size, reachesDown: true);

    /// <summary>
    /// Builds a flat frame with one brighter pixel in the middle, which is the
    /// smallest thing a neighbourhood operator can add to or take away.
    /// </summary>
    /// <param name="value">The value of the pixel.</param>
    /// <param name="field">The value of the frame around it.</param>
    /// <param name="size">The width and height of the frame, which is square.</param>
    /// <returns>The frame, which the caller owns.</returns>
    internal static Mat Spike(byte value, byte field = 0, int size = DefaultSize)
        => WithCentrePixel(value, field, size);

    /// <summary>
    /// Builds a flat frame with one darker pixel in the middle, which is the smallest
    /// gap an operator can fill in.
    /// </summary>
    /// <param name="value">The value of the pixel.</param>
    /// <param name="field">The value of the frame around it.</param>
    /// <param name="size">The width and height of the frame, which is square.</param>
    /// <returns>The frame, which the caller owns.</returns>
    internal static Mat Hole(byte value, byte field = 255, int size = DefaultSize)
        => WithCentrePixel(value, field, size);

    /// <summary>
    /// Builds a frame of one value, which holds nothing for a node to measure.
    /// </summary>
    /// <param name="value">The value of every pixel.</param>
    /// <param name="size">The width and height of the frame, which is square.</param>
    /// <returns>The frame, which the caller owns.</returns>
    internal static Mat Flat(byte value = 0, int size = DefaultSize)
        => new(size, size, MatType.CV_8UC1, Scalar.All(value));

    private static Mat Stepped(byte bright, byte dark, int size, bool reachesDown)
    {
        var mat = new Mat(size, size, MatType.CV_8UC1, Scalar.All(dark));
        int half = size / 2;

        // The brighter half is filled as a rectangle of the frame rather than as a
        // range view of it, because a view is a second owner of the buffer.
        Rect brighter = reachesDown
            ? new Rect(0, half, size, size - half)
            : new Rect(half, 0, size - half, size);

        Cv2.Rectangle(mat, brighter, Scalar.All(bright), thickness: -1);

        return mat;
    }

    private static Mat WithCentrePixel(byte value, byte field, int size)
    {
        var mat = new Mat(size, size, MatType.CV_8UC1, Scalar.All(field));
        mat.Set(size / 2, size / 2, value);

        return mat;
    }
}
