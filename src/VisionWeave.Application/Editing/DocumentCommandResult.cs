using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.Application.Editing;

/// <summary>
/// The outcome of one editing attempt: whether the document changed, and the
/// diagnostics that explain a refusal. A refused edit leaves the document
/// untouched, so a caller reports the diagnostics and shows the previous state
/// without having to roll anything back. Nothing here throws, because an editing
/// attempt is a user intent that can be answered, not a programming error.
/// </summary>
public sealed class DocumentCommandResult
{
    private DocumentCommandResult(bool isAccepted, IReadOnlyList<NodeDiagnostic> diagnostics)
    {
        IsAccepted = isAccepted;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets the outcome of an edit that changed the document.
    /// </summary>
    public static DocumentCommandResult Accepted { get; } = new(true, []);

    /// <summary>
    /// Gets a value indicating whether the document changed.
    /// </summary>
    public bool IsAccepted { get; }

    /// <summary>
    /// Gets the diagnostics that explain a refusal.
    /// </summary>
    public IReadOnlyList<NodeDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Creates the outcome of a refused edit.
    /// </summary>
    /// <param name="diagnostics">The diagnostics that explain the refusal.</param>
    /// <returns>The refused outcome.</returns>
    public static DocumentCommandResult Refused(IEnumerable<NodeDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return new DocumentCommandResult(false, [.. diagnostics]);
    }

    /// <summary>
    /// Creates the outcome of a refused edit that reports one condition.
    /// </summary>
    /// <param name="code">A stable code from <see cref="DiagnosticCodes"/>.</param>
    /// <param name="message">A user-facing message.</param>
    /// <param name="nodeInstanceId">The node the condition belongs to, when it is node-scoped.</param>
    /// <returns>The refused outcome.</returns>
    public static DocumentCommandResult Refused(string code, string message, Guid? nodeInstanceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new DocumentCommandResult(
            false,
            [new NodeDiagnostic(code, DiagnosticSeverity.Error, message, nodeInstanceId)]);
    }
}
