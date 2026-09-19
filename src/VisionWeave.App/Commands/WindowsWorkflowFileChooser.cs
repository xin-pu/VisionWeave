using Microsoft.Win32;

namespace VisionWeave.App.Commands;

/// <summary>
/// Asks for a document through the Windows file dialog. The dialog is the only
/// thing this type does: it returns the chosen path and decides nothing about
/// whether that path can be read.
/// </summary>
internal sealed class WindowsWorkflowFileChooser : IWorkflowFileChooser
{
    /// <summary>The filter offered by the dialog, so a stored workflow is easy to find.</summary>
    internal const string DocumentFilter = "VisionWeave workflow (*.vwflow)|*.vwflow|All files (*.*)|*.*";

    /// <inheritdoc />
    public string? ChooseDocumentToOpen(string? currentPath)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Open workflow",
            Filter = DocumentFilter,
            CheckFileExists = true,
            Multiselect = false,
        };

        // Opening a second document from the folder the first one came from is the
        // common case, so the dialog starts there rather than at the last folder
        // some other application used.
        if (currentPath is not null
            && System.IO.Path.GetDirectoryName(currentPath) is { Length: > 0 } directory
            && System.IO.Directory.Exists(directory))
        {
            dialog.InitialDirectory = directory;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
