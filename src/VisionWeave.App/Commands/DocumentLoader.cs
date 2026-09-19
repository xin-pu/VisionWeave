using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Commands;

/// <summary>
/// Opens a workflow document for the shell. The reader already reports an
/// unreadable file as a diagnostic, but a path it cannot open at all — missing,
/// locked, or not permitted — reaches the caller as a file error, and this is the
/// boundary that turns that expected outcome into the same stable diagnostic
/// instead of sending it to the log as an unanticipated failure.
/// </summary>
internal sealed class DocumentLoader : IDocumentLoader
{
    /// <inheritdoc />
    public WorkflowSessionResult Open(string path)
    {
        try
        {
            return WorkflowSession.Open(path);
        }
        catch (Exception exception) when (IsUnusablePath(exception))
        {
            return new WorkflowSessionResult(
                null,
                [
                    new NodeDiagnostic(
                        DiagnosticCodes.UnreadableDocument,
                        DiagnosticSeverity.Error,
                        "The workflow file could not be opened. Check that it exists and that you are allowed to read it.",
                        null,
                        exception),
                ]);
        }
    }

    /// <inheritdoc />
    public WorkflowSessionResult Recover(string path)
    {
        try
        {
            return WorkflowSession.Recover(path);
        }
        catch (Exception exception) when (IsUnusablePath(exception))
        {
            return new WorkflowSessionResult(
                null,
                [
                    new NodeDiagnostic(
                        DiagnosticCodes.UnreadableDocument,
                        DiagnosticSeverity.Error,
                        "The recovered workflow file could not be opened. Check that you are allowed to read it.",
                        null,
                        exception),
                ]);
        }
    }

    /// <inheritdoc />
    public bool HasWorkingCopy(string path) => WorkflowSession.HasWorkingCopy(path);

    /// <summary>
    /// The failures a path produces before any content is read or written. They are
    /// expected outcomes of naming a file, so both opening and saving report them as
    /// a diagnostic rather than as an unanticipated failure.
    /// </summary>
    internal static bool IsUnusablePath(Exception exception)
        => exception is System.IO.IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException;
}
