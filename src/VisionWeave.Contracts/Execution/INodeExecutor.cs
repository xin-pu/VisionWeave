namespace VisionWeave.Contracts.Execution;

/// <summary>
/// Runs one node of a workflow. Implementations are expected to honor the
/// cancellation token, treat input values as read-only, register every private
/// resource in the request scope, and report expected failures as diagnostics
/// instead of throwing.
/// </summary>
public interface INodeExecutor
{
    /// <summary>
    /// Executes the node.
    /// </summary>
    /// <param name="request">The validated inputs and context for this run.</param>
    /// <param name="cancellationToken">Signals that the run is being cancelled.</param>
    /// <returns>The terminal result of the execution.</returns>
    Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CancellationToken cancellationToken);
}
