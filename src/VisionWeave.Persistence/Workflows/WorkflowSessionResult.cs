using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.Persistence.Workflows;

/// <summary>
/// The outcome of opening a session. A session is produced whenever the file
/// could be understood well enough to edit or display; a file that cannot be read
/// at all reports diagnostics instead.
/// </summary>
/// <param name="Session">The opened session, or <see langword="null"/> when the file could not be read.</param>
/// <param name="Diagnostics">The conditions observed while reading.</param>
public sealed record WorkflowSessionResult(WorkflowSession? Session, IReadOnlyList<NodeDiagnostic> Diagnostics)
{
    /// <summary>
    /// Gets a value indicating whether a session was produced.
    /// </summary>
    public bool Succeeded => Session is not null;
}
