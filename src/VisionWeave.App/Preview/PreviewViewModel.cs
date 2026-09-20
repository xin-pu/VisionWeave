using System.ComponentModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using VisionWeave.App.Sessions;

namespace VisionWeave.App.Preview;

/// <summary>
/// Holds the image outputs from the newest run and follows the editor selection, so
/// selecting a node shows that node's output rather than whichever node ran last.
/// </summary>
internal sealed partial class PreviewViewModel : ObservableObject
{
    /// <summary>What the shell shows before anything has run.</summary>
    internal const string NoPreviewText = "Run the workflow, then select a node to inspect its image output.";

    /// <summary>What the shell shows when the selected node has no image output.</summary>
    internal const string SelectedNodeHasNoPreviewText =
        "The selected node did not publish an image in the newest run.";

    private readonly EditorSession _session;
    private readonly Dictionary<Guid, RunPreview> _nodePreviews = [];
    private Guid? _operationId;
    private RunPreview? _newestPreview;
    private bool _selectionHasNoPreview;

    /// <summary>Creates the preview over the session it follows.</summary>
    /// <param name="session">The session whose selected node decides which output is visible.</param>
    public PreviewViewModel(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        Clear();
        _session.PropertyChanged += OnSessionPropertyChanged;
    }

    /// <summary>Gets the bitmap the region draws.</summary>
    [ObservableProperty]
    public partial BitmapSource? Image { get; private set; }

    /// <summary>Gets the name of the node that published the visible image.</summary>
    [ObservableProperty]
    public partial string PreviewTitle { get; private set; }

    /// <summary>Gets the size and pixel layout of the visible image.</summary>
    [ObservableProperty]
    public partial string PreviewDetail { get; private set; }

    /// <summary>Gets a value indicating whether an image is visible.</summary>
    [ObservableProperty]
    public partial bool HasPreview { get; private set; }

    /// <summary>Gets the explanation shown while no image is visible.</summary>
    public string PreviewNotice
        => HasPreview ? string.Empty
        : _selectionHasNoPreview ? SelectedNodeHasNoPreviewText
        : NoPreviewText;

    internal void Show(RunPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (_operationId != preview.OperationId)
        {
            _operationId = preview.OperationId;
            _nodePreviews.Clear();
            Clear();
        }

        _nodePreviews[preview.NodeInstanceId] = preview;
        _newestPreview = preview;
        Guid? selected = SelectedNode();
        if (selected is null || selected == preview.NodeInstanceId)
        {
            Display(preview);
        }
    }

    internal void Clear()
    {
        _selectionHasNoPreview = false;
        Image = null;
        PreviewTitle = string.Empty;
        PreviewDetail = string.Empty;
        HasPreview = false;
        OnPropertyChanged(nameof(PreviewNotice));
    }

    private void Display(RunPreview preview)
    {
        _selectionHasNoPreview = false;
        Image = preview.Image;
        PreviewTitle = preview.NodeTitle;
        PreviewDetail = $"{preview.Width} × {preview.Height} pixels, {preview.PixelFormat}";
        HasPreview = true;
        OnPropertyChanged(nameof(PreviewNotice));
    }

    private Guid? SelectedNode()
        => _session.Selection.Count == 1 ? _session.Selection[0] : null;

    private void FollowSelection()
    {
        Guid? selected = SelectedNode();
        if (selected is null)
        {
            if (_newestPreview is { } newest)
            {
                Display(newest);
            }
            else
            {
                Clear();
            }

            return;
        }

        if (_nodePreviews.TryGetValue(selected.Value, out RunPreview? preview))
        {
            Display(preview);
            return;
        }

        Clear();
        _selectionHasNoPreview = true;
        OnPropertyChanged(nameof(PreviewNotice));
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorSession.Session))
        {
            _operationId = null;
            _nodePreviews.Clear();
            _newestPreview = null;
            Clear();
        }
        else if (e.PropertyName == nameof(EditorSession.Selection))
        {
            FollowSelection();
        }
    }
}
