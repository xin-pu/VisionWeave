using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Editing;

/// <summary>
/// The single undo stack of one editing session. A caller submits user intent as
/// an <see cref="IDocumentCommand"/> and learns from the returned result whether
/// the document changed and, when it did not, why. The history holds the applied
/// commands, so undo needs no document copy and the caller never supplies the
/// state to restore.
/// </summary>
public sealed class DocumentCommandHistory
{
    private readonly List<IDocumentCommand> _undo = [];
    private readonly List<IDocumentCommand> _redo = [];
    private readonly DocumentEditingOptions _options;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset? _lastAppliedUtc;

    /// <summary>
    /// Initializes a history over one document.
    /// </summary>
    /// <param name="document">The document the commands edit.</param>
    /// <param name="options">The undo-unit options, or the defaults.</param>
    /// <param name="timeProvider">The clock the coalescing window is measured on.</param>
    public DocumentCommandHistory(
        WorkflowDocument document,
        DocumentEditingOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        Document = document;
        _options = options ?? DocumentEditingOptions.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets the document the commands edit.
    /// </summary>
    public WorkflowDocument Document { get; }

    /// <summary>
    /// Gets a value indicating whether an edit can be undone.
    /// </summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>
    /// Gets a value indicating whether an undone edit can be redone.
    /// </summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// Applies an edit. A refused edit changes nothing, including the history, so
    /// a rejected intent is never an undo step. An accepted edit discards the redo
    /// stack, because the branch it would have restored is gone.
    /// </summary>
    /// <param name="command">The command carrying the user's intent.</param>
    /// <returns>The outcome of the attempt.</returns>
    public DocumentCommandResult Execute(IDocumentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        DocumentCommandResult result;

        try
        {
            result = command.Apply(Document);
        }
        catch (Exception exception) when (IsRefusal(exception))
        {
            // The command named something the document does not hold: a stale
            // projection, or an intent aimed at something already gone.
            return DocumentCommandResult.Refused(DiagnosticCodes.EditRefused, exception.Message);
        }

        if (!result.IsAccepted)
        {
            return result;
        }

        _redo.Clear();

        DateTimeOffset now = _timeProvider.GetUtcNow();
        ICoalescingCommand? unit = _undo.Count > 0 ? _undo[^1] as ICoalescingCommand : null;
        bool coalesced = _lastAppliedUtc is DateTimeOffset applied
            && now - applied <= _options.ParameterCoalescingWindow
            && unit is not null
            && unit.ContinuesInto(command);

        if (coalesced)
        {
            // The unit keeps the command that was applied first, because that is the
            // one holding the value the parameter had before it, so the newer value
            // moves into it: the document already holds that value, and the kept
            // command now describes both ends of the unit.
            unit!.ContinueWith(command);
        }
        else
        {
            _undo.Add(command);
        }

        _lastAppliedUtc = now;
        return DocumentCommandResult.Accepted;
    }

    /// <summary>
    /// Reverses the most recent edit.
    /// </summary>
    /// <returns>The outcome of the attempt.</returns>
    public DocumentCommandResult Undo()
    {
        if (_undo.Count == 0)
        {
            return DocumentCommandResult.Refused(DiagnosticCodes.EmptyHistory, "There is no edit to undo.");
        }

        IDocumentCommand command = _undo[^1];
        command.Revert(Document);
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(command);

        // An undone edit ends the current coalescing unit: the next edit of the
        // same parameter is a new unit even when it arrives in the same moment.
        _lastAppliedUtc = null;
        return DocumentCommandResult.Accepted;
    }

    /// <summary>
    /// Reapplies the most recently undone edit.
    /// </summary>
    /// <returns>The outcome of the attempt.</returns>
    public DocumentCommandResult Redo()
    {
        if (_redo.Count == 0)
        {
            return DocumentCommandResult.Refused(DiagnosticCodes.EmptyHistory, "There is no edit to redo.");
        }

        IDocumentCommand command = _redo[^1];
        DocumentCommandResult result = command.Apply(Document);

        if (!result.IsAccepted)
        {
            // The command was accepted once, against the same document contents it
            // sees now, so a refusal here would mean the document moved underneath
            // the history. Report it rather than pushing an unapplied command.
            return result;
        }

        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(command);
        _lastAppliedUtc = null;
        return DocumentCommandResult.Accepted;
    }

    /// <summary>
    /// Discards the recorded history.
    /// </summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _lastAppliedUtc = null;
    }

    /// <summary>
    /// Decides whether an exception means the intent does not fit the document, as
    /// opposed to a programming error. A null argument stays a programming error
    /// and is allowed to surface.
    /// </summary>
    private static bool IsRefusal(Exception exception)
        => exception is KeyNotFoundException or InvalidOperationException
            || (exception is ArgumentException and not ArgumentNullException);
}
