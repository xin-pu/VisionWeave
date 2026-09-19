using VisionWeave.Contracts.Ports;

namespace VisionWeave.Application.Execution;

/// <summary>
/// Turns a validated snapshot into the work of one run. The builder marks the
/// changed nodes and everything downstream of them as dirty, so a parameter edit
/// re-runs only the affected branch, and it sorts the result into levels that
/// the runtime can execute in parallel.
/// </summary>
public sealed class ExecutionPlanBuilder
{
    /// <summary>
    /// Builds the plan for a run.
    /// </summary>
    /// <param name="snapshot">A validated snapshot.</param>
    /// <param name="changedNodeIds">
    /// The nodes whose own state changed, or <see langword="null"/> to run the
    /// whole snapshot.
    /// </param>
    /// <returns>The plan.</returns>
    /// <exception cref="KeyNotFoundException">A changed node is not in the snapshot.</exception>
    /// <exception cref="InvalidOperationException">The snapshot contains a cycle.</exception>
    public ExecutionPlan Build(WorkflowSnapshot snapshot, IEnumerable<Guid>? changedNodeIds = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Dictionary<Guid, List<Guid>> consumersBySource = [];
        Dictionary<Guid, List<Guid>> producersByTarget = [];
        Dictionary<Guid, List<NodeInputBinding>> inputsByTarget = [];

        foreach (SnapshotEdge edge in snapshot.Edges)
        {
            Add(consumersBySource, edge.SourceNodeId, edge.TargetNodeId);
            Add(producersByTarget, edge.TargetNodeId, edge.SourceNodeId);
            Add(inputsByTarget, edge.TargetNodeId, new NodeInputBinding(edge.TargetPortId, edge.SourceNodeId, edge.SourcePortId));
        }

        List<Guid> allNodes = [.. snapshot.Nodes.Select(node => node.InstanceId)];
        List<Guid> wholeOrder = SortTopologically(allNodes, consumersBySource, producersByTarget);

        HashSet<Guid> unavailable = ResolveUnavailableNodes(snapshot, wholeOrder, inputsByTarget);
        HashSet<Guid> scope = ResolveScope(snapshot, changedNodeIds, consumersBySource);
        List<Guid> scheduled = [.. scope.Where(id => !unavailable.Contains(id))];
        List<Guid> order = SortTopologically(scheduled, consumersBySource, producersByTarget);
        Dictionary<Guid, int> levels = ComputeLevels(order, producersByTarget);

        return new ExecutionPlan(
            snapshot,
            order.Select(id => new ExecutionPlanNode(snapshot.GetNode(id), [.. InputsOf(inputsByTarget, id)])),
            BuildLevels(order, levels),
            scope.Where(unavailable.Contains).OrderBy(id => id));
    }

    private static HashSet<Guid> ResolveScope(
        WorkflowSnapshot snapshot,
        IEnumerable<Guid>? changedNodeIds,
        IReadOnlyDictionary<Guid, List<Guid>> consumersBySource)
    {
        if (changedNodeIds is null)
        {
            return [.. snapshot.Nodes.Select(node => node.InstanceId)];
        }

        HashSet<Guid> scope = [.. changedNodeIds];

        foreach (Guid id in scope)
        {
            if (!snapshot.TryGetNode(id, out _))
            {
                throw new KeyNotFoundException($"Node instance '{id}' is not part of the snapshot.");
            }
        }

        Queue<Guid> pending = new(scope);

        while (pending.Count > 0)
        {
            foreach (Guid consumer in ConsumersOf(consumersBySource, pending.Dequeue()))
            {
                if (scope.Add(consumer))
                {
                    pending.Enqueue(consumer);
                }
            }
        }

        return scope;
    }

