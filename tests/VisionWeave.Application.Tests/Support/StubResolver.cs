using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Execution;

namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// Maps executor type identifiers to the executors a test registers, so that a
/// run resolves exactly the executors the test intends and nothing else.
/// </summary>
internal sealed class StubResolver : INodeExecutorResolver
{
    private readonly Dictionary<string, INodeExecutor> _executors = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers an executor under an executor type identifier.
    /// </summary>
    /// <param name="executorTypeId">The identifier a definition declares.</param>
    /// <param name="executor">The executor to resolve.</param>
    /// <returns>This resolver, so registrations can be chained.</returns>
    internal StubResolver Add(string executorTypeId, INodeExecutor executor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executorTypeId);
        ArgumentNullException.ThrowIfNull(executor);

        _executors[executorTypeId] = executor;
        return this;
    }

    /// <summary>
    /// Registers a stub executor that runs the supplied delegate.
    /// </summary>
    /// <param name="executorTypeId">The identifier a definition declares.</param>
    /// <param name="run">The delegate that produces the node result.</param>
    /// <returns>This resolver, so registrations can be chained.</returns>
    internal StubResolver Add(
        string executorTypeId,
        Func<NodeExecutionRequest, CancellationToken, Task<NodeExecutionResult>> run)
        => Add(executorTypeId, new StubExecutor(run));

    /// <inheritdoc />
    public bool TryResolve(string executorTypeId, out INodeExecutor? executor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executorTypeId);
        return _executors.TryGetValue(executorTypeId, out executor);
    }
}
