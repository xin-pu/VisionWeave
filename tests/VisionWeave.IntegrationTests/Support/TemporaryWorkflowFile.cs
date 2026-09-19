namespace VisionWeave.IntegrationTests.Support;

/// <summary>
/// A saved workflow file that is deleted with its directory when the test ends,
/// so a file-based execution test leaves nothing behind in the repository.
/// </summary>
internal sealed class TemporaryWorkflowFile : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    internal TemporaryWorkflowFile(string name) => Path = _directory.File(name);

    internal string Path { get; }

    public void Dispose() => _directory.Dispose();
}
