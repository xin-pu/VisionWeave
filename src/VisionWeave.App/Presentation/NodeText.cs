using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.App.Presentation;

/// <summary>
/// Names a node the way the shell shows it, so the canvas and the inspector never
/// describe the same instance in two different words. Both read a committed
/// document, and both fall back the same way when this build cannot resolve the
/// definition a node was saved with.
/// </summary>
internal static class NodeText
{
    /// <summary>
    /// Names a node: the label its author gave it, or the name of the type it was
    /// saved as. A type this build does not provide is named as such rather than
    /// left blank, because the node still exists in the document.
    /// </summary>
    /// <param name="instance">The node instance.</param>
    /// <param name="definition">The definition it resolves to, or <see langword="null"/>.</param>
    /// <returns>The title the shell shows.</returns>
    internal static string Title(NodeInstance instance, NodeDefinition? definition)
        => string.IsNullOrWhiteSpace(instance.Label)
            ? definition?.DisplayName ?? "Unknown node type"
            : instance.Label;

    /// <summary>
    /// Names the type and version a node was saved as, and says so when this build
    /// does not provide that type.
    /// </summary>
    /// <param name="instance">The node instance.</param>
    /// <param name="definition">The definition it resolves to, or <see langword="null"/>.</param>
    /// <returns>The line under the title.</returns>
    internal static string Caption(NodeInstance instance, NodeDefinition? definition)
    {
        string identified = $"{instance.NodeTypeId.Value} v{instance.TypeVersion}";

        return definition is null ? $"{identified} · not installed" : identified;
    }
}
