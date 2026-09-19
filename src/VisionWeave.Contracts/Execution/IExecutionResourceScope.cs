namespace VisionWeave.Contracts.Execution;

/// <summary>
/// Disposes the private resources an executor creates while it runs, whatever
/// the outcome. Registration is the executor's obligation; disposal belongs to
/// the runtime.
/// </summary>
public interface IExecutionResourceScope
{
    /// <summary>
    /// Registers a resource for disposal when the node finishes, fails, or is
    /// cancelled. Refuses to accept a scope that has already completed.
    /// </summary>
    /// <param name="resource">The resource to own.</param>
    void Own(IDisposable resource);
}
