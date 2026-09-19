using VisionWeave.App.Commands;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// A loader whose behaviour a test chooses, so an open or a recovery can be made
/// slow, failed, or unreadable on demand while the reader and the writer keep
/// their own tests.
/// </summary>
internal sealed class StubDocumentLoader : IDocumentLoader
{
    private readonly Func<string, WorkflowSessionResult> _open;
    private readonly Func<string, WorkflowSessionResult> _recover;
    private readonly bool _hasWorkingCopy;

    /// <summary>Creates a loader that answers with the supplied behaviour.</summary>
    /// <param name="open">The behaviour of one open.</param>
    /// <param name="recover">The behaviour of one recovery, or a refusal to recover.</param>
    /// <param name="hasWorkingCopy">Whether the loader reports a recoverable copy.</param>
    internal StubDocumentLoader(
        Func<string, WorkflowSessionResult> open,
        Func<string, WorkflowSessionResult>? recover = null,
        bool hasWorkingCopy = false)
    {
        ArgumentNullException.ThrowIfNull(open);

        _open = open;
        _recover = recover ?? (_ => new WorkflowSessionResult(null, []));
        _hasWorkingCopy = hasWorkingCopy;
    }

    /// <summary>Gets the number of times the loader was asked to open a file.</summary>
    internal int OpenCount { get; private set; }

    /// <summary>Gets the number of times the loader was asked to recover a file.</summary>
    internal int RecoverCount { get; private set; }

    /// <inheritdoc />
    public WorkflowSessionResult Open(string path)
    {
        OpenCount++;
        return _open(path);
    }

    /// <inheritdoc />
    public WorkflowSessionResult Recover(string path)
    {
        RecoverCount++;
        return _recover(path);
    }

    /// <inheritdoc />
    public bool HasWorkingCopy(string path) => _hasWorkingCopy;
}
