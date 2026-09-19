using System.Diagnostics;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Execution;

/// <summary>
/// Executes an execution plan: it starts the levels in order, limits how many
/// nodes run at once, publishes a node's outputs only after it succeeded, blocks
/// only the branch a failure belongs to, and releases every lease it owns whether
/// the run succeeded, failed, or was cancelled.
/// </summary>
public sealed class WorkflowRunner
{
    private readonly INodeExecutorResolver _executors;
    private readonly ILeaseLedger _ledger;
    private readonly ExecutionOptions _options;
    private readonly IExecutionOutputObserver? _observer;
    private readonly IExecutionInputSource? _inputs;

    /// <summary>
    /// Initializes the runner.
    /// </summary>
    /// <param name="executors">The resolver that maps a definition to its executor.</param>
    /// <param name="ledger">The ledger that observes native lease lifetime.</param>
    /// <param name="options">The limits of the run.</param>
    /// <param name="observer">The observer that renders previews, when there is one.</param>
    /// <param name="inputs">The source of values produced outside this plan, such as a cache.</param>
    public WorkflowRunner(
        INodeExecutorResolver executors,
        ILeaseLedger ledger,
        ExecutionOptions? options = null,
        IExecutionOutputObserver? observer = null,
        IExecutionInputSource? inputs = null)
    {
        ArgumentNullException.ThrowIfNull(executors);
        ArgumentNullException.ThrowIfNull(ledger);

        _executors = executors;
        _ledger = ledger;
        _options = options ?? ExecutionOptions.Default;
        _observer = observer;
        _inputs = inputs;
    }

    /// <summary>
    /// Executes a plan.
    /// </summary>
    /// <param name="plan">The plan to execute.</param>
    /// <param name="cancellationToken">Signals that the run should stop.</param>
    /// <returns>The summary of the run.</returns>
    public async Task<WorkflowRunSummary> RunAsync(ExecutionPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var context = new RunState(plan, Guid.NewGuid(), _ledger, _options, _inputs);
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            foreach (ExecutionLevel level in plan.Levels)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    context.WasCancelled = true;
                    break;
                }

                List<NodeWork> running = StartLevel(level, context, cancellationToken);

                if (!await CompleteLevelAsync(running, context, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }
        }
        finally
        {
            stopwatch.Stop();
            context.Stop();
        }

