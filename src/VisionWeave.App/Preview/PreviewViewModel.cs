using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VisionWeave.App.Sessions;

namespace VisionWeave.App.Preview;

/// <summary>
/// Holds the image outputs from the newest run and follows the editor selection, so
/// selecting a node shows that node's output rather than whichever node ran last. It
/// keeps every image of that node, because a node that splits an image into channels
/// published all of them at once and the shell is where they are compared.
/// </summary>
internal sealed partial class PreviewViewModel : ObservableObject
{
    /// <summary>What the shell shows before anything has run.</summary>
    internal const string NoPreviewText = "Run the workflow, then select a node to inspect its image output.";

    /// <summary>What the shell shows when the selected node has no image output.</summary>
    internal const string SelectedNodeHasNoPreviewText =
        "The selected node did not publish an image in the newest run.";

    private readonly EditorSession _session;
    private readonly Dictionary<Guid, IReadOnlyList<RunPreview>> _nodePreviews = [];
    private Guid? _operationId;
    private IReadOnlyList<RunPreview>? _newestPreviews;
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

    /// <summary>Gets the images the region draws, in the order the node declared them.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PreviewImage> Images { get; private set; }

    /// <summary>Gets the name of the node that published the visible images.</summary>
    [ObservableProperty]
    public partial string PreviewTitle { get; private set; }

    /// <summary>Gets how many images are visible and what shape and layout they arrived with.</summary>
    [ObservableProperty]
    public partial string PreviewDetail { get; private set; }

    /// <summary>Gets a value indicating whether any image is visible.</summary>
    [ObservableProperty]
    public partial bool HasPreview { get; private set; }

    /// <summary>Gets the explanation shown while no image is visible.</summary>
    public string PreviewNotice
        => HasPreview ? string.Empty
        : _selectionHasNoPreview ? SelectedNodeHasNoPreviewText
        : NoPreviewText;

    internal void Show(IReadOnlyList<RunPreview> previews)
    {
        ArgumentNullException.ThrowIfNull(previews);

        if (previews.Count == 0)
        {
            return;
        }

        RunPreview first = previews[0];
        if (_operationId != first.OperationId)
        {
            _operationId = first.OperationId;
            _nodePreviews.Clear();
            Clear();
        }

        _nodePreviews[first.NodeInstanceId] = previews;
        _newestPreviews = previews;
        Guid? selected = SelectedNode();
        if (selected is null || selected == first.NodeInstanceId)
        {
            Display(previews);
        }
    }

    internal void Clear()
    {
        _selectionHasNoPreview = false;
        Images = [];
        PreviewTitle = string.Empty;
        PreviewDetail = string.Empty;
        HasPreview = false;
        OnPropertyChanged(nameof(PreviewNotice));
    }

    private void Display(IReadOnlyList<RunPreview> previews)
    {
        _selectionHasNoPreview = false;
        Images = [.. previews.Select(preview => new PreviewImage(preview.Image, Caption(preview, previews), Label(preview, previews)))];
        PreviewTitle = previews[0].NodeTitle;
        PreviewDetail = Detail(previews);
        HasPreview = true;
        OnPropertyChanged(nameof(PreviewNotice));
    }

    /// <summary>
    /// What the viewer calls one image of a node. The node names the image when it
    /// published one, and the output names it when the node published several: a window
    /// full of "Split Channels" would say which node but not which channel.
    /// </summary>
    private static string Caption(RunPreview preview, IReadOnlyList<RunPreview> previews)
        => previews.Count > 1 ? $"{preview.NodeTitle} — {preview.PortTitle}" : preview.NodeTitle;

    /// <summary>The name drawn over the tile, which only a node with several images needs.</summary>
    private static string Label(RunPreview preview, IReadOnlyList<RunPreview> previews)
        => previews.Count > 1 ? preview.PortTitle : string.Empty;

    /// <summary>
    /// How many images, and — while they all arrived with the same shape and layout —
    /// what that shape was, so the one line stays as informative as the single-image
    /// case without growing a row per image.
    /// </summary>
    private static string Detail(IReadOnlyList<RunPreview> previews)
    {
        RunPreview first = previews[0];
        if (previews.Count == 1)
        {
            return $"{first.Width} × {first.Height} pixels, {first.PixelFormat}";
        }

        bool alike = previews.All(preview => preview.Width == first.Width
            && preview.Height == first.Height
            && preview.PixelFormat == first.PixelFormat);

        return alike
            ? $"{previews.Count} images, {first.Width} × {first.Height} pixels, {first.PixelFormat}"
            : $"{previews.Count} images";
    }

    private Guid? SelectedNode()
        => _session.Selection.Count == 1 ? _session.Selection[0] : null;

    private void FollowSelection()
    {
        Guid? selected = SelectedNode();
        if (selected is null)
        {
            if (_newestPreviews is { } newest)
            {
                Display(newest);
            }
            else
            {
                Clear();
            }

            return;
        }

        if (_nodePreviews.TryGetValue(selected.Value, out IReadOnlyList<RunPreview>? previews))
        {
            Display(previews);
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
            _newestPreviews = null;
            Clear();
        }
        else if (e.PropertyName == nameof(EditorSession.Selection))
        {
            FollowSelection();
        }
    }
}
