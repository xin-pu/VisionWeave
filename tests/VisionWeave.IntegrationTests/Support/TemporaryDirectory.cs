namespace VisionWeave.IntegrationTests.Support;

/// <summary>
/// A directory under the temporary path that is deleted when the test ends, so a
/// test that reads or writes a real file leaves nothing behind in the repository.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    internal TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"visionweave-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// Gets the absolute path of the directory, which is also the working
    /// directory a test-run resolves relative paths against.
    /// </summary>
    internal string Path { get; }

    /// <summary>
    /// Builds the absolute path of a named file inside the directory, which is
    /// what a document names relative to the workflow.
    /// </summary>
    /// <param name="name">The name of the file, which may name a subfolder.</param>
    /// <returns>The absolute path.</returns>
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
