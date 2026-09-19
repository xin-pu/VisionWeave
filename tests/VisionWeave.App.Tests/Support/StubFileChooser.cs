using VisionWeave.App.Commands;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// Stands in for the file dialog, so the open and save flows — including the case
/// where the user dismisses the dialog — are covered without opening a window.
/// Opening and saving are queued apart, because a test that saves and then opens
/// is asking two different questions.
/// </summary>
internal sealed class StubFileChooser : IWorkflowFileChooser
{
    private readonly Queue<string?> _openAnswers = new();
    private readonly Queue<string?> _saveAnswers = new();

    /// <summary>Gets the file the shell offered as the current one, once per open dialog.</summary>
    internal List<string?> Asked { get; } = [];

    /// <summary>Gets the name the shell offered for a document that has never been saved.</summary>
    internal List<string> SuggestedNames { get; } = [];

    /// <summary>Gets the number of times the open dialog was opened.</summary>
    internal int AskCount => Asked.Count;

    /// <summary>Gets the number of times the save dialog was opened.</summary>
    internal int SaveCount => SuggestedNames.Count;

    /// <summary>
    /// Gets or sets what a test observes at the moment the save dialog is asked.
    /// The write runs where the gesture happened, so the one moment a flow can be
    /// seen waiting — and therefore still running — is the dialog it opens.
    /// </summary>
    internal Action? OnAskToSave { get; set; }

    /// <summary>Queues the answer of the next open dialog.</summary>
    /// <param name="path">The chosen path, or <see langword="null"/> for a dismissed dialog.</param>
    /// <returns>This chooser, so answers can be queued in a row.</returns>
    internal StubFileChooser Answer(string? path)
    {
        _openAnswers.Enqueue(path);
        return this;
    }

    /// <summary>Queues the answer of the next save dialog.</summary>
    /// <param name="path">The chosen path, or <see langword="null"/> for a dismissed dialog.</param>
    /// <returns>This chooser, so answers can be queued in a row.</returns>
    internal StubFileChooser AnswerSave(string? path)
    {
        _saveAnswers.Enqueue(path);
        return this;
    }

    /// <inheritdoc />
    public string? ChooseDocumentToOpen(string? currentPath)
    {
        Asked.Add(currentPath);

        // A dialog that was never queued answers as a dismissed one, so a test that
        // does not expect the dialog to open fails on AskCount instead of hanging.
        return _openAnswers.Count == 0 ? null : _openAnswers.Dequeue();
    }

    /// <inheritdoc />
    public string? ChooseDocumentToSave(string? currentPath, string suggestedName)
    {
        SuggestedNames.Add(suggestedName);
        OnAskToSave?.Invoke();

        return _saveAnswers.Count == 0 ? null : _saveAnswers.Dequeue();
    }
}
