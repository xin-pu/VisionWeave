using Microsoft.Win32;
using VisionWeave.Contracts.Nodes;

namespace VisionWeave.App.Inspector;

/// <summary>
///     Uses the Windows file and folder pickers for path parameters.
/// </summary>
internal sealed class WindowsParameterPathChooser : IParameterPathChooser
{
    /// <inheritdoc />
    public ParameterPathChoice ChoosePath(
        PathSelectionMode mode,
        string? currentPath,
        string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return new ParameterPathChoice(
                null,
                "Save the workflow before choosing a file so its relative path has a project directory.");
        }

        string? selected = mode switch
        {
            PathSelectionMode.SaveFile => ChooseFileToSave(currentPath, workingDirectory),
            PathSelectionMode.Folder => ChooseFolder(currentPath, workingDirectory),
            _ => ChooseFileToOpen(currentPath, workingDirectory),
        };

        if (selected is null)
        {
            return ParameterPathChoice.Dismissed;
        }

        return PortablePath(selected, workingDirectory);
    }

    internal static ParameterPathChoice PortablePath(string selected, string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selected);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        string root = System.IO.Path.GetFullPath(workingDirectory);
        string relative = System.IO.Path.GetRelativePath(root, System.IO.Path.GetFullPath(selected));

        if (System.IO.Path.IsPathRooted(relative)
            || relative == ".."
            || relative.StartsWith($"..{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return new ParameterPathChoice(
                null,
                "Choose a location inside the workflow directory. VisionWeave stores portable relative paths and cannot use a file outside the project.");
        }

        return new ParameterPathChoice(relative, null);
    }

    private static string? ChooseFileToOpen(string? currentPath, string workingDirectory)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Choose a file",
            CheckFileExists = true,
            Multiselect = false,
            FileName = ExistingFileName(currentPath, workingDirectory),
            InitialDirectory = InitialDirectory(currentPath, workingDirectory),
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? ChooseFileToSave(string? currentPath, string workingDirectory)
    {
        SaveFileDialog dialog = new()
        {
            Title = "Choose an output file",
            OverwritePrompt = true,
            FileName = FileName(currentPath),
            InitialDirectory = InitialDirectory(currentPath, workingDirectory),
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? ChooseFolder(string? currentPath, string workingDirectory)
    {
        OpenFolderDialog dialog = new()
        {
            Title = "Choose a folder",
            InitialDirectory = ExistingFolder(currentPath, workingDirectory),
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private static string InitialDirectory(string? path, string workingDirectory)
    {
        string absolute = Absolute(path, workingDirectory);
        string? directory = System.IO.Path.GetDirectoryName(absolute);
        return directory is not null && System.IO.Directory.Exists(directory) ? directory : string.Empty;
    }

    private static string ExistingFolder(string? path, string workingDirectory)
    {
        string absolute = Absolute(path, workingDirectory);
        return System.IO.Directory.Exists(absolute) ? absolute : workingDirectory;
    }

    private static string ExistingFileName(string? path, string workingDirectory)
    {
        string absolute = Absolute(path, workingDirectory);
        return System.IO.File.Exists(absolute) ? System.IO.Path.GetFileName(absolute) : string.Empty;
    }

    private static string FileName(string? path)
        => string.IsNullOrWhiteSpace(path) ? string.Empty : System.IO.Path.GetFileName(path);

    private static string Absolute(string? path, string workingDirectory)
        => string.IsNullOrWhiteSpace(path)
            ? workingDirectory
            : System.IO.Path.GetFullPath(path, workingDirectory);
}
