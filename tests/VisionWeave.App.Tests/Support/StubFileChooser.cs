using VisionWeave.App.Commands;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// Stands in for the file dialog, so the open flow — including the case where the
/// user dismisses the dialog — is covered without opening a window.
/// </summary>
internal sealed class StubFileChooser : IWorkflowFileChooser
{
    private readonly Queue<string?> _answers = new();

    /// <summary>Gets the file the shell offered as the current one, once per dialog.</summary>
    internal List<string?> Asked { get; } = [];

    /// <summary>Gets the number of times the dialog was opened.</summary>
    internal int AskCount => Asked.Count;

    /// <summary>Queues the answer of the next dialog.</summary>
    /// <param name="path">The chosen path, or <see langword="null"/> for a dismissed dialog.</param>
    /// <returns>This chooser, so answers can be queued in a row.</returns>
    internal StubFileChooser Answer(string? path)
    {
        _answers.Enqueue(path);
        return this;
    }

    /// <inheritdoc />
    public string? ChooseDocumentToOpen(string? currentPath)
    {
        Asked.Add(currentPath);

        // A dialog that was never queued answers as a dismissed one, so a test that
        // does not expect the dialog to open fails on AskCount instead of hanging.
        return _answers.Count == 0 ? null : _answers.Dequeue();
    }
}
