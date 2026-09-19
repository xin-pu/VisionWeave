using CommunityToolkit.Mvvm.ComponentModel;
using VisionWeave.App.Commands;
using VisionWeave.App.Presentation;
using VisionWeave.App.Sessions;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Editing;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.App.Inspector;

/// <summary>
/// The inspector: what the selection is, the parameters its definition declares,
/// and the conditions the selection carries. It creates intent and never edits —
/// a committed field becomes a <see cref="SetNodeParameterCommand"/> and the
/// document decides whether it is legal — and it derives every string from the
/// session, so nothing here outlives the revision it described.
/// </summary>
internal sealed partial class InspectorViewModel : ObservableObject
{
    private readonly EditorSession _session;
    private readonly NodeDefinitionCatalog _catalog;
    private readonly ShellStatus _status;

    /// <summary>
    /// The node the current parameter list was built for. A list is kept while its
    /// owner and its schema stay the same, because rebuilding it on every accepted
    /// edit would replace the field the user is typing in.
    /// </summary>
    private (Guid InstanceId, string TypeId, int Version)? _parameterOwner;

    /// <summary>
    /// The field whose committed value is being applied, so its text is not
    /// rewritten from the document under the caret that is still in it.
    /// </summary>
    private ParameterEditorViewModel? _editing;

    /// <summary>
    /// Creates the inspector over the session it presents.
    /// </summary>
    /// <param name="session">The session the document, the selection, and the projection come from.</param>
    /// <param name="catalog">The catalog each selected node's definition is resolved against.</param>
    /// <param name="status">The shell state a refused edit is reported to.</param>
    internal InspectorViewModel(EditorSession session, NodeDefinitionCatalog catalog, ShellStatus status)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(status);

        _session = session;
        _catalog = catalog;
        _status = status;

        NodeTitle = string.Empty;
        NodeCaption = string.Empty;
        ParameterNote = string.Empty;
        DiagnosticCountText = string.Empty;
        Parameters = [];
        Diagnostics = [];

        _session.PropertyChanged += OnSessionPropertyChanged;

