using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionWeave.App.Commands;
using VisionWeave.App.Sessions;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Validation;

namespace VisionWeave.App.ViewModels;

/// <summary>
/// Presents the one document the shell currently edits. It reads the session and
/// the catalog but never edits them: every change arrives as an application
/// command, and every string here is derived from session state rather than kept
/// alongside it.
/// </summary>
internal sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly NodeDefinitionCatalog _catalog;
    private readonly IWorkflowFileChooser _fileChooser;

    internal MainWindowViewModel(
        EditorSession session,
        NodeDefinitionCatalog catalog,
        OpenDocumentCommand openDocument,
        IWorkflowFileChooser fileChooser,
        ShellStatus status)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(openDocument);
        ArgumentNullException.ThrowIfNull(fileChooser);
        ArgumentNullException.ThrowIfNull(status);

        Session = session;
        Status = status;
        _catalog = catalog;
        _fileChooser = fileChooser;
        OpenDocument = openDocument;
        CatalogueGroups = [.. catalog.Definitions
            .GroupBy(definition => definition.Category)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ShellCatalogueGroup(
                group.Key,
                [.. group.Select(definition => definition.DisplayName).OrderBy(name => name, StringComparer.Ordinal)]))];

        // The session owns the state and raises its own notifications; the strings
        // below read several of its values at once, so any session change refreshes
        // them together instead of leaving one of them stale.
        Session.PropertyChanged += OnSessionPropertyChanged;
    }

    /// <summary>Gets the command that opens a workflow document into this shell.</summary>
    internal OpenDocumentCommand OpenDocument { get; }

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

    /// <summary>Gets the node catalogue, grouped by the category each definition declares.</summary>
    /// <remarks>
    /// Public because the catalogue region binds to it: WPF binds only to public
    /// members, and a binding to anything else fails silently rather than raising an
    /// error. The type itself stays internal.
    /// </remarks>
    public IReadOnlyList<ShellCatalogueGroup> CatalogueGroups { get; }

    public string NodeCatalogSummary
        => $"{_catalog.Definitions.Count} node types available";

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
    /// Gets the command the shell's document commands call. It asks for a file,
    /// hands the chosen path to the open command, and reports nothing itself: the
    /// session and the command boundary own every outcome.
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

        OpenDocument.Path = path;
        await OpenDocument.Command.ExecuteAsync(null);
    }

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(DocumentTitle));
        OnPropertyChanged(nameof(DocumentCounts));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(SelectionConditionSummary));
    }

    /// <summary>
    /// Counts what the document holds. A count of one is written as one thing
    /// rather than as a number and a plural, because the summary is read as a
    /// sentence.
    /// </summary>
    private static string Count(int count, string noun)
        => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
