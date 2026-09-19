using CommunityToolkit.Mvvm.ComponentModel;
using VisionWeave.App.Commands;
using VisionWeave.Application.Definitions;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.ViewModels;

/// <summary>
/// Presents the one document the shell currently edits. It reads the session and
/// the catalog but never edits them: every change will arrive as an application
/// command once the canvas exists.
/// </summary>
internal sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly NodeDefinitionCatalog _catalog;

    internal MainWindowViewModel(WorkflowSession session, NodeDefinitionCatalog catalog, OpenDocumentCommand openDocument)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(openDocument);

        Session = session;
        _catalog = catalog;
        OpenDocument = openDocument;
        OpenDocument.Opened += OnDocumentOpened;
    }

    /// <summary>Gets the command that opens a workflow document into this shell.</summary>
    internal OpenDocumentCommand OpenDocument { get; }

    /// <summary>
    /// Gets or sets the document this shell presents. An opened document replaces
    /// the session rather than editing it, so the presentation keeps reading one
    /// authoritative object.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(DocumentTitle))]
    [NotifyPropertyChangedFor(nameof(DocumentSummary))]
    internal partial WorkflowSession Session { get; set; }

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

    private void OnDocumentOpened(object? sender, WorkflowSession session) => Session = session;
}
