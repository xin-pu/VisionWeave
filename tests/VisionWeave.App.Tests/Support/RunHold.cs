using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Execution;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// The executors a run resolves its nodes through, with the resize node replaced by one
/// that waits until the run is stopped. A test turns <see cref="Hold"/> on to hold a run
/// open at a point it can see, and off to run the same workflow to its end.
/// </summary>
internal sealed class RunHold : INodeExecutorResolver
{
    private readonly INodeExecutorResolver _executors;
    private readonly WaitingNode _node = new();
    private int _holding = 1;

    internal RunHold(INodeExecutorResolver executors)
    {
        ArgumentNullException.ThrowIfNull(executors);

        _executors = executors;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the resize node waits instead of resizing.
    /// It starts on, because holding a run open is why a test resolves its executors
    /// through this type at all.
    /// </summary>
    internal bool Hold
    {
        get => Volatile.Read(ref _holding) == 1;
        set => Volatile.Write(ref _holding, value ? 1 : 0);
    }

    /// <summary>Gets the task that completes once the run has reached the held node.</summary>
    internal Task Started => _node.Started;

    /// <inheritdoc />
    public bool TryResolve(string executorTypeId, out INodeExecutor? executor)
    {
        if (Hold && string.Equals(executorTypeId, OpenCvNodeIds.ResizeExecutorTypeId, StringComparison.Ordinal))
        {
            executor = _node;
            return true;
        }

        return _executors.TryResolve(executorTypeId, out executor);
    }

    /// <summary>
    /// Reports when it has started, then waits for the run to stop. It publishes nothing
    /// and holds no frame, so a run that stops at it is stopped between two nodes.
    /// </summary>
    private sealed class WaitingNode : INodeExecutor
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Started => _started.Task;

        /// <inheritdoc />
        public async Task<NodeExecutionResult> ExecuteAsync(
            NodeExecutionRequest request,
            CancellationToken cancellationToken)
        {
            _started.TrySetResult();

            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);

            throw new InvalidOperationException("A held node never finishes on its own.");
        }
    }
}
