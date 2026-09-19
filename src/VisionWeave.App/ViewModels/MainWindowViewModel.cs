using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionWeave.App.Canvas;
using VisionWeave.App.Commands;
using VisionWeave.App.Inspector;
using VisionWeave.App.Preview;
using VisionWeave.App.Sessions;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Nodes;

namespace VisionWeave.App.ViewModels;

/// <summary>
/// Presents the one document the shell currently edits. It reads the session, the
/// catalog, and its own regions but never edits them: every change arrives as an
/// application command, and every string here is derived from session state rather
/// than kept alongside it. The two questions the document cannot answer itself —
/// what to do with unsaved changes, and whether to resume a working copy — are
/// asked through the prompt seam rather than by opening a window of its own.
/// </summary>
internal sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly NodeDefinitionCatalog _catalog;
    private readonly IWorkflowFileChooser _fileChooser;

    internal MainWindowViewModel(
        EditorSession session,
        NodeDefinitionCatalog catalog,
        WorkflowValidator validator,
        OpenDocumentCommand openDocument,
        SaveDocumentCommand saveDocument,
        RunWorkflowCommand runWorkflow,
        PreviewViewModel preview,
        IWorkflowFileChooser fileChooser,
        ShellPromptViewModel prompt,
        ShellStatus status)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(openDocument);
        ArgumentNullException.ThrowIfNull(saveDocument);
        ArgumentNullException.ThrowIfNull(runWorkflow);
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(fileChooser);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(status);

        Session = session;
        Status = status;
        _catalog = catalog;
        _fileChooser = fileChooser;
        Prompt = prompt;
        Preview = preview;
        OpenDocument = openDocument;
        SaveDocument = saveDocument;
        RunWorkflow = runWorkflow;
        Canvas = new CanvasViewModel(session, catalog, validator, status);
        Inspector = new InspectorViewModel(session, catalog, status);
        CatalogueSearch = string.Empty;
        CatalogueGroups = Grouped(null);

        // The session owns the state and raises its own notifications; the strings
        // below read several of its values at once, so any session change refreshes
        // them together instead of leaving one of them stale.
        Session.PropertyChanged += OnSessionPropertyChanged;
    }

    /// <summary>Gets the command that opens a workflow document into this shell.</summary>
    internal OpenDocumentCommand OpenDocument { get; }

    /// <summary>Gets the command that writes the document to its file.</summary>
    internal SaveDocumentCommand SaveDocument { get; }

    /// <summary>Gets the command that runs the document and reports what it produced.</summary>
    /// <remarks>
    /// Public so the shell's Run and Cancel actions can bind through it, which is
    /// what keeps two gestures over one running state in one object; the type stays
    /// internal, and WPF reaches its public members from here.
    /// </remarks>
    public RunWorkflowCommand RunWorkflow { get; }

    /// <summary>Gets the managed preview of the newest image a run published.</summary>
    /// <remarks>Public so the preview region can bind through it.</remarks>
    public PreviewViewModel Preview { get; }

    /// <summary>
    /// Gets the editing session this shell presents. The session replaces the
    /// document inside itself, so the presentation keeps reading one authoritative
    /// object and never has to rebuild its own state.
    /// </summary>
    internal EditorSession Session { get; }

    /// <summary>Gets the durable state the status area reports.</summary>
    /// <remarks>
    /// Public because the status region binds through it: WPF binds only to public
    /// members, and the first step of the path is a member like any other. The type
    /// itself stays internal.
    /// </remarks>
    public ShellStatus Status { get; }

    /// <summary>Gets the canvas: the document projected onto nodes, ports, and wires.</summary>
    /// <remarks>
    /// Public for the same reason as <see cref="Status"/>: every binding beneath it
    /// starts here.
    /// </remarks>
    public CanvasViewModel Canvas { get; }

    /// <summary>
    /// Gets the inspector: the selection's parameters and the conditions it carries.
    /// </summary>
    /// <remarks>Public so the inspector region can bind through it.</remarks>
    public InspectorViewModel Inspector { get; }

    /// <summary>
    /// Gets the surface a question about the document is drawn on. A flow that needs
    /// an answer asks through it, so asking and showing are the same object.
    /// </summary>
    /// <remarks>Public so the prompt can be drawn over the shell.</remarks>
    public ShellPromptViewModel Prompt { get; }

    /// <summary>
    /// Gets the node catalogue, grouped by the category each definition declares and
    /// filtered by <see cref="CatalogueSearch"/>.
    /// </summary>
    /// <remarks>
    /// Public because the catalogue region binds to it: WPF binds only to public
    /// members, and a binding to anything else fails silently rather than raising an
    /// error. The type itself stays internal.
    /// </remarks>
    [ObservableProperty]
    public partial IReadOnlyList<ShellCatalogueGroup> CatalogueGroups { get; private set; }

    /// <summary>
    /// Gets or sets what the user is looking for in the catalogue. It filters what
    /// exists rather than changing it, so a search that matches nothing leaves the
    /// document and the catalog exactly as they were.
    /// </summary>
    [ObservableProperty]
    public partial string CatalogueSearch { get; set; }

    public string NodeCatalogSummary
        => CatalogueSearch.Trim().Length == 0
            ? $"{_catalog.KnownTypeIds.Count} node types available"
            : $"{Matched()} of {_catalog.KnownTypeIds.Count} node types match “{CatalogueSearch.Trim()}”";

    /// <summary>
    /// Gets what the catalogue says when the search matched nothing, so an empty
    /// region explains itself instead of looking like a catalog that holds nothing.
    /// </summary>
    public string CatalogueNotice
        => CatalogueGroups.Count == 0 && CatalogueSearch.Trim().Length > 0
            ? "No node type matches this search."
            : string.Empty;

    public string WindowTitle
        => $"{DocumentTitle} — VisionWeave";

    public string DocumentTitle
        => Session.Path is null ? Session.Document.Name : System.IO.Path.GetFileName(Session.Path);

    public string DocumentCounts
        => $"{Count(Session.Document.Nodes.Count, "node")}, {Count(Session.Document.Connections.Count, "connection")}";

    public string SelectionSummary
        => Session.Selection.Count switch
        {
            0 => "Nothing selected",
            1 => "1 node selected",
            _ => $"{Session.Selection.Count} nodes selected",
        };

    public string SelectionConditionSummary
    {
        get
        {
            SelectionValidation summary = Session.SelectionDiagnostics;

            return summary.Diagnostics.Count == 0
                ? "No conditions"
                : $"{summary.Diagnostics.Count} conditions, highest severity {summary.Severity}";
        }
    }

    /// <summary>
    /// Gets a value indicating whether the document was opened read-only, because
    /// this build does not understand everything it stores.
    /// </summary>
    public bool IsReadOnly => Session.IsReadOnly;

    /// <summary>
    /// Gets the reason the document is read-only. It is a sentence rather than a
    /// flag, because a field that cannot be used and does not say why reads as a
    /// defect of the shell.
    /// </summary>
    public string ReadOnlyNote
        => "This document was written by a build that stores more than this one understands, so it opened read-only. Editing it would discard that content.";

    /// <summary>
    /// Gets the command the shell's Open action calls. It asks for a file, asks
    /// about what the current document still holds, and hands the chosen path to the
    /// open command; the session and the command boundary own every outcome.
    /// </summary>
    [RelayCommand]
    private async Task OpenAsync()
    {
        string? path = _fileChooser.ChooseDocumentToOpen(Session.Path);
        if (path is null)
        {
            // The user dismissed the dialog, so there is no intent to carry out and
            // nothing to report.
            return;
        }

        if (Session.IsDirty && !await SaveBeforeLeavingAsync())
        {
            return;
        }

        bool recover = false;
        if (Session.HasWorkingCopy(path))
        {
            WorkflowPromptAnswer answer = await Prompt.AskAsync(RecoverPrompt(path));
            if (answer == WorkflowPromptAnswer.Cancel)
            {
                return;
            }

            recover = answer == WorkflowPromptAnswer.Accept;
        }

        OpenDocument.Path = path;
        OpenDocument.Recover = recover;
        await OpenDocument.Command.ExecuteAsync(null);
    }

    /// <summary>
    /// Writes the document, asking for a destination when it has never been saved.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync() => await SaveDocument.Command.ExecuteAsync(null);

    /// <summary>
    /// Asks what to do with the changes the current document still holds, and saves
    /// them when that is the answer. It answers with whether the flow may continue:
    /// a save that did not write the document — because it was refused or because
    /// the destination was dismissed — leaves the changes in place, and continuing
    /// would replace them.
    /// </summary>
    private async Task<bool> SaveBeforeLeavingAsync()
    {
        WorkflowPromptAnswer answer = await Prompt.AskAsync(new WorkflowPrompt(
            $"“{DocumentTitle}” has unsaved changes. Save them before opening another document?",
            "Save",
            "Discard",
            "Cancel"));

        if (answer == WorkflowPromptAnswer.Cancel)
        {
            return false;
        }

        if (answer == WorkflowPromptAnswer.Refuse)
        {
            return true;
        }

        await SaveDocument.Command.ExecuteAsync(null);
        return SaveDocument.Saved;
    }

    /// <summary>
    /// Asks whether to resume the working copy a crash left beside the document.
    /// A working copy holds work the file does not, so it is offered rather than
    /// opened: resuming starts the document with unsaved changes, and opening the
    /// saved file is the other answer.
    /// </summary>
    private static WorkflowPrompt RecoverPrompt(string path)
        => new(
            $"A recoverable working copy of “{System.IO.Path.GetFileName(path)}” was found, and it holds changes the file does not. Resume it?",
            "Resume the working copy",
            "Open the saved file",
            "Cancel");

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(DocumentTitle));
        OnPropertyChanged(nameof(DocumentCounts));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(SelectionConditionSummary));
        OnPropertyChanged(nameof(IsReadOnly));
    }

    /// <summary>
    /// Rebuilds the catalogue from the current search. A search that matches nothing
    /// leaves an empty catalogue rather than the whole catalog, because showing
    /// everything again would read as a search that was ignored.
    /// </summary>
    partial void OnCatalogueSearchChanged(string value)
    {
        CatalogueGroups = Grouped(value);
        OnPropertyChanged(nameof(NodeCatalogSummary));
        OnPropertyChanged(nameof(CatalogueNotice));
    }

    /// <summary>
    /// Groups the types the catalogue offers by the category each definition
    /// declares, in the order the categories sort in and the display names within
    /// them.
    /// </summary>
    /// <param name="search">The text to match, or <see langword="null"/> for everything.</param>
    private IReadOnlyList<ShellCatalogueGroup> Grouped(string? search)
        => [.. CataloguedTypes()
            .Where(definition => Matches(definition, search))
            .GroupBy(definition => definition.Category)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ShellCatalogueGroup(
                group.Key,
                [.. group
                    .Select(definition => new ShellCatalogueEntry(definition.TypeId.Value, definition.DisplayName))
                    .OrderBy(entry => entry.DisplayName, StringComparer.Ordinal)]))];

    /// <summary>
    /// Determines whether a definition answers the search. The name a user reads and
    /// the identifier a document stores are both matched, because a search for
    /// either is a search for the same type.
    /// </summary>
    private static bool Matches(NodeDefinition definition, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        string query = search.Trim();

        return definition.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || definition.TypeId.Value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Counts the types the current search matches, which is one type per catalogue
    /// entry because a category never repeats a type.
    /// </summary>
    private int Matched() => CatalogueGroups.Sum(group => group.Entries.Count);

    /// <summary>
    /// Names the types the catalogue offers: one entry per known type, at the
    /// version the catalog currently publishes, because adding a node places the
    /// latest version and offering the same type twice would imply a choice the
    /// command does not offer.
    /// </summary>
    private IEnumerable<NodeDefinition> CataloguedTypes()
    {
        foreach (NodeTypeId typeId in _catalog.KnownTypeIds.OrderBy(id => id.Value, StringComparer.Ordinal))
        {
            if (_catalog.TryResolveLatest(typeId, out NodeDefinition? definition) && definition is not null)
            {
                yield return definition;
            }
        }
    }

    /// <summary>
    /// Counts what the document holds. A count of one is written as one thing
    /// rather than as a number and a plural, because the summary is read as a
    /// sentence.
    /// </summary>
    private static string Count(int count, string noun)
        => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
