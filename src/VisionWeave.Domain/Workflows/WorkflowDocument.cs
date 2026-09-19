using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Workflows;

namespace VisionWeave.Domain.Workflows;

/// <summary>
/// The editable, persisted form of a workflow. A document is always loadable,
/// renderable, and savable, even when it is not executable: connection legality
/// is decided by the application validator, not here. This type enforces only the
/// coherence of its own collections and the revision accounting that execution
/// invalidation depends on.
/// </summary>
public sealed class WorkflowDocument
{
    private readonly Dictionary<Guid, NodeInstance> _nodes = [];
    private readonly List<WorkflowConnection> _connections = [];
    private readonly List<ResourceReference> _resources = [];
    private readonly Dictionary<string, string> _extensionData = new(StringComparer.Ordinal);
    private readonly HashSet<string> _requiredPlugins = new(StringComparer.Ordinal);
    private bool _isHydrating;

    private WorkflowDocument(Guid id, string name, long revision, DateTimeOffset createdUtc, TimeProvider timeProvider)
    {
        Id = id;
        Name = name;
        Revision = revision;
        CreatedUtc = createdUtc;
        ModifiedUtc = createdUtc;
        TimeProvider = timeProvider;
    }

    /// <summary>
    /// Gets the document identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the workflow name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the revision that execution invalidation is keyed on. Semantic edits
    /// increment it; layout-only edits do not.
    /// </summary>
    public long Revision { get; private set; }

    /// <summary>
    /// Gets how many changes this document has received since it was created or
    /// hydrated. Unlike <see cref="Revision"/>, layout-only changes count too,
    /// because a moved node is saved state: a caller comparing this value with the
    /// one recorded at the last save knows whether the document has unsaved
    /// changes, which the revision alone cannot tell.
    /// </summary>
    public long ChangeCount { get; private set; }

    /// <summary>
    /// Gets the instant the document was created.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; }

    /// <summary>
    /// Gets the instant the document was last changed.
    /// </summary>
    public DateTimeOffset ModifiedUtc { get; private set; }

    /// <summary>
    /// Gets the placed node instances.
    /// </summary>
    public IReadOnlyCollection<NodeInstance> Nodes => _nodes.Values;

    /// <summary>
    /// Gets the saved connections.
    /// </summary>
    public IReadOnlyList<WorkflowConnection> Connections => _connections;

    /// <summary>
    /// Gets the plugin identifiers the document depends on.
    /// </summary>
    public IReadOnlyCollection<string> RequiredPlugins => _requiredPlugins;

    /// <summary>
    /// Gets the resources the document declares that a node depends on, in the
    /// order the document records them. A resource is a reference, never a machine
    /// fingerprint, so the same workflow stays portable between machines.
    /// </summary>
    public IReadOnlyList<ResourceReference> Resources => _resources;

    /// <summary>
    /// Gets unknown fields preserved from a loaded document, keyed by field name.
    /// </summary>
    public IReadOnlyDictionary<string, string> ExtensionData => _extensionData;

    /// <summary>
    /// Gets the application version that last wrote the document, when known.
    /// </summary>
    public string? AppVersion { get; private set; }

    private TimeProvider TimeProvider { get; }

    /// <summary>
    /// Creates an empty document.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <param name="timeProvider">The clock used for timestamps.</param>
    /// <returns>The new document.</returns>
    public static WorkflowDocument Create(string name, TimeProvider? timeProvider = null)
    {
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        return new WorkflowDocument(Guid.NewGuid(), RequireName(name), 0, clock.GetUtcNow(), clock);
    }

    /// <summary>
    /// Rehydrates a document that was loaded from storage. Loading is not an
    /// edit: the mutations <paramref name="fill"/> performs rebuild the document
    /// instead of revising it, and the stored revision and modification instant
    /// are restored unchanged so that cached results keyed on the revision stay
    /// reachable and a load never rewrites history.
    /// </summary>
    /// <param name="id">The stored document identifier.</param>
    /// <param name="name">The stored workflow name.</param>
    /// <param name="createdUtc">The stored creation instant.</param>
    /// <param name="modifiedUtc">The stored modification instant.</param>
    /// <param name="revision">The stored revision.</param>
    /// <param name="fill">Rebuilds the nodes, connections, and preserved fields.</param>
    /// <param name="timeProvider">The clock used for timestamps.</param>
    /// <returns>The restored document.</returns>
    public static WorkflowDocument Hydrate(
        Guid id,
        string name,
        DateTimeOffset createdUtc,
        DateTimeOffset modifiedUtc,
        long revision,
        Action<WorkflowDocument> fill,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(fill);
        ArgumentOutOfRangeException.ThrowIfNegative(revision);

        var document = new WorkflowDocument(
            id,
            RequireName(name),
            revision,
            createdUtc,
            timeProvider ?? TimeProvider.System)
        {
            ModifiedUtc = modifiedUtc,
        };

        document._isHydrating = true;
        try
        {
            fill(document);
        }
        finally
        {
            document._isHydrating = false;
        }

        return document;
    }

