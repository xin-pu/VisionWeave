using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Workflows;

namespace VisionWeave.Domain.Workflows;

/// <summary>
/// One placed node in a workflow document. An instance holds user-controlled
/// state only; the node type's ports, parameters, and executor come from
/// <see cref="NodeDefinition"/>. Mutation is exposed through
/// <see cref="WorkflowDocument"/> so that revision accounting stays in one place.
/// </summary>
public sealed class NodeInstance
{
    private readonly Dictionary<string, object?> _parameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _extensionData = new(StringComparer.Ordinal);
    private PortSchemaEntry[] _portSchemaSnapshot = [];

    internal NodeInstance(Guid instanceId, NodeTypeId nodeTypeId, int typeVersion, CanvasPosition position)
    {
        InstanceId = instanceId;
        NodeTypeId = nodeTypeId;
        TypeVersion = typeVersion;
        Position = position;
    }

    /// <summary>
    /// Gets the instance identifier, which is unique inside one document.
    /// </summary>
    public Guid InstanceId { get; }

    /// <summary>
    /// Gets the node type this instance places.
    /// </summary>
    public NodeTypeId NodeTypeId { get; }

    /// <summary>
    /// Gets the definition version the instance was saved with.
    /// </summary>
    public int TypeVersion { get; private set; }

    /// <summary>
    /// Gets the canvas position.
    /// </summary>
    public CanvasPosition Position { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the instance takes part in a run.
    /// </summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// Gets the optional display label override.
    /// </summary>
    public string? Label { get; private set; }

    /// <summary>
    /// Gets the saved parameter values.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters => _parameters;

    /// <summary>
    /// Gets unknown JSON fragments preserved from the document, keyed by field
    /// name, so that a save never drops content this build does not understand.
    /// </summary>
    public IReadOnlyDictionary<string, string> ExtensionData => _extensionData;

    /// <summary>
    /// Gets the ports the node was saved with, which a session renders the node
    /// from when its definition is unavailable. The snapshot is a rendering
    /// fallback and never a second source of truth: for a known node type the
    /// resolved definition wins.
    /// </summary>
    public IReadOnlyList<PortSchemaEntry> PortSchemaSnapshot => _portSchemaSnapshot;

    internal void SetParameter(string name, object? value) => _parameters[name] = value;

    internal void ClearParameter(string name) => _parameters.Remove(name);

    internal void SetTypeVersion(int typeVersion) => TypeVersion = typeVersion;

    internal void SetPosition(CanvasPosition position) => Position = position;

    internal void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    internal void SetLabel(string? label) => Label = label;

    internal void SetExtensionData(string name, string json) => _extensionData[name] = json;

    internal void SetPortSchemaSnapshot(IReadOnlyList<PortSchemaEntry> snapshot)
        => _portSchemaSnapshot = [.. snapshot];

    internal NodeInstance Copy()
    {
        var copy = new NodeInstance(InstanceId, NodeTypeId, TypeVersion, Position)
        {
            IsEnabled = IsEnabled,
            Label = Label,
        };

        foreach (KeyValuePair<string, object?> parameter in _parameters)
        {
            copy._parameters[parameter.Key] = parameter.Value;
        }

        foreach (KeyValuePair<string, string> extension in _extensionData)
        {
            copy._extensionData[extension.Key] = extension.Value;
        }

        copy._portSchemaSnapshot = [.. _portSchemaSnapshot];

        return copy;
    }
}
