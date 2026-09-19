using VisionWeave.Application.Validation;

namespace VisionWeave.Application.Execution;

/// <summary>
/// The outcome of capturing and validating a document. Either the document could
/// be turned into an executable snapshot, or it could not and the diagnostics
/// explain why, which is what the editor shows instead of running.
/// </summary>
/// <param name="Snapshot">The executable snapshot, or <see langword="null"/> when validation failed.</param>
/// <param name="Validation">The diagnostics reported while capturing the snapshot.</param>
public sealed record SnapshotBuildResult(WorkflowSnapshot? Snapshot, ValidationResult Validation)
{
    /// <summary>
    /// Gets a value indicating whether the document could be captured as an
    /// executable snapshot.
    /// </summary>
    public bool Succeeded => Snapshot is not null;
}
