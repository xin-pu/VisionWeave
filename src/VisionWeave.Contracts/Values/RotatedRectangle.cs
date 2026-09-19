using System.Drawing;

namespace VisionWeave.Contracts.Values;

/// <summary>
/// An oriented rectangle expressed by its center, extents, and rotation.
/// </summary>
/// <param name="Center">The rectangle center.</param>
/// <param name="Size">The width and height before rotation.</param>
/// <param name="AngleDegrees">The rotation in degrees, counter-clockwise.</param>
public readonly record struct RotatedRectangle(Point Center, Size Size, double AngleDegrees);
