namespace VisionWeave.Application.Execution;

/// <summary>
/// The work one run has to perform: the nodes to execute in topological order,
/// their parallel batches, and the nodes that cannot run because a producer they
/// depend on is disabled. Nodes outside the requested scope are simply absent,
/// since their values come from cache instead of a new execution.
/// </summary>
public sealed record ExecutionPlan
{
    private readonly Dictionary<Guid, ExecutionPlanNode> _index;

    /// <summary>
    /// Initializes a plan.
    /// </summary>
    /// <param name="snapshot">The snapshot the plan was derived from.</param>
    /// <param name="nodes">The nodes to execute, in topological order.</param>
    /// <param name="levels">The parallel batches of the plan.</param>
    /// <param name="blockedNodeIds">The nodes inside the scope that cannot run.</param>
    public ExecutionPlan(
        WorkflowSnapshot snapshot,
        IEnumerable<ExecutionPlanNode> nodes,
        IEnumerable<ExecutionLevel> levels,
        IEnumerable<Guid> blockedNodeIds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(blockedNodeIds);

        Snapshot = snapshot;
        Nodes = [.. nodes];
        Levels = [.. levels];
        BlockedNodeIds = [.. blockedNodeIds];
        _index = Nodes.ToDictionary(node => node.InstanceId);
    }

    /// <summary>
    /// Gets the snapshot the plan was derived from.
    /// </summary>
    public WorkflowSnapshot Snapshot { get; }

    /// <summary>
    /// Gets the nodes to execute, in topological order.
    /// </summary>
    public IReadOnlyList<ExecutionPlanNode> Nodes { get; }

    /// <summary>
    /// Gets the parallel batches of the plan, each of which may only start after
    /// the previous level has completed.
    /// </summary>
    public IReadOnlyList<ExecutionLevel> Levels { get; }

    /// <summary>
    /// Gets the nodes that are part of the requested scope but cannot run,
    /// because a producer they require is disabled or is itself blocked. The
    /// runtime reports these as blocked instead of executing them.
    /// </summary>
    public IReadOnlyList<Guid> BlockedNodeIds { get; }

    /// <summary>
    /// Gets the identifiers of the nodes to execute, in topological order.
    /// </summary>
    public IReadOnlyList<Guid> ScheduledNodeIds => [.. Nodes.Select(node => node.InstanceId)];

    /// <summary>
    /// Gets a value indicating whether the plan has nothing to execute.
    /// </summary>
    public bool IsEmpty => Nodes.Count == 0;

    /// <summary>
    /// Tries to get a planned node.
    /// </summary>
    /// <param name="instanceId">The node instance identifier.</param>
    /// <param name="node">The planned node when it is scheduled.</param>
    /// <returns><see langword="true"/> when the node is scheduled.</returns>
    public bool TryGetNode(Guid instanceId, out ExecutionPlanNode? node)
        => _index.TryGetValue(instanceId, out node);

    /// <summary>
    /// Gets a planned node.
    /// </summary>
    /// <param name="instanceId">The node instance identifier.</param>
    /// <returns>The planned node.</returns>
    /// <exception cref="KeyNotFoundException">The node is not scheduled by this plan.</exception>
    public ExecutionPlanNode GetNode(Guid instanceId)
        => _index.TryGetValue(instanceId, out ExecutionPlanNode? node)
            ? node
            : throw new KeyNotFoundException($"Node instance '{instanceId}' is not scheduled by this plan.");
}
