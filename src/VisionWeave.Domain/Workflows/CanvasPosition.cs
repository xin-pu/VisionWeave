namespace VisionWeave.Domain.Workflows;

/// <summary>
/// The canvas position of a node instance. Position is document state; viewport
/// and zoom are not.
/// </summary>
/// <param name="X">The horizontal canvas coordinate.</param>
/// <param name="Y">The vertical canvas coordinate.</param>
public readonly record struct CanvasPosition(double X, double Y);
