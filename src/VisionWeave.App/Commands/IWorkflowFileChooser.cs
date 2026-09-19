namespace VisionWeave.App.Commands;

/// <summary>
/// The shell's one question to the user about a file: which document to open. It
/// is a seam rather than a dialog call inside the view model, so the open flow —
/// including a dismissed dialog — is covered by a test that never opens a window.
/// </summary>
internal interface IWorkflowFileChooser
{
    /// <summary>
    /// Asks the user for the document to open.
    /// </summary>
    /// <param name="currentPath">The file the session is editing, or <see langword="null"/>.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the user dismissed the dialog.</returns>
    string? ChooseDocumentToOpen(string? currentPath);
}
