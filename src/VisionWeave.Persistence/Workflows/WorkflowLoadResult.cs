using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Persistence.Workflows;

/// <summary>
/// The outcome of reading a stored workflow document. A document is returned
/// whenever the file could be understood well enough to display; a file that
/// cannot be understood at all reports diagnostics instead of a document.
/// </summary>
/// <param name="Document">The loaded document, or <see langword="null"/> when the file could not be read.</param>
/// <param name="SchemaVersion">The schema version the file declared.</param>
/// <param name="IsReadOnly">
/// Whether the file must not be saved over, because its schema version is not one
/// this build can migrate.
/// </param>
/// <param name="Diagnostics">The conditions observed while reading.</param>
public sealed record WorkflowLoadResult(
    WorkflowDocument? Document,
    int SchemaVersion,
    bool IsReadOnly,
    IReadOnlyList<NodeDiagnostic> Diagnostics)
{
    /// <summary>
    /// Gets a value indicating whether a document was produced.
    /// </summary>
    public bool Succeeded => Document is not null;
}
