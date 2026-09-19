using CommunityToolkit.Mvvm.Input;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Commands;

/// <summary>
/// The representative document command: it opens the file named by
/// <see cref="Path"/> and hands the loaded session to whoever subscribed. It
/// exists to prove the command and error boundary end to end before the canvas
/// adds commands of its own.
/// </summary>
internal sealed class OpenDocumentCommand
{
    /// <summary>The stable name this operation is logged under.</summary>
    internal const string OperationName = "OpenDocument";

    private readonly AsyncCommandBoundary _boundary;
    private readonly IDocumentLoader _loader;

    /// <summary>Creates the command.</summary>
    /// <param name="boundary">The boundary that runs the operation and reports its outcome.</param>
    /// <param name="loader">The loader that turns a path into a session.</param>
    internal OpenDocumentCommand(AsyncCommandBoundary boundary, IDocumentLoader loader)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(loader);

        _boundary = boundary;
        _loader = loader;

        // The toolkit owns the command's state: IsRunning, CanExecute, the token
        // source behind Cancel, and the notifications that report them. Concurrent
        // executions stay disabled, so a running open cannot be started again from
        // the shell.
        Command = new AsyncRelayCommand(OpenFromPathAsync, AsyncRelayCommandOptions.None);
    }

    /// <summary>Gets the command the shell binds and the execution state it exposes.</summary>
    internal IAsyncRelayCommand Command { get; }

    /// <summary>
    /// Gets or sets the file this command opens. One command instance is bound
    /// once, so the file arrives at execution time rather than at construction:
    /// the shell's file chooser fills it before the command runs.
    /// </summary>
    internal string? Path { get; set; }

    /// <summary>Raised with the loaded session once a document was opened.</summary>
    internal event EventHandler<WorkflowSession>? Opened;

    /// <summary>
    /// Opens the file and reports the session. The boundary is the only path to the
    /// loader, so the task this returns cannot fault, and the final await
    /// deliberately returns to the context that asked for the command: every state
    /// change — the toolkit's IsRunning and CanExecute notifications and the
    /// <see cref="Opened"/> event — is then raised on the thread that owns that
    /// context, and a <c>ConfigureAwait(false)</c> here would move them onto a
    /// worker thread where a binding cannot use them.
    /// </summary>
    private async Task OpenFromPathAsync(CancellationToken cancellationToken)
    {
        WorkflowSession? opened = null;
        string path = Path ?? string.Empty;

        CommandExecutionResult result = await _boundary
            .RunAsync(
                OperationName,
                async token =>
                {
                    // Reading a file is blocking work, so it runs off the calling
                    // thread and the shell keeps responding to input while it waits.
                    WorkflowSessionResult loaded = await Task
                        .Run(() => _loader.Open(path), token)
                        .ConfigureAwait(false);

                    // A stopped command must not go on to replace the document the
                    // shell is editing, and the read itself cannot be interrupted.
                    token.ThrowIfCancellationRequested();

                    opened = loaded.Session;
                    return loaded.Diagnostics;
                },
                cancellationToken)
            .ConfigureAwait(true);

        if (result.Succeeded && opened is not null)
        {
            Opened?.Invoke(this, opened);
        }
    }
}
