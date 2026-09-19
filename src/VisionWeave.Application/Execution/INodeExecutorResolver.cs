using VisionWeave.Contracts.Execution;

namespace VisionWeave.Application.Execution;

/// <summary>
/// Resolves the executor a node definition names. The composition root owns the
/// registrations, so a plugin executor and a built-in executor are resolved the
/// same way and the runtime never guesses.
/// </summary>
public interface INodeExecutorResolver
{
    /// <summary>
    /// Resolves the executor registered for an executor type identifier.
    /// </summary>
    /// <param name="executorTypeId">The identifier the node definition declares.</param>
    /// <param name="executor">The resolved executor when it is registered.</param>
    /// <returns><see langword="true"/> when an executor is registered.</returns>
    bool TryResolve(string executorTypeId, out INodeExecutor? executor);
}
