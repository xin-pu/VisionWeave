using Microsoft.Extensions.Logging;
using VisionWeave.App.Notifications;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Commands;

/// <summary>
/// The single place a shell command runs its work. It keeps an unexpected failure
/// from reaching the UI thread, turns it into one safe diagnostic, and logs it
/// once. A failure the operation anticipated is reported by the operation itself
/// as a diagnostic, so the boundary never has to know what "expected" means for a
/// particular command.
/// </summary>
internal sealed class AsyncCommandBoundary
{
    private readonly IUserNotificationPresenter _presenter;
    private readonly ILogger<AsyncCommandBoundary> _logger;

    /// <summary>
    /// Creates the boundary. The constructor is public because the host's service
    /// container resolves this type and only considers public constructors; the
    /// type itself stays internal to the application.
    /// </summary>
    /// <param name="presenter">The one place user-facing messages leave the shell.</param>
    /// <param name="logger">The log that records an unexpected failure once.</param>
    public AsyncCommandBoundary(IUserNotificationPresenter presenter, ILogger<AsyncCommandBoundary> logger)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(logger);

        _presenter = presenter;
        _logger = logger;
    }

    /// <summary>
    /// Runs an operation and reports its outcome. The returned task never faults,
    /// so a caller that binds a command cannot raise an unobserved exception on
    /// the UI thread.
    /// </summary>
    /// <param name="operationName">The stable name the operation is logged under.</param>
    /// <param name="operation">The work to run, which reports the conditions it observed.</param>
    /// <param name="cancellationToken">The token whose cancellation the caller requested.</param>
    /// <returns>The outcome of the operation.</returns>
    internal async Task<CommandExecutionResult> RunAsync(
        string operationName,
        Func<CancellationToken, Task<IReadOnlyList<NodeDiagnostic>>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            IReadOnlyList<NodeDiagnostic> diagnostics = await operation(cancellationToken).ConfigureAwait(false);
            PresentReportedFailure(diagnostics);

            return new CommandExecutionResult(CommandCompletion.Completed, diagnostics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller asked to stop, so nothing failed: no diagnostic, no
            // message, and no error in the log.
            return new CommandExecutionResult(CommandCompletion.Cancelled, []);
        }
        catch (Exception exception)
        {
            NodeDiagnostic diagnostic = new(
                DiagnosticCodes.UnexpectedCommandFailure,
                DiagnosticSeverity.Error,
                $"The {operationName} command failed unexpectedly and was stopped. The failure was written to the log.",
                null,
                exception);

            _logger.LogError(
                exception,
                "Command {OperationName} failed unexpectedly with {DiagnosticCode}.",
                operationName,
                diagnostic.Code);

            _presenter.Present(diagnostic);

            return new CommandExecutionResult(CommandCompletion.Failed, [diagnostic]);
        }
    }

    /// <summary>
    /// Shows the first failure an operation reported. A warning or an
    /// informational condition stays in the result for the status area and the
    /// diagnostics panel instead of interrupting the user with a dialog.
    /// </summary>
    private void PresentReportedFailure(IReadOnlyList<NodeDiagnostic> diagnostics)
    {
        foreach (NodeDiagnostic diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                _presenter.Present(diagnostic);
                return;
            }
        }
    }
}
