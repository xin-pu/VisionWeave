using VisionWeave.Domain.Workflows;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// A temporary directory holding real workflow files, so the document command is
/// tested against the reader and the writer the shell actually ships.
/// </summary>
internal sealed class TemporaryWorkflowDirectory : IDisposable
{
    private readonly string _directory = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        $"visionweave-documents-{Guid.NewGuid():N}");

    /// <summary>Creates the directory.</summary>
    internal TemporaryWorkflowDirectory() => System.IO.Directory.CreateDirectory(_directory);

    /// <summary>Writes a document this build can read.</summary>
    /// <param name="name">The file name to write.</param>
    /// <returns>The path of the written document.</returns>
    internal string SaveReadableDocument(string name = "workflow.vwflow")
    {
        string path = PathOf(name);
        WorkflowDocumentWriter.Save(WorkflowDocument.Create("Saved workflow"), path);
        return path;
    }

    /// <summary>Writes a file that is not a workflow document.</summary>
    /// <param name="name">The file name to write.</param>
    /// <returns>The path of the written file.</returns>
    internal string SaveUnreadableDocument(string name = "broken.vwflow")
    {
        string path = PathOf(name);
        System.IO.File.WriteAllText(path, "{ this is not a workflow document");
        return path;
    }

    /// <summary>Builds a path inside the directory, whether or not it holds a file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The full path.</returns>
    internal string PathOf(string name) => System.IO.Path.Combine(_directory, name);

    /// <inheritdoc />
    public void Dispose()
    {
        if (System.IO.Directory.Exists(_directory))
        {
            System.IO.Directory.Delete(_directory, recursive: true);
        }
    }
}
