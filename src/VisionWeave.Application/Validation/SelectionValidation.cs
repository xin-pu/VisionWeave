using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.Application.Validation;

/// <summary>
/// The validation state of a selection: the diagnostics that apply to it and the
/// severity it should present. It is produced by
/// <see cref="ValidationProjection.Select"/> and carries no selection identity of
/// its own, so it stays meaningful when the selection moves on.
/// </summary>
public sealed class SelectionValidation
{
    /// <summary>
    /// Initializes a selection state from the diagnostics that apply to it.
    /// </summary>
    /// <param name="diagnostics">The applicable diagnostics, in a deterministic order.</param>
    public SelectionValidation(IReadOnlyList<NodeDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        Diagnostics = diagnostics;
        Severity = HighestSeverity(diagnostics);
    }

    /// <summary>
    /// Gets the diagnostics that apply to the selection.
    /// </summary>
    public IReadOnlyList<NodeDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets the highest severity among the applicable diagnostics, or
    /// <see langword="null"/> when there is none.
    /// </summary>
    public DiagnosticSeverity? Severity { get; }

    /// <summary>
    /// Gets a value indicating whether the selection is free of blocking errors.
    /// </summary>
    public bool IsValid => Severity != DiagnosticSeverity.Error;

    private static DiagnosticSeverity? HighestSeverity(IReadOnlyList<NodeDiagnostic> diagnostics)
    {
        DiagnosticSeverity? highest = null;

        foreach (NodeDiagnostic diagnostic in diagnostics)
        {
            if (highest is not { } current || diagnostic.Severity > current)
            {
                highest = diagnostic.Severity;
            }
        }

        return highest;
    }
}
