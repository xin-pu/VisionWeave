using System.Globalization;
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

            if (resolved.TryGetValue(node.InstanceId, out NodeDefinition? definition))
            {
                ValidateParameters(node, definition, diagnostics);
            }
        }

        ValidateConnections(document, nodes, resolved, diagnostics);

        return new ValidationResult(diagnostics);
    }

    /// <summary>
    /// Validates a document and tags the outcome with the revision it was
    /// computed from, so the editor can tell a current projection from one an
    /// edit has superseded.
    /// </summary>
    /// <param name="document">The document to validate.</param>
    /// <returns>The projection for the document's current revision.</returns>
    public ValidationProjection Project(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new ValidationProjection(Validate(document), document.Revision);
    }

    /// <summary>
    /// Decides whether one candidate connection could be added to a document,
    /// reporting only the diagnostics that the connection itself would introduce.
    /// The document is not changed and the diagnostics it already earns are not
    /// repeated, so an editor can judge a pending wire while the rest of the graph
    /// is still incomplete. This is the one place the connection rules are decided;
    /// no caller re-implements direction, type, multiplicity, or cycle checking.
    /// </summary>
    /// <param name="document">The document the connection would be added to.</param>
    /// <param name="connection">The candidate connection.</param>
    /// <returns>The diagnostics that adding the connection would introduce.</returns>
    public ValidationResult ValidateConnection(WorkflowDocument document, WorkflowConnection connection)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(connection);

        List<NodeDiagnostic> diagnostics = [];

        if (!document.TryGetNode(connection.SourceNodeId, out NodeInstance? source)
            || !document.TryGetNode(connection.TargetNodeId, out NodeInstance? target))
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.InvalidGraph,
                DiagnosticSeverity.Error,
                $"Connection '{connection.ConnectionId}' refers to a node instance that is not in the document.",
                null,
                null,
                new ConnectionTarget(connection.ConnectionId)));
            return new ValidationResult(diagnostics);
        }

        if (connection.SourceNodeId == connection.TargetNodeId)
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.InvalidGraph,
                DiagnosticSeverity.Error,
                $"Node instance '{connection.SourceNodeId}' is connected to itself.",
                connection.SourceNodeId,
                null,
                new ConnectionTarget(connection.ConnectionId)));
            return new ValidationResult(diagnostics);
        }

        // A definition this build cannot resolve already reported VW-NODE-001 as
        // part of whole-document validation, so its ports are not judged here.
        Dictionary<Guid, NodeDefinition> resolved = [];
        foreach (NodeInstance node in document.Nodes)
        {
            if (_catalog.TryResolve(node.NodeTypeId, node.TypeVersion, out NodeDefinition? definition))
            {
                resolved[node.InstanceId] = definition!;
            }
        }

        Dictionary<(Guid NodeId, string PortId), int> incoming = [];
        foreach (WorkflowConnection existing in document.Connections)
        {
            if (existing.TargetNodeId == connection.TargetNodeId
                && string.Equals(existing.TargetPortId, connection.TargetPortId, StringComparison.Ordinal))
            {
                var key = (existing.TargetNodeId, existing.TargetPortId);
                incoming[key] = incoming.GetValueOrDefault(key) + 1;
            }
        }

        ValidatePorts(connection, source!, target!, resolved, incoming, diagnostics);

        if (TryDescribeCycle(document.Connections, connection, out string cycle))
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.InvalidGraph,
                DiagnosticSeverity.Error,
                $"The connection from node instance '{connection.SourceNodeId}' to node instance '{connection.TargetNodeId}' closes a cycle: {cycle}.",
                connection.SourceNodeId,
                null,
                new ConnectionTarget(connection.ConnectionId)));
        }

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

    /// <summary>
    /// Validates the parameter values a document saved against the definition
    /// that now resolves. A value the executor could not read is refused here
    /// rather than surfacing later as an execution failure, and a required
    /// parameter the document leaves unset is refused before the run starts.
    /// </summary>
    private static void ValidateParameters(
        NodeInstance node,
        NodeDefinition definition,
        List<NodeDiagnostic> diagnostics)
    {
        foreach (KeyValuePair<string, object?> saved in node.Parameters.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            ParameterDefinition? declared = definition.Parameters
                .FirstOrDefault(parameter => string.Equals(parameter.Name, saved.Key, StringComparison.Ordinal));

            if (declared is null)
            {
                diagnostics.Add(new NodeDiagnostic(
                    DiagnosticCodes.UnknownParameter,
                    DiagnosticSeverity.Error,
                    $"Node instance '{node.InstanceId}' saves parameter '{saved.Key}', which node type '{definition.TypeId}' version {definition.TypeVersion} does not declare.",
                    node.InstanceId,
                    null,
                    new ParameterTarget(node.InstanceId, saved.Key)));
                continue;
            }

            // An explicit null is how a document clears a value, so it counts as
            // unset rather than as a value of the wrong shape.
            if (saved.Value is not null)
            {
                ValidateParameterValue(node, declared, saved.Value, diagnostics);
            }
        }

        if (!node.IsEnabled)
        {
            // A disabled node is not executed, so a missing value cannot block a run.
            return;
        }

        foreach (ParameterDefinition declared in definition.Parameters)
        {
            bool supplied = node.Parameters.TryGetValue(declared.Name, out object? saved) && saved is not null;

            if (declared.IsRequired && !supplied && declared.DefaultValue is null)
            {
                diagnostics.Add(new NodeDiagnostic(
                    DiagnosticCodes.MissingRequiredParameter,
                    DiagnosticSeverity.Error,
                    $"Parameter '{declared.Name}' of node instance '{node.InstanceId}' is required, but the document supplies no value and the definition declares no default.",
                    node.InstanceId,
                    null,
                    new ParameterTarget(node.InstanceId, declared.Name)));
            }
        }
    }

    private static void ValidateParameterValue(
        NodeInstance node,
        ParameterDefinition declared,
        object value,
        List<NodeDiagnostic> diagnostics)
    {
        switch (declared.Kind)
        {
            case ParameterKind.Boolean:
                if (value is not bool)
                {
                    RejectKind(node, declared, "a boolean", diagnostics);
                }

                break;
            case ParameterKind.Text:
            case ParameterKind.Path:
                if (value is not string)
                {
                    RejectKind(node, declared, "text", diagnostics);
                }

                break;
            case ParameterKind.Option:
                if (value is not string option)
                {
                    RejectKind(node, declared, "one of its declared options", diagnostics);
                }
                else if (declared.Options is { Count: > 0 } options
                    && !options.Contains(option, StringComparer.Ordinal))
                {
                    diagnostics.Add(new NodeDiagnostic(
                        DiagnosticCodes.ParameterOptionNotDeclared,
                        DiagnosticSeverity.Error,
                        $"Parameter '{declared.Name}' of node instance '{node.InstanceId}' is '{option}', which is not one of the declared options: {string.Join(", ", options)}.",
                        node.InstanceId,
                        null,
                        new ParameterTarget(node.InstanceId, declared.Name)));
                }

                break;
            case ParameterKind.Integer:
                if (!TryReadNumber(value, out double integer))
                {
                    RejectKind(node, declared, "a whole number", diagnostics);
                }
                else if (!double.IsFinite(integer) || integer != Math.Floor(integer))
                {
                    RejectKind(node, declared, "a whole number", diagnostics);
                }
                else
                {
                    ValidateBounds(node, declared, integer, diagnostics);
                }

                break;
            case ParameterKind.Number:
                if (!TryReadNumber(value, out double number) || !double.IsFinite(number))
                {
                    RejectKind(node, declared, "a finite number", diagnostics);
                }
                else
                {
                    ValidateBounds(node, declared, number, diagnostics);
                }

                break;
            default:
                break;
        }
    }

    private static void ValidateBounds(
        NodeInstance node,
        ParameterDefinition declared,
        double value,
        List<NodeDiagnostic> diagnostics)
    {
        bool belowMinimum = declared.Minimum is { } minimum && value < minimum;
        bool aboveMaximum = declared.Maximum is { } maximum && value > maximum;

        if (!belowMinimum && !aboveMaximum)
        {
            return;
        }

        diagnostics.Add(new NodeDiagnostic(
            DiagnosticCodes.ParameterOutOfRange,
            DiagnosticSeverity.Error,
            $"Parameter '{declared.Name}' of node instance '{node.InstanceId}' is {Format(value)}, which is outside the declared range {DescribeRange(declared)}.",
            node.InstanceId,
            null,
            new ParameterTarget(node.InstanceId, declared.Name)));
    }

    private static void RejectKind(
        NodeInstance node,
        ParameterDefinition declared,
        string expected,
        List<NodeDiagnostic> diagnostics)
        => diagnostics.Add(new NodeDiagnostic(
            DiagnosticCodes.InvalidParameterValue,
            DiagnosticSeverity.Error,
            $"Parameter '{declared.Name}' of node instance '{node.InstanceId}' accepts only {expected}.",
            node.InstanceId,
            null,
            new ParameterTarget(node.InstanceId, declared.Name)));

    private static bool TryReadNumber(object value, out double number)
    {
        if (value is double or float or decimal or long or int or short or sbyte or byte or ushort or uint or ulong)
        {
            number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return true;
        }

        number = 0;
        return false;
    }

    private static string DescribeRange(ParameterDefinition declared)
        => (declared.Minimum, declared.Maximum) switch
        {
            ({ } minimum, { } maximum) => $"[{Format(minimum)}, {Format(maximum)}]",
            ({ } minimum, null) => $"at least {Format(minimum)}",
            (null, { } maximum) => $"at most {Format(maximum)}",
            _ => "the declared range",
        };

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

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
                    null,
                    null,
                    new ConnectionTarget(connection.ConnectionId)));
                continue;
            }

            boundInputs.Add((connection.TargetNodeId, connection.TargetPortId));

            if (connection.SourceNodeId == connection.TargetNodeId)
            {
                diagnostics.Add(new NodeDiagnostic(
                    DiagnosticCodes.InvalidGraph,
                    DiagnosticSeverity.Error,
                    $"Node instance '{connection.SourceNodeId}' is connected to itself.",
                    connection.SourceNodeId,
                    null,
                    new ConnectionTarget(connection.ConnectionId)));
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
                    node.InstanceId,
                    null,
                    new PortTarget(node.InstanceId, port.Id)));
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
                source.InstanceId,
                null,
                new PortTarget(source.InstanceId, connection.SourcePortId)));
        }

        if (targetPort is null)
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.UnknownPort,
                DiagnosticSeverity.Error,
                $"Node instance '{target.InstanceId}' has no input port '{connection.TargetPortId}' in version {target.TypeVersion}.",
                target.InstanceId,
                null,
                new PortTarget(target.InstanceId, connection.TargetPortId)));
        }

        if (sourcePort is not null && sourcePort.Direction != PortDirection.Output)
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.IncompatiblePort,
                DiagnosticSeverity.Error,
                $"Node instance '{source.InstanceId}' port '{sourcePort.Id}' is an input port and cannot be a connection source.",
                source.InstanceId,
                null,
                new PortTarget(source.InstanceId, sourcePort.Id)));
        }

        if (targetPort is not null && targetPort.Direction != PortDirection.Input)
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.IncompatiblePort,
                DiagnosticSeverity.Error,
                $"Node instance '{target.InstanceId}' port '{targetPort.Id}' is an output port and cannot be a connection target.",
                target.InstanceId,
                null,
                new PortTarget(target.InstanceId, targetPort.Id)));
        }

        if (sourcePort is not null && targetPort is not null
            && !_compatibility.IsAccepted(sourcePort.TypeId, targetPort.TypeId))
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.IncompatiblePort,
                DiagnosticSeverity.Error,
                $"Port '{sourcePort.Id}' carries '{sourcePort.TypeId}' but port '{targetPort.Id}' of the same connection accepts '{targetPort.TypeId}'.",
                target.InstanceId,
                null,
                new ConnectionTarget(connection.ConnectionId)));
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
                target.InstanceId,
                null,
                new PortTarget(target.InstanceId, targetPort.Id)));
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

    /// <summary>
    /// Determines whether adding one connection to a graph would close a cycle, and
    /// describes the cycle when it would. The candidate closes a cycle exactly when
    /// its target already reaches its source, which is what the search looks for.
    /// </summary>
    /// <param name="connections">The connections already in the graph.</param>
    /// <param name="candidate">The connection being considered.</param>
    /// <param name="cycle">The cycle the candidate would close.</param>
    /// <returns><see langword="true"/> when the candidate closes a cycle.</returns>
    private static bool TryDescribeCycle(
        IReadOnlyList<WorkflowConnection> connections,
        WorkflowConnection candidate,
        out string cycle)
    {
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