    private static HashSet<Guid> ResolveUnavailableNodes(
        WorkflowSnapshot snapshot,
        IReadOnlyList<Guid> wholeOrder,
        IReadOnlyDictionary<Guid, List<NodeInputBinding>> inputsByTarget)
    {
        HashSet<Guid> unavailable = [.. snapshot.Nodes.Where(node => !node.IsEnabled).Select(node => node.InstanceId)];

        // wholeOrder is topological, so every producer has already been decided
        // by the time its consumers are examined.
        foreach (Guid id in wholeOrder)
        {
            if (unavailable.Contains(id))
            {
                continue;
            }

            foreach (NodeInputBinding binding in InputsOf(inputsByTarget, id))
            {
                if (!unavailable.Contains(binding.SourceNodeId) || !IsRequiredInput(snapshot, id, binding.TargetPortId))
                {
                    continue;
                }

                unavailable.Add(id);
                break;
            }
        }

        return unavailable;
    }

    private static bool IsRequiredInput(WorkflowSnapshot snapshot, Guid targetNodeId, string portId)
    {
        PortDefinition? port = snapshot.GetNode(targetNodeId).Definition.FindPort(portId);
        return port?.IsOptional != true;
    }

    private static List<Guid> SortTopologically(
        IReadOnlyList<Guid> nodes,
        IReadOnlyDictionary<Guid, List<Guid>> consumersBySource,
        IReadOnlyDictionary<Guid, List<Guid>> producersByTarget)
    {
        HashSet<Guid> included = [.. nodes];
        Dictionary<Guid, int> remainingProducers = [];
        SortedSet<Guid> ready = [];
        List<Guid> order = [];

        foreach (Guid id in nodes)
        {
            int count = ProducersOf(producersByTarget, id).Count(producer => included.Contains(producer));
            remainingProducers[id] = count;

            if (count == 0)
            {
                ready.Add(id);
            }
        }

        while (ready.Count > 0)
        {
            Guid id = ready.Min;
            ready.Remove(id);
            order.Add(id);

            foreach (Guid consumer in ConsumersOf(consumersBySource, id))
            {
                if (!included.Contains(consumer))
                {
                    continue;
                }

                remainingProducers[consumer]--;

                if (remainingProducers[consumer] == 0)
                {
                    ready.Add(consumer);
                }
            }
        }

        if (order.Count != nodes.Count)
        {
            throw new InvalidOperationException(
                "The snapshot contains a cycle; capture it from a validated document before building a plan.");
        }

        return order;
    }

    private static Dictionary<Guid, int> ComputeLevels(
        IReadOnlyList<Guid> order,
        IReadOnlyDictionary<Guid, List<Guid>> producersByTarget)
    {
        HashSet<Guid> included = [.. order];
        Dictionary<Guid, int> levels = [];

        foreach (Guid id in order)
        {
            int level = 0;

            foreach (Guid producer in ProducersOf(producersByTarget, id))
            {
                if (included.Contains(producer))
                {
                    level = Math.Max(level, levels[producer] + 1);
                }
            }

            levels[id] = level;
        }

        return levels;
    }

    private static IEnumerable<ExecutionLevel> BuildLevels(
        IReadOnlyList<Guid> order,
        IReadOnlyDictionary<Guid, int> levels)
        => order
            .GroupBy(id => levels[id])
            .OrderBy(group => group.Key)
            .Select(group => new ExecutionLevel(group.Key, [.. group]));

    private static IReadOnlyList<NodeInputBinding> InputsOf(
        IReadOnlyDictionary<Guid, List<NodeInputBinding>> inputsByTarget,
        Guid nodeId)
        => inputsByTarget.TryGetValue(nodeId, out List<NodeInputBinding>? inputs) ? inputs : [];

    private static IReadOnlyList<Guid> ConsumersOf(IReadOnlyDictionary<Guid, List<Guid>> consumersBySource, Guid nodeId)
        => consumersBySource.TryGetValue(nodeId, out List<Guid>? consumers) ? consumers : [];

    private static IReadOnlyList<Guid> ProducersOf(IReadOnlyDictionary<Guid, List<Guid>> producersByTarget, Guid nodeId)
        => producersByTarget.TryGetValue(nodeId, out List<Guid>? producers) ? producers : [];

    private static void Add<TValue>(Dictionary<Guid, List<TValue>> map, Guid key, TValue value)
    {
        if (!map.TryGetValue(key, out List<TValue>? values))
        {
            values = [];
            map.Add(key, values);
        }

        values.Add(value);
    }
}
