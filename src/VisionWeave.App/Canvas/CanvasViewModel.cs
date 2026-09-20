using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionWeave.App.Commands;
using VisionWeave.App.Sessions;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Editing;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.App.Canvas;

/// <summary>
/// The canvas: what the document looks like as nodes, ports, and wires, and the
/// gestures that act on it. It creates intent and never edits: every gesture
/// becomes an application command, the document decides whether the edit is legal,
/// and the canvas is then rebuilt from what the document holds. A refused gesture
/// therefore needs no rollback of its own — the projection it was drawn from never
/// changed — and the reason it was refused travels to the status area.
/// </summary>
internal sealed partial class CanvasViewModel : ObservableObject
{
    /// <summary>
    /// How far a node added onto an occupied spot is stepped aside, so a second add
    /// does not hide the first.
    /// </summary>
    private const double PlacementStep = 32;

    /// <summary>
    /// How close two positions count as the same spot. Two nodes may share a place
    /// the user chose, so this only decides where the next added node goes.
    /// </summary>
    private const double PlacementTolerance = 8;

    /// <summary>
    /// Roughly half a drawn node, so an added node lands centred in the view rather
    /// than hanging off the point the viewport reports.
    /// </summary>
    private static readonly CanvasPosition PlacementOffset = new(100, 60);

    private readonly EditorSession _session;
    private readonly NodeDefinitionCatalog _catalog;
    private readonly WorkflowValidator _validator;
    private readonly ShellStatus _status;

    /// <summary>
    /// Guards the push of the session's selection onto the nodes, so a selection the
    /// canvas applied is not read back as a selection the user made.
    /// </summary>
    private bool _applyingSelection;

    /// <summary>
    /// Creates the canvas over the session it presents.
    /// </summary>
    /// <param name="session">The session the document, its revision, and its selection come from.</param>
    /// <param name="catalog">The catalog each node type is resolved against.</param>
    /// <param name="validator">The validator that decides whether a wire is legal.</param>
    /// <param name="status">The shell state a refused gesture is reported to.</param>
    internal CanvasViewModel(
        EditorSession session,
        NodeDefinitionCatalog catalog,
        WorkflowValidator validator,
        ShellStatus status)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(status);

        _session = session;
        _catalog = catalog;
        _validator = validator;
        _status = status;

        Nodes = [];
        Connectors = [];

        // The session owns the document and the selection, so the canvas learns
        // about both from it rather than from its own gestures: an edit made
        // anywhere in the shell, including an undo, redraws this surface.
        _session.PropertyChanged += OnSessionPropertyChanged;

        // A run is not an edit: it changes nothing the document holds, so its report
        // arrives through the status area instead, and what the run did to a node is
        // read from there like every other thing the projection draws.
        _status.PropertyChanged += OnStatusPropertyChanged;

