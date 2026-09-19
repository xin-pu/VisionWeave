using VisionWeave.Application.Definitions;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Validation;

/// <summary>
/// Decides whether a workflow document is executable. The document model itself
/// stays permissive so a loaded or half-edited workflow can always be displayed
/// and saved; this validator is where definition availability, connection
/// legality, and cycle freedom are decided, reporting stable diagnostics instead
/// of throwing.
/// </summary>
public sealed class WorkflowValidator
{
    private readonly NodeDefinitionCatalog _catalog;
    private readonly PortCompatibility _compatibility;

    /// <summary>
    /// Initializes the validator.
    /// </summary>
    /// <param name="catalog">The node definitions available to this build.</param>
    /// <param name="compatibility">The declared port compatibility relation.</param>
    public WorkflowValidator(NodeDefinitionCatalog catalog, PortCompatibility? compatibility = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
        _compatibility = compatibility ?? PortCompatibility.BuiltIn;
    }

    /// <summary>
    /// Validates a document without changing it.
    /// </summary>
    /// <param name="document">The document to validate.</param>
    /// <returns>The reported diagnostics in a deterministic order.</returns>
    public ValidationResult Validate(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Node instances are visited in identifier order so that a given document
        // always produces the same diagnostic sequence.
        List<NodeInstance> nodes = [.. document.Nodes.OrderBy(node => node.InstanceId)];
        Dictionary<Guid, NodeDefinition> resolved = [];
        List<NodeDiagnostic> diagnostics = [];

        foreach (NodeInstance node in nodes)
        {
            ResolveNode(node, resolved, diagnostics);
        }

        ValidateConnections(document, nodes, resolved, diagnostics);

        return new ValidationResult(diagnostics);
    }

