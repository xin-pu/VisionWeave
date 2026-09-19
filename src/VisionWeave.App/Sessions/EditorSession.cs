using CommunityToolkit.Mvvm.ComponentModel;
using VisionWeave.App.Commands;
using VisionWeave.Application.Editing;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Sessions;

/// <summary>
/// The one editing session the shell presents: the document, the file it belongs
/// to, the undo stack, what is selected, and the validation projection that
/// answers for that selection. Every transition a session can make — new, open,
/// recover, save, autosave, edit, undo, redo — goes through this object, and each
/// one reports a diagnostic instead of throwing, so the shell never has to
/// interpret an exception to explain itself. No WPF or Nodify type appears here,
/// so a whole session, including the failure paths, runs in a headless test.
/// </summary>
internal sealed partial class EditorSession : ObservableObject
{
    /// <summary>The name a document carries before it has ever been saved.</summary>
    internal const string UntitledDocumentName = "Untitled";

    private readonly IDocumentLoader _loader;
    private readonly WorkflowValidator _validator;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the session, which starts on an empty untitled document.
    /// </summary>
    /// <param name="loader">The seam that turns a path into a document.</param>
    /// <param name="validator">The validator that produces the projection.</param>
    /// <param name="timeProvider">The clock the document and the undo stack read.</param>
    /// <remarks>
    /// The constructor is public because the host's service container resolves this
    /// type and only considers public constructors; the type itself stays internal
    /// to the application.
    /// </remarks>
    public EditorSession(IDocumentLoader loader, WorkflowValidator validator, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _loader = loader;
        _validator = validator;
        _timeProvider = timeProvider;

        Session = WorkflowSession.New(UntitledDocumentName, timeProvider);
        _history = CreateHistory(Session.Document);
        Projection = validator.Project(Session.Document);
        Selection = [];
    }

    private DocumentCommandHistory _history;

    /// <summary>
    /// Gets the document session: the document, its file, whether that file may be
    /// written, and whether it holds changes the file does not.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Document))]
    [NotifyPropertyChangedFor(nameof(Path))]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    [NotifyPropertyChangedFor(nameof(IsReadOnly))]
    [NotifyPropertyChangedFor(nameof(Diagnostics))]
    internal partial WorkflowSession Session { get; private set; }

    /// <summary>
    /// Gets the validation projection of the document, replaced after every
    /// accepted edit so a node badge never describes a revision the document has
    /// moved past.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDocumentValid))]
    [NotifyPropertyChangedFor(nameof(SelectionDiagnostics))]
    internal partial ValidationProjection Projection { get; private set; }

    /// <summary>Gets the selected node instances, which the projection answers for.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionDiagnostics))]
    internal partial IReadOnlyList<Guid> Selection { get; private set; }

    /// <summary>Gets the document being edited.</summary>
    internal WorkflowDocument Document => Session.Document;

    /// <summary>
    /// Gets the file the document belongs to, or <see langword="null"/> while it has
    /// never been saved.
    /// </summary>
    internal string? Path => Session.Path;

    /// <summary>Gets a value indicating whether the document holds changes its file does not.</summary>
    internal bool IsDirty => Session.IsDirty;

    /// <summary>
    /// Gets a value indicating whether the file must not be written, because this
    /// build would discard content it does not understand.
    /// </summary>
    internal bool IsReadOnly => Session.IsReadOnly;

    /// <summary>
    /// Gets a value indicating whether the document can be executed, which is the
    /// projection's answer for the whole document.
    /// </summary>
    internal bool IsDocumentValid => Projection.IsValid;

    /// <summary>
    /// Gets the conditions observed while the document was opened, which explain a
    /// recovered or partially unreadable file after it has been read.
    /// </summary>
    internal IReadOnlyList<NodeDiagnostic> Diagnostics => Session.Diagnostics;

    /// <summary>
    /// Gets the validation state of the current selection, which is the union of
    /// the selected nodes' diagnostics and the document-level ones.
    /// </summary>
    internal SelectionValidation SelectionDiagnostics => Projection.Select(Selection);

