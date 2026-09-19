using System.Diagnostics.CodeAnalysis;

namespace VisionWeave.Contracts.Files;

/// <summary>
/// Resolves the path a node declared into the absolute path it names. A workflow
/// stores a path relative to its own folder (ADR-0012), so every node that reads
/// or writes a file resolves it here instead of composing a path itself, and a
/// path that leaves the folder is refused rather than quietly aimed elsewhere.
/// <para>
/// The rule never throws. An unusable input is a refusal like a missing working
/// directory is, because a node reports an expected condition as a diagnostic the
/// user can read rather than as an exception the run boundary has to log.
/// </para>
/// </summary>
public static class WorkDirectoryPath
{
    /// <summary>
    /// Resolves a declared path against the directory the run is allowed to read
    /// and write.
    /// </summary>
    /// <param name="workingDirectory">The absolute directory of the document being run, when the run has one.</param>
    /// <param name="declaredPath">The path exactly as the document recorded it.</param>
    /// <param name="resolvedPath">The absolute path the declaration names, when it is accepted.</param>
    /// <param name="refusal">The reason the declaration was refused, when it was.</param>
    /// <returns><see langword="true"/> when the path was resolved.</returns>
    public static bool TryResolve(
        string? workingDirectory,
        string? declaredPath,
        out string resolvedPath,
        [NotNullWhen(false)] out string? refusal)
    {
        resolvedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            refusal = "The workflow has not been saved, so there is no folder to resolve its file paths against. Save it beside the files it uses and run it again.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(declaredPath))
        {
            refusal = "The path is empty, so it names no file.";
            return false;
        }

        if (Path.IsPathRooted(declaredPath))
        {
            refusal = $"The path '{declaredPath}' is absolute. A workflow names its files relative to its own folder, so the path must be relative.";
            return false;
        }

        if (declaredPath.EndsWith(Path.DirectorySeparatorChar) || declaredPath.EndsWith(Path.AltDirectorySeparatorChar))
        {
            refusal = $"The path '{declaredPath}' names a folder. A node reads or writes one file.";
            return false;
        }

        if (NamesAnInvalidFile(declaredPath))
        {
            refusal = $"The path '{declaredPath}' contains a character a file name cannot hold on this platform.";
            return false;
        }

        string candidate;

        try
        {
            candidate = Path.GetFullPath(Path.Combine(workingDirectory, declaredPath));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            refusal = $"The path '{declaredPath}' cannot be resolved, because it is not a path this platform can read: {exception.Message}";
            return false;
        }

        if (!IsInside(workingDirectory, candidate))
        {
            refusal = $"The path '{declaredPath}' leaves the folder of the workflow. A node reads or writes only files beside the workflow.";
            return false;
        }

        resolvedPath = candidate;
        refusal = null;
        return true;
    }

    /// <summary>
    /// Reports whether the resolved path is the working directory itself or a
    /// descendant of it. A relative path that climbs out with <c>..</c> is
    /// reported as outside, and so is one that lands on another volume.
    /// </summary>
    private static bool IsInside(string workingDirectory, string candidate)
    {
        string relative = Path.GetRelativePath(workingDirectory, candidate);

        return !Path.IsPathRooted(relative)
            && relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    /// <summary>
    /// Reports whether any segment of the declared path carries a character the
    /// platform does not allow in a file name. The path is judged as the user
    /// wrote it, so a caller learns about the mistake rather than about what the
    /// combination with the working directory turned it into.
    /// </summary>
    private static bool NamesAnInvalidFile(string declaredPath)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string[] segments = declaredPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment => segment.IndexOfAny(invalid) >= 0);
    }
}
