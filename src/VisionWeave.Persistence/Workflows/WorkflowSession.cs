using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Persistence.Workflows;

/// <summary>
/// One editing session over a stored workflow document: the document being
/// edited, the file it belongs to, whether that file may be written, whether
/// there are unsaved changes, and the working copy that survives a crash. A
/// session never touches a Nodify or WPF type, so an editor can be driven
/// headlessly.
/// </summary>
public sealed class WorkflowSession
{
    private readonly IReadOnlyList<NodeDiagnostic> _diagnostics;
    private long _savedChangeCount;
    private bool _recovered;

    private WorkflowSession(
        WorkflowDocument document,
        string? path,
        bool isReadOnly,
        bool isRecovered,
        IReadOnlyList<NodeDiagnostic> diagnostics)
    {
        Document = document;
        Path = path;
        IsReadOnly = isReadOnly;
        _diagnostics = diagnostics;
        _recovered = isRecovered;
        _savedChangeCount = document.ChangeCount;
    }

    /// <summary>
    /// Gets the document being edited.
    /// </summary>
    public WorkflowDocument Document { get; }

    /// <summary>
    /// Gets the file the document belongs to, or <see langword="null"/> for a
    /// document that has never been saved.
    /// </summary>
    public string? Path { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the file must not be written, because its
    /// schema version is not one this build can migrate and a save would discard
    /// content this build does not understand.
    /// </summary>
    public bool IsReadOnly { get; }

    /// <summary>
    /// Gets a value indicating whether the document has changed since it was
    /// opened, saved, or autosaved. A moved node counts: layout is saved state.
    /// A document recovered from a working copy starts dirty, because its content
    /// is not yet the content of its file.
    /// </summary>
    public bool IsDirty => _recovered || Document.ChangeCount != _savedChangeCount;

    /// <summary>
    /// Gets the conditions observed while opening the document. They are kept so
    /// that a recovered or partially unreadable document can be explained after
    /// the file has been closed.
    /// </summary>
    public IReadOnlyList<NodeDiagnostic> Diagnostics => _diagnostics;

    /// <summary>
    /// Starts a session with a new, empty document.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <param name="timeProvider">The clock the document timestamps use.</param>
    /// <returns>The new session, which has no path until it is saved.</returns>
    public static WorkflowSession New(string name, TimeProvider? timeProvider = null)
        => new(WorkflowDocument.Create(name, timeProvider), null, isReadOnly: false, isRecovered: false, []);

    /// <summary>
    /// Opens the document stored at a path.
    /// </summary>
    /// <param name="path">The document path.</param>
    /// <returns>The session, or the diagnostics that explain why it could not be opened.</returns>
    public static WorkflowSessionResult Open(string path)
        => FromLoad(WorkflowDocumentReader.Load(path), path, isRecovered: false);

    /// <summary>
    /// Opens the recoverable working copy of a document, still bound to the
    /// document path, so that saving a recovered document overwrites the document
    /// rather than the copy.
    /// </summary>
    /// <param name="path">The path of the document the working copy belongs to.</param>
    /// <returns>The session, or the diagnostics that explain why it could not be opened.</returns>
    public static WorkflowSessionResult Recover(string path)
        => FromLoad(WorkflowDocumentReader.Load(WorkflowDocumentWriter.GetWorkingCopyPath(path)), path, isRecovered: true);

    /// <summary>
    /// Determines whether a document has a working copy that could be recovered.
    /// </summary>
    /// <param name="path">The document path.</param>
    /// <returns><see langword="true"/> when a working copy exists.</returns>
    public static bool HasWorkingCopy(string path)
        => File.Exists(WorkflowDocumentWriter.GetWorkingCopyPath(path));

    /// <summary>
    /// Saves the document over the file it was opened from or last saved to.
    /// </summary>
    /// <exception cref="InvalidOperationException">The session has no path, or the file is read-only.</exception>
    public void Save()
    {
        if (Path is null)
        {
            throw new InvalidOperationException("This document has not been saved yet; save it with a path first.");
        }

        Save(Path);
    }

    /// <summary>
    /// Saves the document to a path, which becomes the file the session belongs to.
    /// A successful save also drops the working copy, because the file now holds
    /// the same content and a stale copy would only offer a pointless recovery.
    /// </summary>
    /// <param name="path">The destination path.</param>
    /// <exception cref="InvalidOperationException">The file is read-only.</exception>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        RequireWritable();

        WorkflowDocumentWriter.Save(Document, path);

        Path = System.IO.Path.GetFullPath(path);
        _savedChangeCount = Document.ChangeCount;
        _recovered = false;
        DeleteWorkingCopy();
    }

    /// <summary>
    /// Writes the recoverable working copy of the document, which a later session
    /// can recover when the application did not shut down cleanly.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when a working copy was written; <see langword="false"/>
    /// when there is nothing to autosave, because the document is unsaved or unchanged.
    /// </returns>
    public bool TryAutosave()
    {
        if (Path is null || IsReadOnly || !IsDirty)
        {
            return false;
        }

        WorkflowDocumentWriter.SaveWorkingCopy(Document, Path);
        return true;
    }

    private static WorkflowSessionResult FromLoad(WorkflowLoadResult load, string path, bool isRecovered)
        => load.Succeeded
            ? new WorkflowSessionResult(
                new WorkflowSession(
                    load.Document!,
                    System.IO.Path.GetFullPath(path),
                    load.IsReadOnly,
                    isRecovered,
                    load.Diagnostics),
                load.Diagnostics)
            : new WorkflowSessionResult(null, load.Diagnostics);

    private void RequireWritable()
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException(
                "This document was opened read-only because its schema version is not one this build can migrate, so saving it is refused.");
        }
    }

    private void DeleteWorkingCopy()
    {
        string workingCopy = WorkflowDocumentWriter.GetWorkingCopyPath(Path!);

        try
        {
            File.Delete(workingCopy);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
