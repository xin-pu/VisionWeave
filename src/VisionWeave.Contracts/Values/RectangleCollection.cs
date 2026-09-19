using System.Drawing;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// An immutable, ordered set of axis-aligned rectangles.
/// </summary>
public sealed class RectangleCollection
{
    private readonly Rectangle[] _rectangles;

    /// <summary>
    /// Initializes a collection from the supplied rectangles.
    /// </summary>
    /// <param name="rectangles">The rectangles in discovery order.</param>
    public RectangleCollection(IEnumerable<Rectangle> rectangles)
    {
        ArgumentNullException.ThrowIfNull(rectangles);
        _rectangles = [.. rectangles];
    }

    /// <summary>
    /// Gets an empty collection.
    /// </summary>
    public static RectangleCollection Empty { get; } = new([]);

    /// <summary>
    /// Gets the rectangles in discovery order.
    /// </summary>
    public IReadOnlyList<Rectangle> Rectangles => _rectangles;

    /// <summary>
    /// Gets the number of rectangles.
    /// </summary>
    public int Count => _rectangles.Length;
}
