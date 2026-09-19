using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Validation;

/// <summary>
/// Finds the connections that close a cycle in a workflow graph. It answers the
/// graph question alone and states what it found as the closing edge and the path
/// that leads back around to it, leaving the diagnostic codes, wording, and
/// attribution to the validator that reports them.
/// </summary>
internal static class CycleDetection
{
    /// <summary>
    /// Finds every edge that closes a cycle, in the order the search reaches it.
    /// </summary>
    /// <param name="nodeOrder">The nodes to start from, in the order to visit them.</param>
    /// <param name="adjacency">
    /// The consumers of each node. Only the edges that count are in it, so an edge
    /// the caller rejected is not walked here.
    /// </param>
    /// <returns>The closing edges, in the order they were found.</returns>
    internal static IReadOnlyList<ClosedCycle> Find(
        IReadOnlyList<Guid> nodeOrder,
        IReadOnlyDictionary<Guid, List<Guid>> adjacency)
    {
        ArgumentNullException.ThrowIfNull(nodeOrder);
        ArgumentNullException.ThrowIfNull(adjacency);

        List<ClosedCycle> closed = [];

        // An absent entry means unvisited, 1 means on the current path, and 2
        // means fully explored.
        Dictionary<Guid, int> state = [];
        List<Guid> path = [];

        foreach (Guid root in nodeOrder)
        {
            if (state.ContainsKey(root))
            {
                continue;
            }

            var stack = new Stack<(Guid Node, int NextConsumer)>();
            state[root] = 1;
            path.Add(root);
            stack.Push((root, 0));

            while (stack.Count > 0)
            {
                (Guid node, int nextConsumer) = stack.Pop();
                IReadOnlyList<Guid> consumers = ConsumersOf(adjacency, node);

                if (nextConsumer < consumers.Count)
                {
                    stack.Push((node, nextConsumer + 1));
                    Guid consumer = consumers[nextConsumer];

                    if (state.TryGetValue(consumer, out int consumerState))
                    {
                        if (consumerState == 1)
                        {
                            closed.Add(new ClosedCycle(node, consumer, DescribeCycle(path, consumer)));
                        }

                        continue;
                    }

                    state[consumer] = 1;
                    path.Add(consumer);
                    stack.Push((consumer, 0));
                    continue;
                }

                state[node] = 2;
                path.RemoveAt(path.Count - 1);
            }
        }

        return closed;
    }

    /// <summary>
    /// Determines whether adding one connection to a graph would close a cycle, and
    /// describes the cycle when it would. The candidate closes a cycle exactly when
    /// its target already reaches its source, which is what the search looks for.
    /// </summary>
    /// <param name="connections">The connections already in the graph.</param>
    /// <param name="candidate">The connection being considered.</param>
    /// <param name="cycle">The cycle the candidate would close.</param>
    /// <returns><see langword="true"/> when the candidate closes a cycle.</returns>
    internal static bool TryDescribeCandidate(
        IReadOnlyList<WorkflowConnection> connections,
        WorkflowConnection candidate,
        out string cycle)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(candidate);

        Dictionary<Guid, List<Guid>> adjacency = [];

        foreach (WorkflowConnection connection in connections)
        {
            AddEdge(adjacency, connection.SourceNodeId, connection.TargetNodeId);
        }

        AddEdge(adjacency, candidate.SourceNodeId, candidate.TargetNodeId);

        Dictionary<Guid, Guid> parent = [];
        Queue<Guid> pending = new();
        parent[candidate.TargetNodeId] = candidate.TargetNodeId;
        pending.Enqueue(candidate.TargetNodeId);

        while (pending.Count > 0)
        {
            Guid node = pending.Dequeue();

            if (node == candidate.SourceNodeId)
            {
                cycle = DescribeCandidateCycle(parent, candidate);
                return true;
            }

            foreach (Guid consumer in ConsumersOf(adjacency, node))
            {
                if (parent.TryAdd(consumer, node))
                {
                    pending.Enqueue(consumer);
                }
            }
        }

        cycle = string.Empty;
        return false;
    }

    private static IReadOnlyList<Guid> ConsumersOf(IReadOnlyDictionary<Guid, List<Guid>> adjacency, Guid node)
        => adjacency.TryGetValue(node, out List<Guid>? consumers) ? consumers : [];

    private static void AddEdge(Dictionary<Guid, List<Guid>> adjacency, Guid source, Guid target)
    {
        if (!adjacency.TryGetValue(source, out List<Guid>? consumers))
        {
            consumers = [];
            adjacency.Add(source, consumers);
        }

        consumers.Add(target);
    }

    /// <summary>
    /// Builds the cycle text from the search parents: the chain runs from the
    /// candidate's target back to its source, and the candidate edge closes it.
    /// </summary>
    private static string DescribeCandidateCycle(
        IReadOnlyDictionary<Guid, Guid> parent,
        WorkflowConnection candidate)
    {
        List<Guid> chain = [];
        Guid node = candidate.SourceNodeId;

        while (node != candidate.TargetNodeId)
        {
            chain.Add(node);
            node = parent[node];
        }

        chain.Reverse();
        List<Guid> cycle = [candidate.SourceNodeId, candidate.TargetNodeId, .. chain];
        return string.Join(" -> ", cycle.Select(item => item.ToString()));
    }

    private static string DescribeCycle(IReadOnlyList<Guid> path, Guid repeated)
    {
        List<string> steps = [];
        bool reached = false;

        foreach (Guid node in path)
        {
            reached |= node == repeated;

            if (reached)
            {
                steps.Add(node.ToString());
            }
        }

        steps.Add(repeated.ToString());
        return string.Join(" -> ", steps);
    }
}

/// <summary>
/// One connection that closes a cycle.
/// </summary>
/// <param name="From">The node the closing connection leaves.</param>
/// <param name="To">The node it reaches, which the search was already on.</param>
/// <param name="Cycle">The nodes of the cycle, starting and ending at <paramref name="To"/>.</param>
internal readonly record struct ClosedCycle(Guid From, Guid To, string Cycle);
