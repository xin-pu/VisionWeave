using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Commands;

/// <summary>
/// Turns a path into a workflow session for the shell. The seam exists so the
/// command's failure and cancellation behaviour is covered without a file system
/// that has to be made slow, locked, or unreadable on demand.
/// </summary>
internal interface IDocumentLoader
{
    /// <summary>Opens the document stored at a path.</summary>
    /// <param name="path">The document path.</param>
    /// <returns>The session, or the diagnostics that explain why it could not be opened.</returns>
    WorkflowSessionResult Open(string path);
}