        return new WorkflowRunSummary
        {
            OperationId = context.OperationId,
            DocumentId = plan.Snapshot.DocumentId,
            Revision = plan.Snapshot.Revision,
            Duration = stopwatch.Elapsed,
            Nodes = context.CollectReports(),
            Diagnostics = context.CollectDiagnostics(cancellationToken, _ledger),
            WasCancelled = context.WasCancelled,
            QuarantinedNodeIds = context.QuarantinedNodeIds,
        };
    }

    private List<NodeWork> StartLevel(ExecutionLevel level, RunState context, CancellationToken cancellationToken)
    {
        List<NodeWork> running = [];

        foreach (Guid nodeId in level.NodeInstanceIds)
        {
            string? blockedReason = context.FindBlockedReason(nodeId);

            if (blockedReason is not null)
            {
                context.ReportBlocked(nodeId, blockedReason);
                continue;
            }

            running.Add(new NodeWork(nodeId, RunNodeTrackedAsync(nodeId, context, cancellationToken)));
        }

        return running;
    }

    private async Task<bool> CompleteLevelAsync(
        IReadOnlyList<NodeWork> running,
        RunState context,
        CancellationToken cancellationToken)
    {
        if (running.Count == 0)
        {
            return !cancellationToken.IsCancellationRequested;
        }

        Task all = Task.WhenAll(running.Select(item => item.Work));
        using var stopGraceWait = new CancellationTokenSource();
        Task grace = WaitForCancellationGraceAsync(cancellationToken, stopGraceWait.Token);

        try
        {
            if (ReferenceEquals(await Task.WhenAny(all, grace).ConfigureAwait(false), all))
            {
                await all.ConfigureAwait(false);
                return !cancellationToken.IsCancellationRequested;
            }

            foreach (NodeWork work in running.Where(item => !item.Work.IsCompleted))
            {
                context.Quarantine(work.NodeId);
            }

            context.WasCancelled = true;
            return false;
        }
        finally
        {
            stopGraceWait.Cancel();

            try
            {
                await grace.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopGraceWait.IsCancellationRequested)
            {
                // A completed level has no use for its pending cancellation wait.
            }
        }
    }

    private async Task WaitForCancellationGraceAsync(
        CancellationToken cancellationToken,
        CancellationToken stopWaitingToken)
    {
        Task cancellation = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        Task stopped = Task.Delay(Timeout.InfiniteTimeSpan, stopWaitingToken);

        if (!ReferenceEquals(await Task.WhenAny(cancellation, stopped).ConfigureAwait(false), cancellation))
        {
            return;
        }

        await Task.Delay(_options.CancellationGracePeriod, stopWaitingToken).ConfigureAwait(false);
    }

    private async Task RunNodeTrackedAsync(Guid nodeId, RunState context, CancellationToken cancellationToken)
    {
        bool acquired = false;

        try
        {
            await context.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            await RunNodeAsync(nodeId, context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            context.SetCancelled(
                nodeId,
                DiagnosticCodes.NodeExecutionCancelled,
                "The node was cancelled before it started.",
                []);
        }
        finally
        {
            if (acquired)
            {
                context.Gate.Release();
            }

            context.ReleaseNode(nodeId);
        }
    }

    private async Task RunNodeAsync(Guid nodeId, RunState context, CancellationToken cancellationToken)
    {
        ExecutionPlanNode planned = context.Plan.GetNode(nodeId);

        if (!_executors.TryResolve(planned.Node.Definition.ExecutorTypeId, out INodeExecutor? executor) || executor is null)
        {
            context.Fail(
                nodeId,
                DiagnosticCodes.MissingExecutor,
                $"No executor is registered for executor type identifier '{planned.Node.Definition.ExecutorTypeId}'.",
                null);
            return;
        }

        if (!context.TryBindInputs(nodeId, out NodeInputBindingResult binding))
        {
            context.ReportBlocked(nodeId, binding.Reason!);
            return;
        }

        var scope = new ExecutionResourceScope();
        context.RegisterScope(nodeId, scope);

        var request = new NodeExecutionRequest
        {
            NodeTypeId = planned.Node.Definition.TypeId,
            TypeVersion = planned.Node.Definition.TypeVersion,
            NodeInstanceId = nodeId,
            OperationId = context.OperationId,
            Parameters = planned.Node.Parameters,
            Inputs = binding.Values!,
            Resources = scope,
        };

        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            NodeExecutionResult result = await executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            await CompleteNodeAsync(nodeId, result, stopwatch.Elapsed, context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            context.SetCancelled(
                nodeId,
                DiagnosticCodes.NodeExecutionCancelled,
                "The node was cancelled while it was running.",
                []);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            context.Fail(
                nodeId,
                DiagnosticCodes.NodeExecutionFailed,
                $"The executor of node instance '{nodeId}' threw an exception.",
                exception);
        }
    }

    private async Task CompleteNodeAsync(
        Guid nodeId,
        NodeExecutionResult result,
        TimeSpan elapsed,
        RunState context,
        CancellationToken cancellationToken)
    {
        TimeSpan duration = result.Duration > TimeSpan.Zero ? result.Duration : elapsed;
        IReadOnlyList<NodeDiagnostic> diagnostics = context.NormalizeDiagnostics(nodeId, result);

        switch (result.Status)
        {
            case NodeExecutionStatus.Succeeded:
                await PublishAndObserveAsync(nodeId, result, duration, diagnostics, context, cancellationToken)
                    .ConfigureAwait(false);
                return;

            case NodeExecutionStatus.Cancelled:
                ReleaseUnpublishedOutputs(result.Outputs);
                context.SetCancelled(
                    nodeId,
                    DiagnosticCodes.NodeExecutionCancelled,
                    "The node observed cancellation.",
                    diagnostics);
                return;

            default:
                ReleaseUnpublishedOutputs(result.Outputs);
                context.Fail(
                    nodeId,
                    diagnostics.Count > 0 ? null : DiagnosticCodes.NodeExecutionFailed,
                    diagnostics.Count > 0
                        ? null
                        : $"Node instance '{nodeId}' reported a failure without a diagnostic.",
                    null,
                    duration,
                    diagnostics);
                return;
        }
    }

    private static void ReleaseUnpublishedOutputs(IReadOnlyDictionary<string, PortValue> outputs)
        => RunState.ReleaseOrphaned(outputs.Values.OfType<ImageFrameValue>().Select(value => value.Lease));

    /// <summary>
    /// Publishes the outputs of a node that succeeded and then lets the observer
    /// render previews. The observer's task is the conversion fence: the frame
    /// stays alive until it completes, and a preview failure is a warning, never
    /// a node failure.
    /// </summary>
    private async Task PublishAndObserveAsync(
        Guid nodeId,
        NodeExecutionResult result,
        TimeSpan duration,
        IReadOnlyList<NodeDiagnostic> diagnostics,
        RunState context,
        CancellationToken cancellationToken)
    {
        if (!context.TryPublishOutputs(
            nodeId,
            result.Outputs,
            _observer is not null,
            out List<LeaseFence> fences,
            out IReadOnlyList<IDisposable> orphaned))
        {
            RunState.ReleaseOrphaned(orphaned);
            context.SetCancelled(
                nodeId,
                DiagnosticCodes.NodeExecutionCancelled,
                "The node finished after the run had already stopped, so its outputs were released again.",
                diagnostics);
            return;
        }

        try
        {
            if (_observer is not null)
            {
                await _observer
                    .ObserveAsync(
                        new NodeOutputs(context.OperationId, nodeId, context.Plan.GetNode(nodeId).Node.Definition.TypeId, result.Outputs),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The run is stopping; the outputs stay published for their consumers.
        }
        catch (Exception exception)
        {
            context.ReportPreviewFailure(nodeId, exception);
        }
        finally
        {
            context.ReleaseFences(fences);
        }

        context.Succeed(nodeId, duration, diagnostics);
    }

    private sealed record NodeWork(Guid NodeId, Task Work);
}
