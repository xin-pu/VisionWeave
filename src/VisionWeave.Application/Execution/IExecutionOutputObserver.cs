namespace VisionWeave.Application.Execution;

/// <summary>
/// Observes the outputs a node publishes, which is where preview rendering
/// attaches. The task an observer returns is the preview conversion fence: the
/// runtime keeps the published image leases alive until it completes, so a
/// conversion can never read a frame that was already released.
/// </summary>
public interface IExecutionOutputObserver
{
    /// <summary>
    /// Observes the outputs of a node that just succeeded.
    /// </summary>
    /// <param name="outputs">The published outputs.</param>
    /// <param name="cancellationToken">Signals that the run is stopping.</param>
    /// <returns>A task that completes when the observer no longer needs the outputs.</returns>
    Task ObserveAsync(NodeOutputs outputs, CancellationToken cancellationToken);
}
