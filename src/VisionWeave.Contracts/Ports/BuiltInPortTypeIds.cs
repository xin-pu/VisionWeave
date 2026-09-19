namespace VisionWeave.Contracts.Ports;

/// <summary>
/// Declares the compatibility rules for the built-in port types.
/// </summary>
public static class BuiltInPortTypeIds
{
    /// <summary>A leased, read-only image frame.</summary>
    public static readonly PortTypeId ImageFrame = new("visionweave.type.image-frame");

    /// <summary>An immutable contour set.</summary>
    public static readonly PortTypeId ContourCollection = new("visionweave.type.contour-collection");

    /// <summary>A single axis-aligned rectangle.</summary>
    public static readonly PortTypeId Rectangle = new("visionweave.type.rectangle");

    /// <summary>An immutable rectangle set.</summary>
    public static readonly PortTypeId RectangleCollection = new("visionweave.type.rectangle-collection");

    /// <summary>An immutable point set.</summary>
    public static readonly PortTypeId PointCollection = new("visionweave.type.point-collection");

    /// <summary>An immutable set of oriented rectangles.</summary>
    public static readonly PortTypeId RotatedRectangleCollection = new("visionweave.type.rotated-rectangle-collection");

    /// <summary>Up to four scalar components, such as a color.</summary>
    public static readonly PortTypeId Scalar = new("visionweave.type.scalar");

    /// <summary>A numeric measurement or parameter.</summary>
    public static readonly PortTypeId Number = new("visionweave.type.number");

    /// <summary>A boolean measurement or decision.</summary>
    public static readonly PortTypeId Boolean = new("visionweave.type.boolean");

    /// <summary>Textual output.</summary>
    public static readonly PortTypeId Text = new("visionweave.type.text");

    /// <summary>
    /// Gets every built-in port type identifier.
    /// </summary>
    public static IReadOnlyList<PortTypeId> All { get; } =
    [
        ImageFrame,
        ContourCollection,
        Rectangle,
        RectangleCollection,
        PointCollection,
        RotatedRectangleCollection,
        Scalar,
        Number,
        Boolean,
        Text,
    ];
}
