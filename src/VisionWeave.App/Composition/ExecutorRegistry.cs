using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Execution;

namespace VisionWeave.App.Composition;

/// <summary>
/// Resolves a node definition's executor by the executor type identifier it
/// declares. The registrations of every provider this build ships arrive here as
/// one dictionary, so a plan never names a concrete executor and a plugin's
/// registration joins on the same terms as a built-in one.
/// </summary>
internal sealed class ExecutorRegistry : INodeExecutorResolver
{
    private readonly IReadOnlyDictionary<string, INodeExecutor> _executors;

    /// <summary>
    /// Creates the registry over the executors the OpenCV layer contributes.
    /// </summary>
    /// <param name="ledger">The ledger the executors report the leases they create to.</param>
    internal ExecutorRegistry(ILeaseLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        _executors = OpenCvExecutors.CreateDefaults(ledger);
    }

    /// <inheritdoc />
    public bool TryResolve(string executorTypeId, out INodeExecutor? executor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executorTypeId);

        bool found = _executors.TryGetValue(executorTypeId, out INodeExecutor? resolved);

        executor = resolved;

        return found;
    }
}