        Rebuild();
    }

    /// <summary>Gets the nodes the canvas draws, ordered as they are drawn.</summary>
    public IReadOnlyList<WorkflowNodeViewModel> Nodes { get; private set; }

    /// <summary>Gets the wires the canvas draws.</summary>
    public IReadOnlyList<WorkflowConnectionViewModel> Connectors { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the document holds no nodes, which is what
    /// the region explains instead of showing an empty surface.
    /// </summary>
    public bool IsEmpty => Nodes.Count == 0;

    /// <summary>
    /// Gets a value indicating whether the surface may be edited. A document this
    /// build cannot write back is shown but not edited: the gesture layer stops
    /// offering the gestures, so a control the user cannot use is disabled rather
    /// than answering with a refusal.
    /// </summary>
    public bool IsEditable => !_session.IsReadOnly;

    /// <summary>
    /// Gets or sets the graph-space point the viewport starts at. The editor owns
    /// this and writes it back, so the canvas can place a node where the user is
    /// looking rather than somewhere off the screen.
    /// </summary>
    public Point ViewportLocation { get; set; }

    /// <summary>Gets or sets how much of the graph the viewport covers.</summary>
    public Size ViewportSize { get; set; }

    /// <summary>
    /// Adds a node of a catalog type, at the middle of what the view shows. The
    /// catalog is the authority on which types exist and at which version, so a type
    /// this build does not hold is refused with the code the rest of the shell uses
    /// for a node whose definition is missing.
    /// </summary>
    /// <param name="typeId">The type identifier the catalog entry names.</param>
    [RelayCommand]
    private void AddNode(string? typeId)
    {
        if (!IsEditable || string.IsNullOrWhiteSpace(typeId))
        {
            return;
        }

        var nodeTypeId = new NodeTypeId(typeId);
        if (!_catalog.TryResolveLatest(nodeTypeId, out NodeDefinition? definition) || definition is null)
        {
            Report(
            [
                new NodeDiagnostic(
                    DiagnosticCodes.MissingNodeDefinition,
                    DiagnosticSeverity.Error,
                    $"No node type {typeId} is available in this build, so nothing was added."),
            ]);
            return;
        }

        Report(_session.Execute(new AddNodeCommand(nodeTypeId, definition.TypeVersion, NextPosition())).Diagnostics);
    }

    /// <summary>
    /// Removes the selected nodes and the wires attached to them. Deleting a
    /// selection is one edit and one undo unit, so the whole selection is one
    /// command and an undo brings all of it back, wires included.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteSelection()
    {
        Guid[] instances = [.. Nodes.Where(node => node.IsSelected).Select(node => node.InstanceId)];
        if (!IsEditable || instances.Length == 0)
        {
            return;
        }

        Report(_session.Execute(new RemoveNodesCommand(instances)).Diagnostics);
    }

    /// <summary>
    /// Connects the two ports a drag joined. Nodify hands over the two connector
    /// data contexts in the order the drag visited them, so a drag that started at
    /// an input is turned around before the document sees it: the document's
    /// convention is that a wire leaves an output and arrives at an input.
    /// Direction, type, multiplicity, and cycle rules stay the validator's, so a
    /// pair that cannot be joined is passed on and refused there.
    /// </summary>
    /// <param name="connection">
    /// The two ports Nodify reports, or anything else. The editor reports them as a
    /// pair, not as the <c>System.Tuple</c> its own documentation names, so the pair
    /// is read as a tuple.
    /// </param>
    [RelayCommand]
    private void Connect(object? connection)
    {
        if (!IsEditable
            || connection is not (object firstConnector, object secondConnector)
            || firstConnector is not PortViewModel first
            || secondConnector is not PortViewModel second)
        {
            // A pending connection is a visual until two ports are named, so
            // anything else is not a gesture the document has an opinion about.
            return;
        }

        (PortViewModel source, PortViewModel target) =
            first.Direction == PortDirection.Output || second.Direction == PortDirection.Input
                ? (first, second)
                : (second, first);

        Report(_session.Execute(new ConnectPortsCommand(
            _validator,
            source.NodeInstanceId,
            source.PortId,
            target.NodeInstanceId,
            target.PortId)).Diagnostics);
    }

    /// <summary>Removes one wire, which is one edit and one undo unit.</summary>
    /// <param name="connector">The wire presentation the gesture named.</param>
    [RelayCommand]
    private void Disconnect(WorkflowConnectionViewModel? connector)
    {
        if (!IsEditable || connector is null)
        {
            return;
        }

        Report(_session.Execute(new DisconnectPortsCommand(connector.ConnectionId)).Diagnostics);
    }

    /// <summary>
    /// Commits the positions the user dragged nodes to. The jump from a completed
    /// drag to a committed position is the one place the canvas decides anything:
    /// each dragged position is rounded to a whole graph-space unit, which is the
    /// quantization the design allows and keeps a document free of the sub-pixel
    /// noise a pointer produces, and a drag that ended where it started differs
    /// nowhere, so it is not an edit and not an undo step.
    /// </summary>
    [RelayCommand]
    private void Move()
    {
        if (!IsEditable)
        {
            return;
        }

        List<(Guid InstanceId, CanvasPosition Position)> moves = [];
        foreach (WorkflowNodeViewModel node in Nodes)
        {
            if (_session.Document.TryGetNode(node.InstanceId, out NodeInstance? instance)
                && instance is not null
                && instance.Position != node.SnappedPosition)
            {
                moves.Add((node.InstanceId, node.SnappedPosition));
            }
        }

        if (moves.Count == 0)
        {
            return;
        }

        Report(_session.Execute(new MoveNodesCommand(moves)).Diagnostics);
    }

    private bool HasSelection() => IsEditable && Nodes.Any(node => node.IsSelected);

    /// <summary>
    /// Reverses the most recent edit. The session owns the undo stack and the
    /// projection is rebuilt from what the reversal leaves behind, so an undone
    /// node, wire, or move disappears from the canvas without the canvas tracking
    /// what changed.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Report(_session.Undo().Diagnostics);

    /// <summary>Reapplies the most recently undone edit.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Report(_session.Redo().Diagnostics);

    private bool CanUndo() => _session.CanUndo;

    private bool CanRedo() => _session.CanRedo;

    /// <summary>
    /// Rebuilds the canvas from committed state. The projection is replaced whole
    /// rather than patched, so no node outlives the document that produced it, and
    /// the pan and zoom survive because they belong to the editor rather than to
    /// this projection.
    /// </summary>
    private void Rebuild()
    {
        CanvasProjectedDocument projected = CanvasProjection.Project(
            _session.Document,
            _catalog,
            _session.Projection,
            _session.Selection,
            _status.LastRun);

        foreach (WorkflowNodeViewModel node in projected.Nodes)
        {
            node.PropertyChanged += OnNodePropertyChanged;
        }

        Nodes = projected.Nodes;
        Connectors = projected.Connectors;

        OnPropertyChanged(nameof(Nodes));
        OnPropertyChanged(nameof(Connectors));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsEditable));
        DeleteSelectionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Pushes the session's selection onto the nodes. The session may name an
    /// instance the document no longer holds, which marks nothing, and the guard
    /// keeps this push from being read back as a selection the user made.
    /// </summary>
    private void ApplySelection()
    {
        HashSet<Guid> selected = [.. _session.Selection];

        _applyingSelection = true;
        try
        {
            foreach (WorkflowNodeViewModel node in Nodes)
            {
                node.IsSelected = selected.Contains(node.InstanceId);
            }
        }
        finally
        {
            _applyingSelection = false;
        }

        DeleteSelectionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Turns a node's own selected flag into the session's selection. A container
    /// control writes that flag while the user selects, so this is where a click or
    /// a box selection becomes the one selection the shell reports.
    /// </summary>
    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_applyingSelection || e.PropertyName != nameof(WorkflowNodeViewModel.IsSelected))
        {
            return;
        }

        _session.Select(Nodes.Where(node => node.IsSelected).Select(node => node.InstanceId));
        DeleteSelectionCommand.NotifyCanExecuteChanged();
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EditorSession.Projection):
            case nameof(EditorSession.Document):
                // An accepted edit replaces the projection, and a new or opened
                // document replaces both, so these are the two moments the canvas
                // has to redraw.
                Rebuild();
                break;
            case nameof(EditorSession.Selection):
                ApplySelection();
                break;
            case nameof(EditorSession.CanUndo):
                UndoCommand.NotifyCanExecuteChanged();
                break;
            case nameof(EditorSession.CanRedo):
                RedoCommand.NotifyCanExecuteChanged();
                break;
        }
    }

    /// <summary>
    /// Redraws when a run reports what it did. The marks of the run before it were
    /// cleared when the new run started, and the summary that replaces them marks
    /// nothing unless it describes the document and revision on screen.
    /// </summary>
    private void OnStatusPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellStatus.LastRun))
        {
            Rebuild();
        }
    }

    /// <summary>
    /// Picks where a node added from the catalog lands: the middle of what the user
    /// can see, stepped aside while that spot is taken. The editor reports its
    /// viewport once it has been laid out, so before that the first node starts at
    /// the origin.
    /// </summary>
    private CanvasPosition NextPosition()
    {
        CanvasPosition position = ViewportSize.Width > 0 && ViewportSize.Height > 0
            ? new CanvasPosition(
                (ViewportLocation.X + (ViewportSize.Width / 2)) - PlacementOffset.X,
                (ViewportLocation.Y + (ViewportSize.Height / 2)) - PlacementOffset.Y)
            : new CanvasPosition(0, 0);

        while (IsTaken(position))
        {
            position = position with { X = position.X + PlacementStep };
        }

        return new CanvasPosition(Math.Round(position.X), Math.Round(position.Y));
    }

    private bool IsTaken(CanvasPosition position)
        => Nodes.Any(node =>
            Math.Abs(node.Position.X - position.X) < PlacementTolerance
            && Math.Abs(node.Position.Y - position.Y) < PlacementTolerance);

    /// <summary>
    /// Reports what an edit observed. An edit is observed rather than awaited: it
    /// always finishes, and a refusal travels as the diagnostics it produced rather
    /// than as a completion of its own, so the status area reads the same way
    /// whether a gesture changed the document or was turned down.
    /// </summary>
    private void Report(IReadOnlyList<NodeDiagnostic> diagnostics)
        => _status.Report(new CommandExecutionResult(CommandCompletion.Completed, diagnostics));
}
