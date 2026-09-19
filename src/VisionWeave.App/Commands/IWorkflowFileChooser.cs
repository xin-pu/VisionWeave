namespace VisionWeave.App.Commands;

/// <summary>
/// The shell's questions to the user about a file: which document to open, and
/// where a document that has never been saved should be written. It is a seam
/// rather than a dialog call inside the view model, so the open and save flows —
/// including a dismissed dialog — are covered by a test that never opens a window.
/// </summary>
internal interface IWorkflowFileChooser
{
    /// <summary>
    /// Asks the user for the document to open.
    /// </summary>
    /// <param name="currentPath">The file the session is editing, or <see langword="null"/>.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the user dismissed the dialog.</returns>
    string? ChooseDocumentToOpen(string? currentPath);

    /// <summary>
    /// Asks the user where the document should be written, which is the one
    /// question a document that has never been saved cannot answer itself.
    /// </summary>
    /// <param name="currentPath">The file the session is editing, or <see langword="null"/>.</param>
    /// <param name="suggestedName">The document's own name, offered as the file name.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the user dismissed the dialog.</returns>
    string? ChooseDocumentToSave(string? currentPath, string suggestedName);
}
