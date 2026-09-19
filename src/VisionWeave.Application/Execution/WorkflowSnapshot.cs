namespace VisionWeave.Application.Execution;

/// <summary>
/// The immutable, validated form of a workflow: resolved definitions, parameter
/// values, and confirmed connections, together with the document revision they
/// were captured from. A snapshot is the only input the runtime executes, so a
/// document can keep changing while a run is in flight.
/// </summary>
public sealed record WorkflowSnapshot
{
    private readonly Dictionary<Guid, SnapshotNode> _index;

    /// <summary>
    /// Initializes a snapshot.
    /// </summary>
    /// <param name="documentId">The document the snapshot was captured from.</param>
    /// <param name="revision">The revision the snapshot was captured at.</param>
    /// <param name="nodes">The resolved node instances.</param>
    /// <param name="edges">The validated connections, in document order.</param>
    public WorkflowSnapshot(
        Guid documentId,
        long revision,
        IEnumerable<SnapshotNode> nodes,
        IEnumerable<SnapshotEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        DocumentId = documentId;
        Revision = revision;
        Nodes = [.. nodes];
        Edges = [.. edges];
        _index = Nodes.ToDictionary(node => node.InstanceId);
    }

    /// <summary>
    /// Gets the document identifier the snapshot was captured from.
    /// </summary>
    public Guid DocumentId { get; }

    /// <summary>
    /// Gets the document revision the snapshot was captured at, which is how the
    /// runtime decides whether a finished run may still publish its results.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets the resolved node instances.
    /// </summary>
    public IReadOnlyList<SnapshotNode> Nodes { get; }

    /// <summary>
    /// Gets the validated connections in document order.
    /// </summary>
    public IReadOnlyList<SnapshotEdge> Edges { get; }

    /// <summary>
    /// Tries to get a snapshot node.
    /// </summary>
    /// <param name="instanceId">The node instance identifier.</param>
    /// <param name="node">The resolved node when it is present.</param>
    /// <returns><see langword="true"/> when the node is present.</returns>
    public bool TryGetNode(Guid instanceId, out SnapshotNode? node)
        => _index.TryGetValue(instanceId, out node);

    /// <summary>
    /// Gets a snapshot node.
    /// </summary>
    /// <param name="instanceId">The node instance identifier.</param>
    /// <returns>The resolved node.</returns>
    /// <exception cref="KeyNotFoundException">The node is not part of the snapshot.</exception>
    public SnapshotNode GetNode(Guid instanceId)
        => _index.TryGetValue(instanceId, out SnapshotNode? node)
            ? node
            : throw new KeyNotFoundException($"Node instance '{instanceId}' is not part of the snapshot.");
}
