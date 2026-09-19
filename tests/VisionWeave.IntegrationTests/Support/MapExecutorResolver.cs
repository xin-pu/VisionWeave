using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Execution;

namespace VisionWeave.IntegrationTests.Support;

/// <summary>
/// An executor written inline by a test, so that a test states the behaviour of
/// one node without declaring a type for it.
/// </summary>
internal sealed class DelegateExecutor : INodeExecutor
{
    private readonly Func<NodeExecutionRequest, CancellationToken, Task<NodeExecutionResult>> _execute;

    internal DelegateExecutor(Func<NodeExecutionRequest, CancellationToken, Task<NodeExecutionResult>> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
    }

    /// <inheritdoc />
    public Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CancellationToken cancellationToken)
        => _execute(request, cancellationToken);
}

/// <summary>
/// Resolves executors from a fixed map, which is how this test project wires the
/// built-in OpenCV registrations and its own source node together.
/// </summary>
internal sealed class MapExecutorResolver : INodeExecutorResolver
{
    private readonly IReadOnlyDictionary<string, INodeExecutor> _executors;

    internal MapExecutorResolver(IReadOnlyDictionary<string, INodeExecutor> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);
        _executors = executors;
    }

    /// <summary>
    /// Builds a resolver from the built-in OpenCV registrations plus the
    /// registrations a test adds.
    /// </summary>
    /// <param name="ledger">The ledger the built-in executors report their leases to.</param>
    /// <param name="additional">The additional registrations of the test.</param>
    /// <returns>The resolver.</returns>
    internal static MapExecutorResolver Create(
        ILeaseLedger ledger,
        params (string ExecutorTypeId, INodeExecutor Executor)[] additional)
    {
        var executors = new Dictionary<string, INodeExecutor>(OpenCvExecutors.CreateDefaults(ledger), StringComparer.Ordinal);

        foreach ((string executorTypeId, INodeExecutor executor) in additional)
        {
            executors[executorTypeId] = executor;
        }

        return new MapExecutorResolver(executors);
    }

    /// <inheritdoc />
    public bool TryResolve(string executorTypeId, out INodeExecutor? executor)
        => _executors.TryGetValue(executorTypeId, out executor);
}
