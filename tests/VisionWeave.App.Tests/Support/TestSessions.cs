using VisionWeave.App.Commands;
using VisionWeave.App.Sessions;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Validation;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// Builds the editing session a test drives. The document loader and the node
/// catalog are the pair a test varies: an empty catalog makes every node the
/// document holds an unknown type, which is how a validation error is produced
/// without inventing a node definition.
/// </summary>
internal static class TestSessions
{
    /// <summary>Creates a session over the real loader and an empty catalog.</summary>
    /// <param name="loader">The document loader, or the one the shell ships.</param>
    /// <param name="catalog">The catalog to validate against, or an empty one.</param>
    /// <param name="timeProvider">The clock the session reads, or the system clock.</param>
    /// <returns>The session.</returns>
    internal static EditorSession Create(
        IDocumentLoader? loader = null,
        NodeDefinitionCatalog? catalog = null,
        TimeProvider? timeProvider = null)
        => new(
            loader ?? new DocumentLoader(),
            new WorkflowValidator(catalog ?? NodeDefinitionCatalog.Empty),
            timeProvider ?? TimeProvider.System);
}
