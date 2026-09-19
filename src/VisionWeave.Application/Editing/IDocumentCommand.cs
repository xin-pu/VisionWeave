using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Editing;

/// <summary>
/// One reversible edit to a workflow document. A command owns the data its own
/// reversal needs — the identifiers it created, the values it replaced, the
/// entries it removed — so that undo never rebuilds a whole document and the
/// caller supplies only the intent, never the previous state.
/// </summary>
public interface IDocumentCommand
{
    /// <summary>
    /// Applies the edit, or refuses it without changing the document.
    /// </summary>
    /// <param name="document">The document to edit.</param>
    /// <returns>The outcome of the attempt.</returns>
    DocumentCommandResult Apply(WorkflowDocument document);

    /// <summary>
    /// Reverses an edit that was applied, restoring the document to the state
    /// before it. Called only on a command that is currently applied.
    /// </summary>
    /// <param name="document">The document to restore.</param>
    void Revert(WorkflowDocument document);
}
