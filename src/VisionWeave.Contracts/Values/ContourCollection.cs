namespace VisionWeave.Contracts.Values;

/// <summary>
/// An immutable set of contours produced by a single processing node.
/// </summary>
public sealed class ContourCollection
{
    private readonly Contour[] _contours;

    /// <summary>
    /// Initializes a collection from the supplied contours.
    /// </summary>
    /// <param name="contours">The contours in discovery order.</param>
    public ContourCollection(IEnumerable<Contour> contours)
    {
        ArgumentNullException.ThrowIfNull(contours);
        _contours = [.. contours];
    }

    /// <summary>
    /// Gets an empty collection.
    /// </summary>
    public static ContourCollection Empty { get; } = new([]);

    /// <summary>
    /// Gets the contours in discovery order.
    /// </summary>
    public IReadOnlyList<Contour> Contours => _contours;

    /// <summary>
    /// Gets the number of contours.
    /// </summary>
    public int Count => _contours.Length;
}