    private void ResolveNode(
        NodeInstance node,
        Dictionary<Guid, NodeDefinition> resolved,
        List<NodeDiagnostic> diagnostics)
    {
        if (_catalog.TryResolve(node.NodeTypeId, node.TypeVersion, out NodeDefinition? definition))
        {
            resolved[node.InstanceId] = definition!;
            return;
        }

        if (_catalog.TryResolveLatest(node.NodeTypeId, out NodeDefinition? latest))
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.UnsupportedNodeVersion,
                DiagnosticSeverity.Error,
                $"Node instance '{node.InstanceId}' is saved as version {node.TypeVersion} of node type '{node.NodeTypeId}', and this build cannot migrate it; the newest version is {latest!.TypeVersion}.",
                node.InstanceId));
            return;
        }

        diagnostics.Add(new NodeDiagnostic(
            DiagnosticCodes.MissingNodeDefinition,
            DiagnosticSeverity.Error,
            $"Node instance '{node.InstanceId}' uses node type '{node.NodeTypeId}', which is not provided by this build.",
            node.InstanceId));
    }

    private void ValidateConnections(
        WorkflowDocument document,
        IReadOnlyList<NodeInstance> nodes,
        IReadOnlyDictionary<Guid, NodeDefinition> resolved,
        List<NodeDiagnostic> diagnostics)
    {
        Dictionary<Guid, NodeInstance> byId = nodes.ToDictionary(node => node.InstanceId);
        Dictionary<Guid, List<Guid>> adjacency = [];

        // Multiplicity counts connections, not values, so the count is kept per
        // target port across the whole document.
        Dictionary<(Guid NodeId, string PortId), int> incoming = [];

        // Every connection that names a real target node marks its port as bound,
        // even when the connection itself is rejected later on.
        HashSet<(Guid NodeId, string PortId)> boundInputs = [];

        // Connections are examined in identifier order, which keeps the
        // diagnostics stable and preserves declaration order for fan-in.
        foreach (WorkflowConnection connection in document.Connections.OrderBy(item => item.ConnectionId))
        {
            if (!byId.TryGetValue(connection.SourceNodeId, out NodeInstance? source)
                || !byId.TryGetValue(connection.TargetNodeId, out NodeInstance? target))
            {
                diagnostics.Add(new NodeDiagnostic(
                    DiagnosticCodes.InvalidGraph,
                    DiagnosticSeverity.Error,
                    $"Connection '{connection.ConnectionId}' refers to a node instance that is not in the document.",
                    null));
                continue;
            }

            boundInputs.Add((connection.TargetNodeId, connection.TargetPortId));

            if (connection.SourceNodeId == connection.TargetNodeId)
            {
                diagnostics.Add(new NodeDiagnostic(
                    DiagnosticCodes.InvalidGraph,
                    DiagnosticSeverity.Error,
                    $"Node instance '{connection.SourceNodeId}' is connected to itself.",
                    connection.SourceNodeId));
                continue;
            }

            if (!adjacency.TryGetValue(connection.SourceNodeId, out List<Guid>? consumers))
            {
                consumers = [];
                adjacency.Add(connection.SourceNodeId, consumers);
            }

            consumers.Add(connection.TargetNodeId);

            ValidatePorts(connection, source, target, resolved, incoming, diagnostics);
        }

        ValidateRequiredInputs(nodes, resolved, boundInputs, diagnostics);
        DetectCycles([.. nodes.Select(node => node.InstanceId)], adjacency, diagnostics);
    }

    private static void ValidateRequiredInputs(
        IReadOnlyList<NodeInstance> nodes,
        IReadOnlyDictionary<Guid, NodeDefinition> resolved,
        IReadOnlySet<(Guid NodeId, string PortId)> boundInputs,
        List<NodeDiagnostic> diagnostics)
    {
        foreach (NodeInstance node in nodes)
        {
            // A disabled node is not executed, so its inputs are not required.
            if (!node.IsEnabled || !resolved.TryGetValue(node.InstanceId, out NodeDefinition? definition))
            {
                continue;
            }

            foreach (PortDefinition port in definition.Inputs)
            {
                if (port.IsOptional || boundInputs.Contains((node.InstanceId, port.Id)))
                {
                    continue;
                }

                diagnostics.Add(new NodeDiagnostic(
                    DiagnosticCodes.InvalidGraph,
                    DiagnosticSeverity.Error,
                    $"Input port '{port.Id}' of node instance '{node.InstanceId}' is required, but nothing is connected to it.",
                    node.InstanceId));
            }
        }
    }

    private void ValidatePorts(
        WorkflowConnection connection,
        NodeInstance source,
        NodeInstance target,
        IReadOnlyDictionary<Guid, NodeDefinition> resolved,
        Dictionary<(Guid NodeId, string PortId), int> incoming,
        List<NodeDiagnostic> diagnostics)
    {
        if (!resolved.TryGetValue(source.InstanceId, out NodeDefinition? sourceDefinition)
            || !resolved.TryGetValue(target.InstanceId, out NodeDefinition? targetDefinition))
        {
            // The node already reported a missing or unmigratable definition;
            // judging its ports on top of that would only add noise.
            return;
        }

        PortDefinition? sourcePort = sourceDefinition.FindPort(connection.SourcePortId);
        PortDefinition? targetPort = targetDefinition.FindPort(connection.TargetPortId);

        if (sourcePort is null)
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.UnknownPort,
                DiagnosticSeverity.Error,
                $"Node instance '{source.InstanceId}' has no output port '{connection.SourcePortId}' in version {source.TypeVersion}.",
                source.InstanceId));
        }

        if (targetPort is null)
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.UnknownPort,
                DiagnosticSeverity.Error,
                $"Node instance '{target.InstanceId}' has no input port '{connection.TargetPortId}' in version {target.TypeVersion}.",
                target.InstanceId));
        }

        if (sourcePort is not null && sourcePort.Direction != PortDirection.Output)
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.IncompatiblePort,
                DiagnosticSeverity.Error,
                $"Node instance '{source.InstanceId}' port '{sourcePort.Id}' is an input port and cannot be a connection source.",
                source.InstanceId));
        }

        if (targetPort is not null && targetPort.Direction != PortDirection.Input)
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.IncompatiblePort,
                DiagnosticSeverity.Error,
                $"Node instance '{target.InstanceId}' port '{targetPort.Id}' is an output port and cannot be a connection target.",
                target.InstanceId));
        }

        if (sourcePort is not null && targetPort is not null
            && !_compatibility.IsAccepted(sourcePort.TypeId, targetPort.TypeId))
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.IncompatiblePort,
                DiagnosticSeverity.Error,
                $"Port '{sourcePort.Id}' carries '{sourcePort.TypeId}' but port '{targetPort.Id}' of the same connection accepts '{targetPort.TypeId}'.",
                target.InstanceId));
        }

        if (targetPort is null)
        {
            return;
        }

        var key = (target.InstanceId, targetPort.Id);
        int count = incoming.GetValueOrDefault(key) + 1;
        incoming[key] = count;

        if (count > 1 && targetPort.Multiplicity == PortMultiplicity.Single)
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.IncompatiblePort,
                DiagnosticSeverity.Error,
                $"Input port '{targetPort.Id}' of node instance '{target.InstanceId}' accepts a single connection, but {count} are connected to it.",
                target.InstanceId));
        }
    }

    private static void DetectCycles(
        IReadOnlyList<Guid> nodeOrder,
        IReadOnlyDictionary<Guid, List<Guid>> adjacency,
        List<NodeDiagnostic> diagnostics)
    {
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
                            diagnostics.Add(new NodeDiagnostic(
                                DiagnosticCodes.InvalidGraph,
                                DiagnosticSeverity.Error,
                                $"The connection from node instance '{node}' to node instance '{consumer}' closes a cycle: {DescribeCycle(path, consumer)}.",
                                node));
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
    }

    private static IReadOnlyList<Guid> ConsumersOf(IReadOnlyDictionary<Guid, List<Guid>> adjacency, Guid node)
        => adjacency.TryGetValue(node, out List<Guid>? consumers) ? consumers : [];

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
