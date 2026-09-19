using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Commands;

/// <summary>
/// Turns a path into a workflow session for the shell, whether the document is
/// opened from its file or resumed from the working copy a crash left behind.
/// The seam exists so the command's failure and cancellation behaviour is covered
/// without a file system that has to be made slow, locked, or unreadable on
/// demand.
/// </summary>
internal interface IDocumentLoader
{
    /// <summary>Opens the document stored at a path.</summary>
    /// <param name="path">The document path.</param>
    /// <returns>The session, or the diagnostics that explain why it could not be opened.</returns>
    WorkflowSessionResult Open(string path);

    /// <summary>Opens the recoverable working copy of the document at a path.</summary>
    /// <param name="path">The path of the document the working copy belongs to.</param>
    /// <returns>The session bound to that path, or the diagnostics that explain the failure.</returns>
    WorkflowSessionResult Recover(string path);

    /// <summary>Determines whether a document has a working copy that could be recovered.</summary>
    /// <param name="path">The document path.</param>
    /// <returns><see langword="true"/> when a working copy exists.</returns>
    bool HasWorkingCopy(string path);
}
