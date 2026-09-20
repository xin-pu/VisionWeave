namespace VisionWeave.Contracts.Nodes;

/// <summary>
/// Describes one user-editable parameter of a node definition. The schema is
/// UI-neutral: it states kind, bounds, and options, and never names a control.
/// </summary>
/// <param name="Name">The stable parameter name used in documents and requests.</param>
/// <param name="Kind">The kind of value the parameter accepts.</param>
/// <param name="IsRequired">Whether a value must be supplied before execution.</param>
/// <param name="DisplayName">The label shown in the property inspector.</param>
/// <param name="Minimum">The inclusive lower bound for numeric parameters.</param>
/// <param name="Maximum">The inclusive upper bound for numeric parameters.</param>
/// <param name="Options">The accepted values for an option parameter.</param>
/// <param name="DefaultValue">The value applied when the document omits the parameter.</param>
/// <param name="PathSelection">The kind of location a path parameter asks the user to choose.</param>
public sealed record ParameterDefinition(
    string Name,
    ParameterKind Kind,
    bool IsRequired,
    string DisplayName,
    double? Minimum = null,
    double? Maximum = null,
    IReadOnlyList<string>? Options = null,
    object? DefaultValue = null,
    PathSelectionMode PathSelection = PathSelectionMode.OpenFile);
