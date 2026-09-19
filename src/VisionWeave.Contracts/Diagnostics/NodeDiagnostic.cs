namespace VisionWeave.Contracts.Diagnostics;

/// <summary>
/// One reportable condition with a stable code, a message safe to show to the
/// user, the node it belongs to when the condition is node-scoped, and the
/// document element it describes when that element is narrower than the node.
/// </summary>
/// <param name="Code">A stable identifier from <see cref="DiagnosticCodes"/>.</param>
/// <param name="Severity">The severity of the condition.</param>
/// <param name="Message">A user-facing message that contains no secret data.</param>
/// <param name="NodeInstanceId">The node instance the diagnostic belongs to.</param>
/// <param name="Exception">The original failure, retained for structured logs only.</param>
/// <param name="Target">
/// The document element the condition is about, or <see langword="null"/> when it
/// is about the named node as a whole, or about the document when no node is
/// named. A caller that only reads <paramref name="NodeInstanceId"/> sees no
/// change, and a caller that reads the target can tell a port, a parameter, and a
/// connection apart without parsing the message.
/// </param>
public sealed record NodeDiagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    Guid? NodeInstanceId = null,
    Exception? Exception = null,
    DiagnosticTarget? Target = null);