    /// <summary>Gets a value indicating whether an edit can be undone.</summary>
    internal bool CanUndo => _history.CanUndo;

    /// <summary>Gets a value indicating whether an undone edit can be redone.</summary>
    internal bool CanRedo => _history.CanRedo;

    /// <summary>
    /// Starts a new, empty document in place of the current one.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <returns>The new session, which has no path until it is saved.</returns>
    internal WorkflowSession New(string name)
    {
        Session = WorkflowSession.New(name, _timeProvider);
        return Session;
    }

    /// <summary>
    /// Opens the document stored at a path, replacing the current document when the
    /// read succeeds.
    /// </summary>
    /// <param name="path">The document path.</param>
    /// <param name="cancellationToken">
    /// The token that stops the open. A stopped open leaves the current document in
    /// place, because the read itself cannot be interrupted and its result has
    /// already arrived.
    /// </param>
    /// <returns>The session, or the diagnostics that explain why it could not be opened.</returns>
    internal WorkflowSessionResult Open(string path, CancellationToken cancellationToken = default)
    {
        WorkflowSessionResult result = _loader.Open(path);
        cancellationToken.ThrowIfCancellationRequested();
        return Adopt(result);
    }

    /// <summary>
    /// Resumes the working copy of the document at a path, which stays bound to that
    /// path so saving overwrites the document rather than the copy. The recovered
    /// session starts with unsaved changes, because its content is not yet the
    /// content of its file.
    /// </summary>
    /// <param name="path">The path of the document the working copy belongs to.</param>
    /// <param name="cancellationToken">The token that stops the open.</param>
    /// <returns>The session, or the diagnostics that explain why it could not be recovered.</returns>
    internal WorkflowSessionResult Recover(string path, CancellationToken cancellationToken = default)
    {
        WorkflowSessionResult result = _loader.Recover(path);
        cancellationToken.ThrowIfCancellationRequested();
        return Adopt(result);
    }

    /// <summary>
    /// Determines whether the document at a path has a working copy, so the shell
    /// can offer recovery before it opens anything.
    /// </summary>
    /// <param name="path">The document path.</param>
    /// <returns><see langword="true"/> when a working copy exists.</returns>
    internal bool HasWorkingCopy(string path) => _loader.HasWorkingCopy(path);

    /// <summary>
    /// Saves the document over the file it belongs to.
    /// </summary>
    /// <returns>
    /// An empty list when the document was written, or the diagnostic that explains
    /// why it was not.
    /// </returns>
    internal IReadOnlyList<NodeDiagnostic> Save()
        => Session.Path is { } path
            ? SaveAs(path)
            : [Refused(
                DiagnosticCodes.MissingDocumentPath,
                "This document has no file yet. Save it to a path first.")];

    /// <summary>
    /// Saves the document to a path, which becomes the file the session belongs to.
    /// A successful save also drops the working copy, because the file now holds the
    /// same content and a stale copy would only offer a pointless recovery.
    /// </summary>
    /// <param name="path">The destination path.</param>
    /// <returns>
    /// An empty list when the document was written, or the diagnostic that explains
    /// why it was not.
    /// </returns>
    internal IReadOnlyList<NodeDiagnostic> SaveAs(string path)
    {
        if (Session.IsReadOnly)
        {
            return
            [
                Refused(
                    DiagnosticCodes.UnsupportedDocumentSchema,
                    "This document was opened read-only, so saving it would discard content this build does not understand."),
            ];
        }

        try
        {
            Session.Save(path);
        }
        catch (Exception exception) when (DocumentLoader.IsUnusablePath(exception))
        {
            return
            [
                Refused(
                    DiagnosticCodes.UnreadableDocument,
                    "The workflow file could not be written. Check that the folder exists and that you are allowed to write to it.",
                    exception),
            ];
        }

        NotifyDocumentChanged();
        return [];
    }

