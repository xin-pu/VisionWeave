namespace VisionWeave.Contracts.Values;

/// <summary>
/// An immutable, ordered set of oriented rectangles.
/// </summary>
public sealed class RotatedRectangleCollection
{
    private readonly RotatedRectangle[] _rectangles;

    /// <summary>
    /// Initializes a collection from the supplied rectangles.
    /// </summary>
    /// <param name="rectangles">The rectangles in discovery order.</param>
    public RotatedRectangleCollection(IEnumerable<RotatedRectangle> rectangles)
    {
        ArgumentNullException.ThrowIfNull(rectangles);
        _rectangles = [.. rectangles];
    }

    /// <summary>
    /// Gets an empty collection.
    /// </summary>
    public static RotatedRectangleCollection Empty { get; } = new([]);

    /// <summary>
    /// Gets the rectangles in discovery order.
    /// </summary>
    public IReadOnlyList<RotatedRectangle> Rectangles => _rectangles;

    /// <summary>
    /// Gets the number of rectangles.
    /// </summary>
    public int Count => _rectangles.Length;
}