    /// <summary>
    /// Adds a node instance to the document.
    /// </summary>
    /// <param name="nodeTypeId">The node type to place.</param>
    /// <param name="typeVersion">The definition version the instance is saved with.</param>
    /// <param name="position">The canvas position.</param>
    /// <param name="instanceId">An explicit instance identifier, or a new one.</param>
    /// <returns>The added instance.</returns>
    public NodeInstance AddNode(
        NodeTypeId nodeTypeId,
        int typeVersion,
        CanvasPosition position,
        Guid? instanceId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(typeVersion);

        Guid id = instanceId ?? Guid.NewGuid();
        if (_nodes.ContainsKey(id))
        {
            throw new InvalidOperationException($"Node instance '{id}' already exists in the document.");
        }

        var node = new NodeInstance(id, nodeTypeId, typeVersion, position);
        _nodes.Add(id, node);
        Commit(semantic: true);
        return node;
    }

    /// <summary>
    /// Removes a node instance and every connection attached to it.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <returns>The removed instance, which a caller may keep as the data needed
    /// to restore it.</returns>
    public NodeInstance RemoveNode(Guid instanceId)
    {
        if (!_nodes.Remove(instanceId, out NodeInstance? removed))
        {
            throw new KeyNotFoundException($"Node instance '{instanceId}' is not present in the document.");
        }

        _connections.RemoveAll(connection =>
            connection.SourceNodeId == instanceId || connection.TargetNodeId == instanceId);

        Commit(semantic: true);
        return removed;
    }

    /// <summary>
    /// Restores a node instance that was removed from this document, together with
    /// the state the instance carries: position, label, enabled flag, saved
    /// parameter values, and preserved fields. Connections are restored separately
    /// because the removal also dropped them. The instance keeps its identifier, so
    /// a caller can restore connections that refer to it.
    /// </summary>
    /// <param name="node">A removed instance of this document.</param>
    public void RestoreNode(NodeInstance node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (_nodes.ContainsKey(node.InstanceId))
        {
            throw new InvalidOperationException($"Node instance '{node.InstanceId}' already exists in the document.");
        }

        _nodes.Add(node.InstanceId, node);
        Commit(semantic: true);
    }

    /// <summary>
    /// Moves a node on the canvas. Layout is document state but it does not
    /// invalidate execution.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="position">The new canvas position.</param>
    public void MoveNode(Guid instanceId, CanvasPosition position)
    {
        GetNode(instanceId).SetPosition(position);
        Commit(semantic: false);
    }

    /// <summary>
    /// Sets or clears a display label. Labels do not affect execution.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="label">The label, or <see langword="null"/> to clear it.</param>
    public void SetNodeLabel(Guid instanceId, string? label)
    {
        GetNode(instanceId).SetLabel(label);
        Commit(semantic: false);
    }

    /// <summary>
    /// Enables or disables a node instance.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="isEnabled">Whether the node takes part in a run.</param>
    public void SetNodeEnabled(Guid instanceId, bool isEnabled)
    {
        GetNode(instanceId).SetEnabled(isEnabled);
        Commit(semantic: true);
    }

