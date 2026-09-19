using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using VisionWeave.App.Sessions;

namespace VisionWeave.App.Preview;

/// <summary>
/// The managed preview the shell draws: the newest image a run published, the node that
/// published it, and its size and pixel layout. It holds a copy rather than a frame, so
/// the image outlives the run, and it forgets the image when the document is replaced.
/// </summary>
internal sealed partial class PreviewViewModel : ObservableObject
{
    /// <summary>What the shell shows before anything has run.</summary>
    internal const string NoPreviewText = "Run the workflow to see the newest image a node publishes.";

    private readonly EditorSession _session;

    /// <summary>
    /// Creates the preview over the session it follows.
    /// </summary>
    /// <remarks>
    /// The constructor is public because the host's service container resolves this
    /// type and only considers public constructors; the type itself stays internal.
    /// </remarks>
    public PreviewViewModel(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;

        Clear();

        _session.PropertyChanged += OnSessionPropertyChanged;
    }

    /// <summary>Gets the bitmap the region draws, or <see langword="null"/> while no run has produced one.</summary>
    [ObservableProperty]
    public partial BitmapSource? Image { get; private set; }

    /// <summary>Gets the name of the node that published the newest image.</summary>
    [ObservableProperty]
    public partial string PreviewTitle { get; private set; }

    /// <summary>Gets the size and pixel layout of the newest image.</summary>
    [ObservableProperty]
    public partial string PreviewDetail { get; private set; }

    /// <summary>Gets a value indicating whether a run has produced an image to show.</summary>
    [ObservableProperty]
    public partial bool HasPreview { get; private set; }

    /// <summary>Gets what the region says while it holds no image.</summary>
    public string PreviewNotice => HasPreview ? string.Empty : NoPreviewText;

    internal void Show(RunPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        // The whole value is replaced at once: a title from one run beside a size from
        // another would describe a frame that never existed.
        Image = preview.Image;
        PreviewTitle = preview.NodeTitle;
        PreviewDetail = $"{preview.Width} × {preview.Height} pixels, {preview.PixelFormat}";
        HasPreview = true;
        OnPropertyChanged(nameof(PreviewNotice));
    }

    internal void Clear()
    {
        Image = null;
        PreviewTitle = string.Empty;
        PreviewDetail = string.Empty;
        HasPreview = false;
        OnPropertyChanged(nameof(PreviewNotice));
    }

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorSession.Session))
        {
            Clear();
        }
    }
}
