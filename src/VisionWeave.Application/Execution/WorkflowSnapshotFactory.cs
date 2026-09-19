using VisionWeave.Application.Definitions;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Execution;

/// <summary>
/// Captures an executable snapshot from a workflow document. Capture validates
/// first, so a snapshot exists only for a document that can actually run and
/// always records the revision it was taken from.
/// </summary>
public sealed class WorkflowSnapshotFactory
{
    private readonly NodeDefinitionCatalog _catalog;
    private readonly WorkflowValidator _validator;

    /// <summary>
    /// Initializes the factory.
    /// </summary>
    /// <param name="catalog">The node definitions available to this build.</param>
    /// <param name="compatibility">The declared port compatibility relation.</param>
    public WorkflowSnapshotFactory(NodeDefinitionCatalog catalog, PortCompatibility? compatibility = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
        _validator = new WorkflowValidator(catalog, compatibility);
    }

    /// <summary>
    /// Captures a snapshot of the document.
    /// </summary>
    /// <param name="document">The document to capture.</param>
    /// <returns>The snapshot, or the diagnostics that prevented it.</returns>
    public SnapshotBuildResult Build(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        ValidationResult validation = _validator.Validate(document);
        if (!validation.IsValid)
        {
            return new SnapshotBuildResult(null, validation);
        }

        var snapshot = new WorkflowSnapshot(
            document.Id,
            document.Revision,
            CaptureNodes(document),
            CaptureEdges(document));

        return new SnapshotBuildResult(snapshot, validation);
    }

    private IEnumerable<SnapshotNode> CaptureNodes(WorkflowDocument document)
    {
        foreach (NodeInstance node in document.Nodes.OrderBy(item => item.InstanceId))
        {
            if (!_catalog.TryResolve(node.NodeTypeId, node.TypeVersion, out NodeDefinition? definition))
            {
                throw new InvalidOperationException(
                    $"Node instance '{node.InstanceId}' passed validation, but its definition is not in the catalog.");
            }

            yield return new SnapshotNode(
                node.InstanceId,
                definition!,
                new NodeParameterSet(node.Parameters),
                node.IsEnabled);
        }
    }

    private static IEnumerable<SnapshotEdge> CaptureEdges(WorkflowDocument document)
        => document.Connections.Select(connection => new SnapshotEdge(
            connection.ConnectionId,
            connection.SourceNodeId,
            connection.SourcePortId,
            connection.TargetNodeId,
            connection.TargetPortId));
}
