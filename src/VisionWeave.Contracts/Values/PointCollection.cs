using System.Drawing;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// An immutable, ordered set of points.
/// </summary>
public sealed class PointCollection
{
    private readonly Point[] _points;

    /// <summary>
    /// Initializes a collection from the supplied points.
    /// </summary>
    /// <param name="points">The points in discovery order.</param>
    public PointCollection(IEnumerable<Point> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = [.. points];
    }

    /// <summary>
    /// Gets an empty collection.
    /// </summary>
    public static PointCollection Empty { get; } = new([]);

    /// <summary>
    /// Gets the points in discovery order.
    /// </summary>
    public IReadOnlyList<Point> Points => _points;

    /// <summary>
    /// Gets the number of points.
    /// </summary>
    public int Count => _points.Length;
}
