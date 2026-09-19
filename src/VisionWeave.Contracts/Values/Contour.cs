using System.Drawing;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// An ordered, closed sequence of points that describes one contour.
/// </summary>
/// <param name="Points">The contour points in traversal order.</param>
public sealed record Contour(IReadOnlyList<Point> Points)
{
    /// <summary>
    /// Gets the number of points in the contour.
    /// </summary>
    public int Count => Points.Count;
}
