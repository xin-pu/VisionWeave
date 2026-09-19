using VisionWeave.Contracts.Execution;

namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// An executor that runs whatever the test tells it to, and counts how often it
/// was invoked so a test can prove that a node ran exactly once.
/// </summary>
internal sealed class StubExecutor : INodeExecutor
{
    private readonly Func<NodeExecutionRequest, CancellationToken, Task<NodeExecutionResult>> _run;
    private int _invocations;

    internal StubExecutor(Func<NodeExecutionRequest, CancellationToken, Task<NodeExecutionResult>> run)
        => _run = run;

    internal int Invocations => Volatile.Read(ref _invocations);

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _invocations);
        return _run(request, cancellationToken);
    }
}
