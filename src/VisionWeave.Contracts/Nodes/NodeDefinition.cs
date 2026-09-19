using VisionWeave.Contracts.Ports;

namespace VisionWeave.Contracts.Nodes;

/// <summary>
/// The immutable description of a node type: its identity, version, display
/// metadata, port schema, parameter schema, and the executor it resolves to. A
/// definition never carries UI controls or native resources.
/// </summary>
/// <param name="TypeId">The provider-qualified node type identifier.</param>
/// <param name="TypeVersion">The definition version recorded in documents.</param>
/// <param name="DisplayName">The node title shown on the canvas.</param>
/// <param name="Category">The library category used for grouping and search.</param>
/// <param name="Ports">The declared input and output ports.</param>
/// <param name="Parameters">The declared user-editable parameters.</param>
/// <param name="ExecutorTypeId">The key the composition root resolves to an executor factory.</param>
public sealed record NodeDefinition(
    NodeTypeId TypeId,
    int TypeVersion,
    string DisplayName,
    string Category,
    IReadOnlyList<PortDefinition> Ports,
    IReadOnlyList<ParameterDefinition> Parameters,
    string ExecutorTypeId)
{
    /// <summary>
    /// Gets the input ports.
    /// </summary>
    public IEnumerable<PortDefinition> Inputs
        => Ports.Where(port => port.Direction == PortDirection.Input);

    /// <summary>
    /// Gets the output ports.
    /// </summary>
    public IEnumerable<PortDefinition> Outputs
        => Ports.Where(port => port.Direction == PortDirection.Output);

    /// <summary>
    /// Finds a declared port by its identifier.
    /// </summary>
    /// <param name="portId">The port identifier.</param>
    /// <returns>The port definition, or <see langword="null"/> when it is not declared.</returns>
    public PortDefinition? FindPort(string portId)
        => Ports.FirstOrDefault(port => string.Equals(port.Id, portId, StringComparison.Ordinal));
}
