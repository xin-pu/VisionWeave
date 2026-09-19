namespace VisionWeave.Contracts.Values;

/// <summary>
/// Carries up to four scalar components, such as a color or a threshold set.
/// </summary>
/// <param name="First">The first component.</param>
/// <param name="Second">The second component, or zero.</param>
/// <param name="Third">The third component, or zero.</param>
/// <param name="Fourth">The fourth component, or zero.</param>
public readonly record struct ScalarComponents(
    double First,
    double Second = 0,
    double Third = 0,
    double Fourth = 0);
