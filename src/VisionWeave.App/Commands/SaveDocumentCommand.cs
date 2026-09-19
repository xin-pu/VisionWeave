using CommunityToolkit.Mvvm.Input;
using VisionWeave.App.Sessions;
using VisionWeave.App.ViewModels;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Commands;

/// <summary>
/// Writes the document to its file, asking for a destination when it has none.
/// The write itself is small and atomic, so it runs where the gesture happened
/// rather than on a worker; the file dialog is the only interaction that waits for
/// the user, and that is the user's own. The boundary is still the one path the
/// work takes, so an unanticipated failure is logged once and reported as a
/// diagnostic instead of reaching the dispatcher.
/// </summary>
internal sealed class SaveDocumentCommand
{
    /// <summary>The stable name this operation is logged under.</summary>
    internal const string OperationName = "SaveDocument";

    /// <summary>The phrase the status area shows while the document is written.</summary>
    internal const string SavingText = "Saving the document…";

    private readonly AsyncCommandBoundary _boundary;
    private readonly EditorSession _session;
    private readonly ShellStatus _status;
    private readonly IWorkflowFileChooser _fileChooser;

    /// <summary>Creates the command.</summary>
    /// <param name="boundary">The boundary that runs the operation and reports its outcome.</param>
    /// <param name="session">The session whose document is written.</param>
    /// <param name="status">The shell state that reports what this command is doing.</param>
    /// <param name="fileChooser">The seam that asks for a destination when the document has none.</param>
    internal SaveDocumentCommand(
        AsyncCommandBoundary boundary,
        EditorSession session,
        ShellStatus status,
        IWorkflowFileChooser fileChooser)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(fileChooser);

        _boundary = boundary;
        _session = session;
        _status = status;
        _fileChooser = fileChooser;

        // The toolkit owns the command's state and its re-entry guard, so a save
        // cannot be started again while the first one is still writing.
        Command = new AsyncRelayCommand(SaveAsync, AsyncRelayCommandOptions.None);
    }

    /// <summary>Gets the command the shell binds and the execution state it exposes.</summary>
    internal IAsyncRelayCommand Command { get; }

    /// <summary>
    /// Gets the outcome of the last save, which a flow that has to know whether the
    /// document was written — saving before opening another one — reads instead of
    /// asking the file system. It is empty when nothing has been saved yet.
    /// </summary>
    internal IReadOnlyList<NodeDiagnostic> Outcome { get; private set; } = [];

    /// <summary>
    /// Gets a value indicating whether the last save wrote the document. A save that
    /// was refused, and one whose destination was dismissed, both leave this false:
    /// either way the document still holds changes its file does not.
    /// </summary>
    internal bool Saved { get; private set; }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        _status.Begin(SavingText);

        CommandExecutionResult result = await _boundary
            .RunAsync(OperationName, _ => Task.FromResult(Write()), cancellationToken)
            .ConfigureAwait(true);

        Outcome = result.Diagnostics;
        _status.Report(result);
    }

    /// <summary>
    /// Writes the document, first asking for a destination when it has never been
    /// saved. A dismissed dialog is not an intent, so nothing is written and nothing
    /// is reported.
    /// </summary>
    private IReadOnlyList<NodeDiagnostic> Write()
    {
        string? path = _session.Path;

        if (path is null)
        {
            path = _fileChooser.ChooseDocumentToSave(_session.Path, _session.Document.Name);
            if (path is null)
            {
                Saved = false;
                return [];
            }
        }

        IReadOnlyList<NodeDiagnostic> diagnostics = _session.SaveAs(path);
        Saved = diagnostics.Count == 0;

        return diagnostics;
    }
}
