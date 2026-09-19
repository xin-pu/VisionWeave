using VisionWeave.Contracts.Nodes;

namespace VisionWeave.Application.Definitions;

/// <summary>
/// The node definitions available to validation and execution, keyed by type and
/// by definition version. Built-in definitions and plugin definitions enter
/// through the same constructor, so a plugin cannot bypass the duplicate check.
/// </summary>
public sealed class NodeDefinitionCatalog
{
    private readonly Dictionary<(NodeTypeId TypeId, int TypeVersion), NodeDefinition> _byVersion;
    private readonly Dictionary<NodeTypeId, NodeDefinition> _latest;

    /// <summary>
    /// Initializes a catalog from the definitions of every provider.
    /// </summary>
    /// <param name="definitions">The declared definitions.</param>
    /// <exception cref="ArgumentException">A type and version pair is declared twice.</exception>
    public NodeDefinitionCatalog(IEnumerable<NodeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        _byVersion = [];
        _latest = [];

        foreach (NodeDefinition definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);

            if (!_byVersion.TryAdd((definition.TypeId, definition.TypeVersion), definition))
            {
                throw new ArgumentException(
                    $"Node definition '{definition.TypeId}' version {definition.TypeVersion} is declared more than once.",
                    nameof(definitions));
            }

            RequireDistinctNames(
                definition,
                definition.Ports.Select(port => port.Id),
                "port identifier",
                nameof(definitions));

            RequireDistinctNames(
                definition,
                definition.Parameters.Select(parameter => parameter.Name),
                "parameter name",
                nameof(definitions));

            if (!_latest.TryGetValue(definition.TypeId, out NodeDefinition? current)
                || definition.TypeVersion > current.TypeVersion)
            {
                _latest[definition.TypeId] = definition;
            }
        }
    }

    /// <summary>
    /// Gets an empty catalog, which reports every node type as unknown.
    /// </summary>
    public static NodeDefinitionCatalog Empty { get; } = new([]);

    /// <summary>
    /// Gets every definition in the catalog.
    /// </summary>
    public IReadOnlyCollection<NodeDefinition> Definitions => _byVersion.Values;

    /// <summary>
    /// Gets the node types the catalog knows, regardless of version.
    /// </summary>
    public IReadOnlyCollection<NodeTypeId> KnownTypeIds => _latest.Keys;

    /// <summary>
    /// Creates a catalog from the definitions the providers contribute.
    /// </summary>
    /// <param name="providers">The definition providers.</param>
    /// <returns>The catalog.</returns>
    public static NodeDefinitionCatalog FromProviders(IEnumerable<INodeDefinitionProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        return new NodeDefinitionCatalog(providers.SelectMany(provider => provider.GetDefinitions()));
    }

    /// <summary>
    /// Resolves the definition a saved node instance refers to.
    /// </summary>
    /// <param name="typeId">The node type identifier.</param>
    /// <param name="typeVersion">The definition version recorded in the document.</param>
    /// <param name="definition">The resolved definition when it is present.</param>
    /// <returns><see langword="true"/> when this build declares that exact version.</returns>
    public bool TryResolve(NodeTypeId typeId, int typeVersion, out NodeDefinition? definition)
        => _byVersion.TryGetValue((typeId, typeVersion), out definition);

    /// <summary>
    /// Resolves the newest definition version the catalog declares for a node
    /// type, which is what the editor offers for newly placed nodes.
    /// </summary>
    /// <param name="typeId">The node type identifier.</param>
    /// <param name="definition">The newest definition when the type is known.</param>
    /// <returns><see langword="true"/> when the type is known.</returns>
    public bool TryResolveLatest(NodeTypeId typeId, out NodeDefinition? definition)
        => _latest.TryGetValue(typeId, out definition);

    /// <summary>
    /// Rejects a definition that declares the same name twice. Port identifiers
    /// and parameter names are how connections and documents refer to a
    /// definition, so an ambiguity there would make a saved reference
    /// unresolvable.
    /// </summary>
    /// <param name="definition">The definition being added.</param>
    /// <param name="names">The names the definition declares.</param>
    /// <param name="what">The kind of name, used in the message.</param>
    /// <param name="parameterName">The constructor parameter the failure belongs to.</param>
    private static void RequireDistinctNames(
        NodeDefinition definition,
        IEnumerable<string> names,
        string what,
        string parameterName)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (string name in names)
        {
            if (!seen.Add(name))
            {
                throw new ArgumentException(
                    $"Node definition '{definition.TypeId}' version {definition.TypeVersion} declares {what} '{name}' more than once.",
                    parameterName);
            }
        }
    }
}
