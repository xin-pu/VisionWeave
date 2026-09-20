using VisionWeave.App.Presentation;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Execution;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;
using VisionWeave.Contracts.Workflows;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.App.Canvas;

/// <summary>
/// Turns a committed document into what the canvas draws. It reads the document,
/// the catalog, and the validation projection and writes nothing, so a projection
/// is a pure answer: the same document always draws the same way, and a gesture
/// that was refused simply leaves the previous projection in place.
/// </summary>
internal static class CanvasProjection
{
    /// <summary>
    /// Projects a document onto canvas presentations.
    /// </summary>
    /// <param name="document">The committed document.</param>
    /// <param name="catalog">The catalog each node type is resolved against.</param>
    /// <param name="validation">The validation projection that answers for this document revision.</param>
    /// <param name="selection">The selected instances, which mark nodes without changing the document.</param>
    /// <param name="run">The newest run, or <see langword="null"/> when nothing has run.</param>
    /// <returns>What the canvas draws.</returns>
    internal static CanvasProjectedDocument Project(
        WorkflowDocument document,
        NodeDefinitionCatalog catalog,
        ValidationProjection validation,
        IReadOnlyList<Guid> selection,
        WorkflowRunSummary? run)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(selection);

        IReadOnlyDictionary<Guid, NodeRunMark> marks = RunMarks(document, run);
        HashSet<Guid> selected = [.. selection];
        List<WorkflowNodeViewModel> nodes = [];
        Dictionary<(Guid Node, string Port), PortViewModel> ports = [];

        foreach (NodeInstance instance in InDrawingOrder(document))
        {
            catalog.TryResolve(instance.NodeTypeId, instance.TypeVersion, out NodeDefinition? definition);

            List<PortViewModel> inputs = [];
            List<PortViewModel> outputs = [];

            foreach ((string portId, string displayName, PortDirection direction) in DeclaredPorts(instance, definition))
            {
                var port = new PortViewModel(
                    instance.InstanceId,
                    portId,
                    displayName,
                    direction,
                    validation.SeverityOfPort(instance.InstanceId, portId),
                    Describe(validation.DiagnosticsForPort(instance.InstanceId, portId)));

                ports[(instance.InstanceId, portId)] = port;
                (direction == PortDirection.Input ? inputs : outputs).Add(port);
            }

            var node = new WorkflowNodeViewModel(
                instance.InstanceId,
                NodeText.Title(instance, definition),
                NodeText.Caption(instance, definition),
                instance.Position,
                inputs,
                outputs,
                validation.SeverityOf(instance.InstanceId),
                Describe(validation.DiagnosticsFor(instance.InstanceId)),
                marks.GetValueOrDefault(instance.InstanceId))
            {
                IsSelected = selected.Contains(instance.InstanceId),
            };

            nodes.Add(node);
        }

        List<WorkflowConnectionViewModel> connectors = [];
        foreach (WorkflowConnection connection in document.Connections)
        {
            // A wire is drawn between two connector controls, so a wire whose port
            // this build cannot name has nothing to attach to and is left out rather
            // than drawn from the origin. The node it belongs to is still shown, and
            // deleting that node removes the wire with it.
            if (!ports.TryGetValue((connection.SourceNodeId, connection.SourcePortId), out PortViewModel? source)
                || !ports.TryGetValue((connection.TargetNodeId, connection.TargetPortId), out PortViewModel? target))
            {
                continue;
            }

            connectors.Add(new WorkflowConnectionViewModel(
                connection.ConnectionId,
                source,
                target,
                validation.SeverityOfConnection(connection.ConnectionId),
                Describe(validation.DiagnosticsForConnection(connection.ConnectionId))));
        }

        return new CanvasProjectedDocument(nodes, connectors);
    }

    /// <summary>
    /// Orders the instances for drawing. The document stores them in a dictionary,
    /// so the order it enumerates is not part of its contract; ordering by position
    /// gives a stable projection and puts nodes nearer the top-left behind the ones
    /// that are laid over them.
    /// </summary>
    private static IEnumerable<NodeInstance> InDrawingOrder(WorkflowDocument document)
        => document.Nodes
            .OrderBy(node => node.Position.Y)
            .ThenBy(node => node.Position.X)
            .ThenBy(node => node.InstanceId);

    /// <summary>
    /// Names the ports a node presents: the ones its definition declares, or, when
    /// this build cannot resolve that definition, the ones the document remembered
    /// when it last knew them. A node whose ports are unknown shows no connectors
    /// rather than connectors that would accept a wire the document cannot describe.
    /// </summary>
    private static IEnumerable<(string PortId, string DisplayName, PortDirection Direction)> DeclaredPorts(
        NodeInstance instance,
        NodeDefinition? definition)
    {
        if (definition is not null)
        {
            foreach (PortDefinition port in definition.Ports)
            {
                yield return (port.Id, port.DisplayName, port.Direction);
            }

            yield break;
        }

        foreach (PortSchemaEntry entry in instance.PortSchemaSnapshot)
        {
            yield return (entry.PortId, entry.DisplayName ?? entry.PortId, entry.Direction);
        }
    }

    /// <summary>
    /// Reads the newest run as a mark for each node of the document on screen. A run
    /// describes one execution of one revision, so a summary of another document, or
    /// of a revision this one has moved past, marks nothing: the graph that ran is
    /// not the graph being drawn, and a mark left standing would report a failure an
    /// edit has since fixed. A node the run never covered — one that is switched off,
    /// or one this build cannot resolve — has no mark either, rather than being
    /// reported as a node the run skipped.
    /// </summary>
    /// <param name="document">The document being drawn.</param>
    /// <param name="run">The newest run, or <see langword="null"/>.</param>
    /// <returns>A mark per instance the run covered.</returns>
    private static IReadOnlyDictionary<Guid, NodeRunMark> RunMarks(WorkflowDocument document, WorkflowRunSummary? run)
    {
        Dictionary<Guid, NodeRunMark> marks = [];

        if (run is null || run.DocumentId != document.Id || run.Revision != document.Revision)
        {
            return marks;
        }

        foreach (NodeRunReport report in run.Nodes)
        {
            marks[report.NodeInstanceId] = new NodeRunMark(
                report.State,
                report.Duration,
                Describe(report.Diagnostics));
        }

        return marks;
    }

    /// <summary>
    /// Describes the condition a place on the canvas owns, or nothing when it owns
    /// none. The projection answers for one revision, so the worst condition is the
    /// one the validator reported for the element itself, not a mark left by an
    /// earlier edit.
    /// </summary>
    private static string Describe(IReadOnlyList<NodeDiagnostic> diagnostics)
        => diagnostics.Count == 0 ? string.Empty : DiagnosticText.Of(diagnostics[0]);
}
