namespace VisionWeave.Persistence.Tests.Support;

/// <summary>
/// A directory that is removed when the test finishes, so a file-format test
/// never leaves saved documents or temporary files behind in the repository.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    internal TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"visionweave-{Guid.NewGuid():N}");

        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    /// <summary>
    /// Creates a path inside the directory.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <returns>The combined path.</returns>
    internal string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
