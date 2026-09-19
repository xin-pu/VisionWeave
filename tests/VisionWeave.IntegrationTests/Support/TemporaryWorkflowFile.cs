namespace VisionWeave.IntegrationTests.Support;

/// <summary>
/// A saved workflow file that is deleted with its directory when the test ends,
/// so a file-based execution test leaves nothing behind in the repository.
/// </summary>
internal sealed class TemporaryWorkflowFile : IDisposable
{
    private readonly string _directory;

    internal TemporaryWorkflowFile(string name)
    {
        _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"visionweave-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        Path = System.IO.Path.Combine(_directory, name);
    }

    internal string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
