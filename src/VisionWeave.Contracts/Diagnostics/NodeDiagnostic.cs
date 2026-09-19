namespace VisionWeave.Contracts.Diagnostics;

/// <summary>
/// One reportable condition with a stable code, a message safe to show to the
/// user, and the node it belongs to when the condition is node-scoped.
/// </summary>
/// <param name="Code">A stable identifier from <see cref="DiagnosticCodes"/>.</param>
/// <param name="Severity">The severity of the condition.</param>
/// <param name="Message">A user-facing message that contains no secret data.</param>
/// <param name="NodeInstanceId">The node instance the diagnostic belongs to.</param>
/// <param name="Exception">The original failure, retained for structured logs only.</param>
public sealed record NodeDiagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    Guid? NodeInstanceId = null,
    Exception? Exception = null);
