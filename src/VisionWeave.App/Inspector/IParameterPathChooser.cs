using VisionWeave.Contracts.Nodes;

namespace VisionWeave.App.Inspector;

/// <summary>
///     Chooses a location for a path parameter without making the inspector depend
///     on a Windows dialog.
/// </summary>
internal interface IParameterPathChooser
{
    /// <summary>
    ///     Shows the picker described by <paramref name="mode"/>.
    /// </summary>
    /// <param name="mode">The kind of location the parameter accepts.</param>
    /// <param name="currentPath">The value currently displayed by the field.</param>
    /// <param name="workingDirectory">The workflow directory paths must stay inside.</param>
    /// <returns>The selected relative path, a refusal, or a dismissed choice.</returns>
    ParameterPathChoice ChoosePath(PathSelectionMode mode, string? currentPath, string? workingDirectory);
}

/// <summary>
///     The outcome of choosing a path parameter.
/// </summary>
/// <param name="Path">The portable path relative to the workflow directory.</param>
/// <param name="Refusal">Why the selected location cannot be stored.</param>
internal sealed record ParameterPathChoice(string? Path, string? Refusal)
{
    internal static ParameterPathChoice Dismissed { get; } = new(null, null);
}
