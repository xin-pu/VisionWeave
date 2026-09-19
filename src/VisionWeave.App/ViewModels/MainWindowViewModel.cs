using CommunityToolkit.Mvvm.ComponentModel;
using VisionWeave.Application.Definitions;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.ViewModels;

/// <summary>
/// Presents the one document the shell currently edits. It reads the session and
/// the catalog but never edits them: every change will arrive as an application
/// command once the canvas exists.
/// </summary>
internal sealed class MainWindowViewModel : ObservableObject
{
    private readonly WorkflowSession _session;
    private readonly NodeDefinitionCatalog _catalog;

    internal MainWindowViewModel(WorkflowSession session, NodeDefinitionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalog);

        _session = session;
        _catalog = catalog;
    }

    public string NodeCatalogSummary
        => $"{_catalog.Definitions.Count} node types available";

    public string WindowTitle
        => $"{DocumentTitle} — VisionWeave";

    public string DocumentTitle
        => _session.Path is null ? _session.Document.Name : System.IO.Path.GetFileName(_session.Path);

    public string DocumentSummary
        => $"{_session.Document.Nodes.Count} nodes, {_session.Document.Connections.Count} connections, {DocumentState}";

    private string DocumentState
        => _session.Path is null
            ? "not saved yet"
            : _session.IsDirty ? "unsaved changes" : "saved";
}