    /// <summary>
    /// Sets a validated parameter value on a node instance.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    public void SetNodeParameter(Guid instanceId, string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        GetNode(instanceId).SetParameter(name, value);
        Commit(semantic: true);
    }

    /// <summary>
    /// Removes a saved parameter value, so the node falls back to the definition's
    /// declared default. Removing an unset parameter is not an error: the caller is
    /// stating the intended value, not the preceding state.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="name">The parameter name.</param>
    public void ClearNodeParameter(Guid instanceId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        GetNode(instanceId).ClearParameter(name);
        Commit(semantic: true);
    }

    /// <summary>
    /// Records the definition version a migrated node instance now conforms to.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="typeVersion">The new definition version.</param>
    public void SetNodeTypeVersion(Guid instanceId, int typeVersion)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(typeVersion);
        GetNode(instanceId).SetTypeVersion(typeVersion);
        Commit(semantic: true);
    }

    /// <summary>
    /// Renames the workflow.
    /// </summary>
    /// <param name="name">The new name.</param>
    public void Rename(string name)
    {
        Name = RequireName(name);
        Commit(semantic: true);
    }

    /// <summary>
    /// Adds a connection between two existing node instances. Whether the pairing
    /// is legal is decided by the application validator.
    /// </summary>
    /// <param name="sourceNodeId">The producing node instance.</param>
    /// <param name="sourcePortId">The output port identifier.</param>
    /// <param name="targetNodeId">The consuming node instance.</param>
    /// <param name="targetPortId">The input port identifier.</param>
    /// <param name="connectionId">An explicit connection identifier, or a new one.</param>
    /// <returns>The added connection.</returns>
    public WorkflowConnection AddConnection(
        Guid sourceNodeId,
        string sourcePortId,
        Guid targetNodeId,
        string targetPortId,
        Guid? connectionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePortId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPortId);
        RequireNode(sourceNodeId);
        RequireNode(targetNodeId);

        Guid id = connectionId ?? Guid.NewGuid();
        if (_connections.Any(connection => connection.ConnectionId == id))
        {
            throw new InvalidOperationException($"Connection '{id}' already exists in the document.");
        }

        var connection = new WorkflowConnection(id, sourceNodeId, sourcePortId, targetNodeId, targetPortId);
        _connections.Add(connection);
        Commit(semantic: true);
        return connection;
    }

    /// <summary>
    /// Removes a connection.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    public void RemoveConnection(Guid connectionId)
    {
        int removed = _connections.RemoveAll(connection => connection.ConnectionId == connectionId);
        if (removed == 0)
        {
            throw new KeyNotFoundException($"Connection '{connectionId}' is not present in the document.");
        }

        Commit(semantic: true);
    }

    /// <summary>
    /// Registers a plugin the document depends on.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    public void RequirePlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        _requiredPlugins.Add(pluginId);
    }

    /// <summary>
    /// Declares a resource the document depends on. Unlike a remembered port
    /// schema, a resource is document state the user controls: adding one is an
    /// edit, so it marks the document as changed and moves the revision.
    /// </summary>
    /// <param name="resource">The reference to add.</param>
    public void AddResource(ResourceReference resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _resources.Add(resource);
        Commit(semantic: true);
    }

    /// <summary>
    /// Preserves an unknown field read from a saved document.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="json">The raw JSON fragment.</param>
    public void PreserveExtension(string name, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(json);
        _extensionData[name] = json;
        Commit(semantic: false);
    }

    /// <summary>
    /// Preserves an unknown field read from a saved node entry, so that a later
    /// save never drops node content this build does not understand.
    /// </summary>
    /// <param name="instanceId">The instance the field was stored on.</param>
    /// <param name="name">The field name.</param>
    /// <param name="json">The raw JSON fragment.</param>
    public void PreserveNodeExtension(Guid instanceId, string name, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(json);
        GetNode(instanceId).SetExtensionData(name, json);
        Commit(semantic: false);
    }

    /// <summary>
    /// Records the ports a node was saved with, which a later session renders the
    /// node from when its definition is unavailable. This is remembered rendering
    /// data rather than an edit, so it does not move the revision.
    /// </summary>
    /// <param name="instanceId">The instance the snapshot belongs to.</param>
    /// <param name="snapshot">The remembered ports, in the order the document recorded them.</param>
    /// <exception cref="KeyNotFoundException">The instance is not present.</exception>
    public void SetNodePortSchemaSnapshot(Guid instanceId, IReadOnlyList<PortSchemaEntry> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        GetNode(instanceId).SetPortSchemaSnapshot(snapshot);
        Commit(semantic: false);
    }

    /// <summary>
    /// Records the application version that wrote the document. This is file
    /// metadata and does not change the workflow revision.
    /// </summary>
    /// <param name="appVersion">The application version, or <see langword="null"/>.</param>
    public void RecordAppVersion(string? appVersion)
    {
        AppVersion = appVersion;
    }

    /// <summary>
    /// Gets a node instance.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <returns>The node instance.</returns>
    /// <exception cref="KeyNotFoundException">The instance is not present.</exception>
    public NodeInstance GetNode(Guid instanceId)
        => _nodes.TryGetValue(instanceId, out NodeInstance? node)
            ? node
            : throw new KeyNotFoundException($"Node instance '{instanceId}' is not present in the document.");

    /// <summary>
    /// Tries to get a node instance.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="node">The node instance when it is present.</param>
    /// <returns><see langword="true"/> when the instance is present.</returns>
    public bool TryGetNode(Guid instanceId, out NodeInstance? node) => _nodes.TryGetValue(instanceId, out node);

    /// <summary>
    /// Creates a deep copy that shares no mutable state with this document, which
    /// copy/paste and any off-document comparison rely on.
    /// </summary>
    /// <returns>The copy.</returns>
    public WorkflowDocument Clone()
    {
        var clone = new WorkflowDocument(Id, Name, Revision, CreatedUtc, TimeProvider)
        {
            ModifiedUtc = ModifiedUtc,
            AppVersion = AppVersion,
            ChangeCount = ChangeCount,
        };

        foreach (NodeInstance node in _nodes.Values)
        {
            clone._nodes.Add(node.InstanceId, node.Copy());
        }

        clone._connections.AddRange(_connections);
        clone._resources.AddRange(_resources);

        foreach (KeyValuePair<string, string> extension in _extensionData)
        {
            clone._extensionData[extension.Key] = extension.Value;
        }

        foreach (string plugin in _requiredPlugins)
        {
            clone._requiredPlugins.Add(plugin);
        }

        return clone;
    }

    private static string RequireName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name;
    }

    private void RequireNode(Guid instanceId)
    {
        if (!_nodes.ContainsKey(instanceId))
        {
            throw new KeyNotFoundException($"Node instance '{instanceId}' is not present in the document.");
        }
    }

    private void Commit(bool semantic)
    {
        if (_isHydrating)
        {
            return;
        }

        if (semantic)
        {
            Revision++;
        }

        ChangeCount++;
        ModifiedUtc = TimeProvider.GetUtcNow();
    }
}
