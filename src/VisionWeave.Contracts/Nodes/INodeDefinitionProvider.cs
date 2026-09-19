namespace VisionWeave.Contracts.Nodes;

/// <summary>
/// Supplies node definitions to the composition root. Built-in nodes and future
/// plugins implement this same contract.
/// </summary>
public interface INodeDefinitionProvider
{
    /// <summary>
    /// Gets the provider-qualified prefix shared by the definitions it supplies.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Gets the definitions this provider contributes.
    /// </summary>
    /// <returns>The node definitions owned by the provider.</returns>
    IReadOnlyCollection<NodeDefinition> GetDefinitions();
}
