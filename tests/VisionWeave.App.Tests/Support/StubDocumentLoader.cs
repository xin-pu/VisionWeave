using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// A loader whose behaviour a test chooses, so an open can be made slow, failed,
/// or unreadable on demand while the reader and the writer keep their own tests.
/// </summary>
internal sealed class StubDocumentLoader : IDocumentLoader
{
    private readonly Func<string, WorkflowSessionResult> _open;

    /// <summary>Creates a loader that answers with the supplied behaviour.</summary>
    /// <param name="open">The behaviour of one open.</param>
    internal StubDocumentLoader(Func<string, WorkflowSessionResult> open)
    {
        ArgumentNullException.ThrowIfNull(open);

        _open = open;
    }

    /// <summary>Gets the number of times the loader was asked to open a file.</summary>
    internal int OpenCount { get; private set; }

    /// <inheritdoc />
    public WorkflowSessionResult Open(string path)
    {
        OpenCount++;
        return _open(path);
    }
}
