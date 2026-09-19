using System.ComponentModel;
using VisionWeave.App.Commands;
using VisionWeave.App.Sessions;
using VisionWeave.Application.Definitions;

namespace VisionWeave.App.ViewModels;

/// <summary>
/// Presents the one document the shell currently edits. It reads the session and
/// the catalog but never edits them: every change arrives as an application
/// command, and every string here is derived from session state rather than kept
/// alongside it.
/// </summary>
internal sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly NodeDefinitionCatalog _catalog;

    internal MainWindowViewModel(EditorSession session, NodeDefinitionCatalog catalog, OpenDocumentCommand openDocument)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(openDocument);

        Session = session;
        _catalog = catalog;
        OpenDocument = openDocument;

        // The session owns the state and raises its own notifications; the strings
        // below read several of its values at once, so any session change refreshes
        // them together instead of leaving one of them stale.
        Session.PropertyChanged += OnSessionPropertyChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the command that opens a workflow document into this shell.</summary>
    internal OpenDocumentCommand OpenDocument { get; }

    /// <summary>
    /// Gets the editing session this shell presents. The session replaces the
    /// document inside itself, so the presentation keeps reading one authoritative
    /// object and never has to rebuild its own state.
    /// </summary>
    internal EditorSession Session { get; }

    public string NodeCatalogSummary
        => $"{_catalog.Definitions.Count} node types available";

    public string WindowTitle
        => $"{DocumentTitle} — VisionWeave";

    public string DocumentTitle
        => Session.Path is null ? Session.Document.Name : System.IO.Path.GetFileName(Session.Path);

    public string DocumentSummary
        => $"{Session.Document.Nodes.Count} nodes, {Session.Document.Connections.Count} connections, {DocumentState}";

    private string DocumentState
        => Session.Path is null
            ? "not saved yet"
            : Session.IsDirty ? "unsaved changes" : "saved";

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WindowTitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DocumentTitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DocumentSummary)));
    }
}
