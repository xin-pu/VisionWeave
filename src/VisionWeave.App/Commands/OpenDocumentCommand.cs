using CommunityToolkit.Mvvm.Input;
using VisionWeave.App.Sessions;
using VisionWeave.App.ViewModels;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Commands;

/// <summary>
/// Opens the file named by <see cref="Path"/> into the editing session. It is the
/// shell's entry point to the session: the session itself decides what an opened
/// file means, and it announces the result, so this command only carries the
/// running state, the failure boundary around the read, and the outcome the status
/// area reports.
/// </summary>
internal sealed class OpenDocumentCommand
{
    /// <summary>The stable name this operation is logged under.</summary>
    internal const string OperationName = "OpenDocument";

    /// <summary>The phrase the status area shows while the read is running.</summary>
    internal const string OpeningText = "Opening a document…";

    private readonly AsyncCommandBoundary _boundary;
    private readonly EditorSession _session;
    private readonly ShellStatus _status;

    /// <summary>Creates the command.</summary>
    /// <param name="boundary">The boundary that runs the operation and reports its outcome.</param>
    /// <param name="session">The session the opened document replaces.</param>
    /// <param name="status">The shell state that reports what this command is doing.</param>
    internal OpenDocumentCommand(AsyncCommandBoundary boundary, EditorSession session, ShellStatus status)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(status);

        _boundary = boundary;
        _session = session;
        _status = status;

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

    /// <summary>
    /// Opens the file into the session. The boundary is the only path to the read,
    /// so the task this returns cannot fault, and the final await deliberately
    /// returns to the context that asked for the command: the session's change
    /// notifications are then raised on the thread that owns that context, and a
    /// <c>ConfigureAwait(false)</c> here would move them onto a worker thread where
    /// a binding cannot use them.
    /// </summary>
    private async Task OpenFromPathAsync(CancellationToken cancellationToken)
    {
        string path = Path ?? string.Empty;

        _status.Begin(OpeningText);

        CommandExecutionResult result = await _boundary
            .RunAsync(
                OperationName,
                async token =>
                {
                    // Reading a file is blocking work, so it runs off the calling
                    // thread and the shell keeps responding to input while it waits.
                    // The session refuses the result when this command has been
                    // stopped, so a cancelled open cannot replace the document the
                    // shell is editing.
                    WorkflowSessionResult opened = await Task
                        .Run(() => _session.Open(path, token), token)
                        .ConfigureAwait(false);

                    return opened.Diagnostics;
                },
                cancellationToken)
            .ConfigureAwait(true);

        _status.Report(result);
    }
}
