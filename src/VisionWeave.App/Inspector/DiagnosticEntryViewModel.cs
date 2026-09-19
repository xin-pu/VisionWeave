using VisionWeave.App.Presentation;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Inspector;

/// <summary>
/// One condition the inspector lists: how serious it is, the stable code, the
/// message, and the element it points at. It is immutable and derived from the
/// diagnostic, so a row never describes a revision the document has moved past.
/// </summary>
internal sealed class DiagnosticEntryViewModel
{
    /// <summary>
    /// Creates a row for one condition.
    /// </summary>
    /// <param name="diagnostic">The condition to present.</param>
    internal DiagnosticEntryViewModel(NodeDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        Severity = diagnostic.Severity;
        Code = diagnostic.Code;
        Message = diagnostic.Message;
        Target = DiagnosticText.TargetOf(diagnostic.Target);
        Text = DiagnosticText.Of(diagnostic);
        Summary = $"{Text}{Environment.NewLine}Points at {Target}.";
    }

    /// <summary>Gets how serious the condition is, which also picks the row's colour.</summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the severity as a word, so a row states how serious it is in text as
    /// well as in colour.
    /// </summary>
    public string SeverityWord => SeverityText.Of(Severity);

    /// <summary>Gets the stable identifier a report or a log entry can be matched against.</summary>
    public string Code { get; }

    /// <summary>Gets the message, which contains no secret data.</summary>
    public string Message { get; }

    /// <summary>
    /// Gets the element the condition points at, named in words: a parameter, a
    /// port, a connection, or the document itself.
    /// </summary>
    public string Target { get; }

    /// <summary>Gets the condition worded the way the status area words it.</summary>
    public string Text { get; }

    /// <summary>Gets what the row says when the pointer rests on it.</summary>
    public string Summary { get; }
}