        Rebuild();
    }

    /// <summary>
    /// Gets the parameters the selected node's definition declares, each with the
    /// value the document holds or the default it falls back to.
    /// </summary>
    public IReadOnlyList<ParameterEditorViewModel> Parameters { get; private set; }

    /// <summary>
    /// Gets the conditions the selection carries, which are the document's own
    /// conditions followed by the selected nodes' ones.
    /// </summary>
    public IReadOnlyList<DiagnosticEntryViewModel> Diagnostics { get; private set; }

    /// <summary>Gets what the selection is: the selected node's title, or the count of them.</summary>
    public string NodeTitle { get; private set; }

    /// <summary>Gets the line under the title: the type the node was saved as, or what to do.</summary>
    public string NodeCaption { get; private set; }

    /// <summary>
    /// Gets why no parameters are offered, when the selection is one node this
    /// build cannot describe. An empty string when the parameters speak for
    /// themselves.
    /// </summary>
    public string ParameterNote { get; private set; }

    /// <summary>Gets how many conditions the selection carries.</summary>
    public string DiagnosticCountText { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the selection can be edited here: one node
    /// whose definition this build holds, in a document that may be written.
    /// </summary>
    public bool IsEditable { get; private set; }

    /// <summary>
    /// Applies the value the user committed in one field. It is reached through the
    /// field's own commit, which is the only thing that knows what the user did —
    /// Enter for text, the change itself for a switch and an option — so there is no
    /// command for the shell to invoke on a field that committed nothing.
    /// </summary>
    /// <param name="editor">The field that was committed.</param>
    private void EditParameter(ParameterEditorViewModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (_parameterOwner is not { } owner || !IsEditable)
        {
            return;
        }

        if (!editor.TryReadValue(out object? value, out NodeDiagnostic? refusal))
        {
            // The field holds nothing the definition can accept, so the document is
            // never asked: the refusal is shown on the field and reported where the
            // shell reports conditions.
            NodeDiagnostic refused = refusal!;

            editor.Refuse(refused);
            Report([refused]);
            return;
        }

        _editing = editor;
        try
        {
            DocumentCommandResult result = _session.Execute(
                new SetNodeParameterCommand(owner.InstanceId, editor.Name, value));

            // An accepted edit reports nothing of its own, and the condition a value
            // earned is a validation result rather than a command outcome, so the
            // field's own conditions are what the status area names next.
            Report(result.Diagnostics.Count > 0
                ? result.Diagnostics
                : _session.Projection.DiagnosticsForParameter(owner.InstanceId, editor.Name));
        }
        finally
        {
            _editing = null;
        }
    }

    /// <summary>
    /// Reads the selected node, which is the one node a selection has to be for the
    /// inspector to offer its parameters.
    /// </summary>
    private (NodeInstance? Instance, NodeDefinition? Definition) SelectedNode()
    {
        if (_session.Selection.Count != 1
            || !_session.Document.TryGetNode(_session.Selection[0], out NodeInstance? instance)
            || instance is null)
        {
            return (null, null);
        }

        _catalog.TryResolve(instance.NodeTypeId, instance.TypeVersion, out NodeDefinition? definition);
        return (instance, definition);
    }

    /// <summary>
    /// Presents the selected node's parameters. A list already built for the same
    /// node and the same schema is re-read rather than rebuilt, so the field being
    /// typed in keeps its caret and every other field still follows the document.
    /// </summary>
    private IReadOnlyList<ParameterEditorViewModel> ParametersFor(NodeInstance? instance, NodeDefinition? definition)
    {
        if (instance is null || definition is null)
        {
            _parameterOwner = null;
            return [];
        }

        var owner = (instance.InstanceId, definition.TypeId.Value, definition.TypeVersion);

        if (_parameterOwner == owner)
        {
            foreach (ParameterEditorViewModel editor in Parameters)
            {
                editor.Commit = EditParameter;
                editor.Refresh(
                    StoredValue(instance, editor.Name),
                    _session.Projection.SeverityOfParameter(instance.InstanceId, editor.Name),
                    Condition(instance.InstanceId, editor.Name),
                    refreshValue: !ReferenceEquals(editor, _editing));
            }

            return Parameters;
        }

        _parameterOwner = owner;

        return
        [
            .. definition.Parameters.Select(parameter =>
            {
                var editor = new ParameterEditorViewModel(
                    parameter,
                    StoredValue(instance, parameter.Name),
                    _session.Projection.SeverityOfParameter(instance.InstanceId, parameter.Name),
                    Condition(instance.InstanceId, parameter.Name));

                // The field asks; the inspector turns what it asks into a document
                // command, so no field holds a rule about parameters.
                editor.Commit = EditParameter;
                return editor;
            }),
        ];
    }

    /// <summary>
    /// Reads the value the document holds for a parameter, or nothing when it holds
    /// none, which is what makes the field fall back to the declared default.
    /// </summary>
    private static object? StoredValue(NodeInstance instance, string parameterName)
        => instance.Parameters.TryGetValue(parameterName, out object? value) ? value : null;

    /// <summary>
    /// Words the worst condition a parameter carries, or nothing when it carries
    /// none. The projection answers for one revision, so this is what the validator
    /// says about the parameter now rather than a mark left by an earlier edit.
    /// </summary>
    private string Condition(Guid instanceId, string parameterName)
    {
        IReadOnlyList<NodeDiagnostic> diagnostics = _session.Projection.DiagnosticsForParameter(instanceId, parameterName);

        return diagnostics.Count == 0 ? string.Empty : DiagnosticText.Of(diagnostics[0]);
    }

    /// <summary>
    /// Rebuilds the inspector from committed state. The projection, the selection,
    /// and the document are the three things it reads, so an edit made anywhere in
    /// the shell — including an undo — refreshes every line here.
    /// </summary>
    private void Rebuild()
    {
        (NodeInstance? instance, NodeDefinition? definition) = SelectedNode();

        Parameters = ParametersFor(instance, definition);
        Diagnostics =
        [
            .. _session.SelectionDiagnostics.Diagnostics.Select(
                diagnostic => new DiagnosticEntryViewModel(diagnostic)),
        ];

        IsEditable = !_session.IsReadOnly && Parameters.Count > 0;
        NodeTitle = instance is null ? SelectionText() : NodeText.Title(instance, definition);
        NodeCaption = instance is null ? SelectionText() + "." : NodeText.Caption(instance, definition);
        ParameterNote = NoteFor(instance, definition);
        DiagnosticCountText = Diagnostics.Count switch
        {
            0 => "No conditions",
            1 => "1 condition",
            _ => $"{Diagnostics.Count} conditions",
        };

        OnPropertyChanged(nameof(Parameters));
        OnPropertyChanged(nameof(Diagnostics));
        OnPropertyChanged(nameof(NodeTitle));
        OnPropertyChanged(nameof(NodeCaption));
        OnPropertyChanged(nameof(ParameterNote));
        OnPropertyChanged(nameof(DiagnosticCountText));
        OnPropertyChanged(nameof(IsEditable));
    }

    /// <summary>
    /// Names the selection when it is not one node. Editing a parameter needs the
    /// single node the parameter belongs to, so the line says what to do rather than
    /// presenting fields that would belong to a node the user has not chosen.
    /// </summary>
    private string SelectionText()
        => _session.Selection.Count switch
        {
            0 => "Nothing is selected",
            1 => "The selected node",
            _ => $"{_session.Selection.Count} nodes are selected",
        };

    /// <summary>
    /// Explains an empty parameter list. A node whose definition this build does not
    /// hold cannot be described at all, and a definition that declares no parameters
    /// is a fact about the node rather than an empty panel.
    /// </summary>
    private static string NoteFor(NodeInstance? instance, NodeDefinition? definition)
    {
        if (instance is null)
        {
            return string.Empty;
        }

        if (definition is null)
        {
            return "This build does not provide this node type, so its parameters cannot be shown or edited.";
        }

        return definition.Parameters.Count == 0 ? "This node type declares no parameters." : string.Empty;
    }

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EditorSession.Document):
            case nameof(EditorSession.Projection):
            case nameof(EditorSession.Selection):
            case nameof(EditorSession.Session):
                Rebuild();
                break;
        }
    }

    /// <summary>
    /// Reports what an edit observed, so a refusal and a condition travel to the
    /// same place the canvas reports its gestures to.
    /// </summary>
    private void Report(IReadOnlyList<NodeDiagnostic> diagnostics)
        => _status.Report(new CommandExecutionResult(CommandCompletion.Completed, diagnostics));
}
