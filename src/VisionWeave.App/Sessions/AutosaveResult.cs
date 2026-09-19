using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Sessions;

/// <summary>
/// The outcome of one autosave attempt together with the conditions it observed,
/// so an autosave caller never has to interpret an exception: a scheduler reads
/// <see cref="Outcome"/> to decide whether to keep asking, and the shell reads
/// <see cref="Diagnostics"/> to explain a refusal or a failure. A failure is an
/// expected result of naming a file, which is why it travels as a diagnostic and
/// keeps its exception only for structured logging.
/// </summary>
/// <param name="Outcome">How the attempt finished.</param>
/// <param name="Diagnostics">The conditions the attempt produced, empty when nothing went wrong.</param>
internal sealed record AutosaveResult(
    AutosaveOutcome Outcome,
    IReadOnlyList<NodeDiagnostic> Diagnostics)
{
    /// <summary>Gets the result of an attempt that found nothing to write.</summary>
    internal static AutosaveResult NotApplicable { get; } = new(AutosaveOutcome.NotApplicable, []);

    /// <summary>Gets the result of an attempt that wrote the working copy.</summary>
    internal static AutosaveResult Written { get; } = new(AutosaveOutcome.Written, []);

    /// <summary>Gets a value indicating whether the working copy was written.</summary>
    internal bool Succeeded => Outcome == AutosaveOutcome.Written;

    /// <summary>
    /// Creates the result of an attempt the session refused before it touched the
    /// file.
    /// </summary>
    /// <param name="code">The stable diagnostic code that names the refusal.</param>
    /// <param name="message">The message that explains the refusal.</param>
    /// <returns>The refused result.</returns>
    internal static AutosaveResult Refused(string code, string message)
        => new(AutosaveOutcome.Refused, [new NodeDiagnostic(code, DiagnosticSeverity.Error, message, null)]);

    /// <summary>
    /// Creates the result of an attempt the file system refused.
    /// </summary>
    /// <param name="code">The stable diagnostic code that names the failure.</param>
    /// <param name="message">The message that explains the failure.</param>
    /// <param name="exception">The exception, which is kept for structured logging and never shown.</param>
    /// <returns>The failed result.</returns>
    internal static AutosaveResult Failed(string code, string message, Exception exception)
        => new(AutosaveOutcome.Failed, [new NodeDiagnostic(code, DiagnosticSeverity.Error, message, null, exception)]);
}