    /// <summary>
    /// Writes the recoverable working copy of the document. The write runs through
    /// the same path classification a deliberate save uses, so an unusable
    /// destination is a diagnostic here too rather than an exception that reaches a
    /// caller outside the command boundary.
    /// </summary>
    /// <returns>
    /// <see cref="AutosaveOutcome.Written"/> when a working copy was written,
    /// <see cref="AutosaveOutcome.NotApplicable"/> when there is nothing to autosave
    /// because the document is unsaved or unchanged,
    /// <see cref="AutosaveOutcome.Refused"/> when the document must not be written,
    /// or <see cref="AutosaveOutcome.Failed"/> with the diagnostic that explains a
    /// write the file system refused.
    /// </returns>
    internal AutosaveResult Autosave()
    {
        if (Session.IsReadOnly)
        {
            return AutosaveResult.Refused(
                DiagnosticCodes.UnsupportedDocumentSchema,
                "This document was opened read-only, so no working copy is written.");
        }

        try
        {
            if (!Session.TryAutosave())
            {
                return AutosaveResult.NotApplicable;
            }
        }
        catch (Exception exception) when (DocumentLoader.IsUnusablePath(exception))
        {
            return AutosaveResult.Failed(
                DiagnosticCodes.UnreadableDocument,
                "The working copy could not be written. Check that the folder exists and that you are allowed to write to it.",
                exception);
        }

        NotifyDocumentChanged();
        return AutosaveResult.Written;
    }

    /// <summary>
    /// Applies an edit to the document. The history owns the undo data, so the
    /// caller supplies intent and learns the outcome; a refused edit changes
    /// nothing and is not an undo step.
    /// </summary>
    /// <param name="command">The command carrying the user's intent.</param>
    /// <returns>The outcome of the attempt.</returns>
    internal DocumentCommandResult Execute(IDocumentCommand command)
    {
        DocumentCommandResult result = _history.Execute(command);
        NotifyCommitted(result);
        return result;
    }

    /// <summary>
    /// Reverses the most recent edit.
    /// </summary>
    /// <returns>The outcome of the attempt.</returns>
    internal DocumentCommandResult Undo()
    {
        DocumentCommandResult result = _history.Undo();
        NotifyCommitted(result);
        return result;
    }

    /// <summary>
    /// Reapplies the most recently undone edit.
    /// </summary>
    /// <returns>The outcome of the attempt.</returns>
    internal DocumentCommandResult Redo()
    {
        DocumentCommandResult result = _history.Redo();
        NotifyCommitted(result);
        return result;
    }

    /// <summary>
    /// Replaces the selection the projection answers for. A selection may name nodes
    /// the document no longer holds, so the selection is stored as intent and the
    /// projection decides what still applies.
    /// </summary>
    /// <param name="nodeInstanceIds">The selected node instances.</param>
    internal void Select(IEnumerable<Guid> nodeInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(nodeInstanceIds);

        Selection = [.. nodeInstanceIds];
    }

    private WorkflowSessionResult Adopt(WorkflowSessionResult result)
    {
        if (result.Session is { } opened)
        {
            Session = opened;
        }

        return result;
    }

    /// <summary>
    /// Replaces everything that belongs to one document when a session takes over:
    /// the history holds the commands of the document it was built for, and a
    /// projection answers for one revision, so neither survives the swap.
    /// </summary>
    partial void OnSessionChanged(WorkflowSession value)
    {
        _history = CreateHistory(value.Document);
        Projection = _validator.Project(value.Document);
        Selection = [];

        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private DocumentCommandHistory CreateHistory(WorkflowDocument document)
        => new(document, timeProvider: _timeProvider);

    private void NotifyCommitted(DocumentCommandResult result)
    {
        if (!result.IsAccepted)
        {
            return;
        }

        Projection = _validator.Project(Session.Document);
        NotifyDocumentChanged();
    }

    /// <summary>
    /// Reports that the document's state moved while the session object stayed the
    /// same: the shell reads the dirty flag, the path, and the undo availability
    /// through this object, so it has to hear about a change the property setter
    /// never saw.
    /// </summary>
    private void NotifyDocumentChanged()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private static NodeDiagnostic Refused(string code, string message, Exception? exception = null)
        => new(code, DiagnosticSeverity.Error, message, null, exception);
}
