using System.Windows.Threading;

namespace VisionWeave.App.Preview;

/// <summary>
/// Hands the images a node published to the shell on the thread the window's bindings
/// belong to. The run converts frames off that thread, and a notification raised
/// anywhere else is one a binding refuses to apply, so the hand-off is marshalled and
/// the caller waits for it — which keeps the run's completion ordered after the previews
/// it produced.
/// </summary>
internal sealed class ShellRunPreviewPresenter : IRunPreviewPresenter
{
    private readonly PreviewViewModel _preview;
    private readonly Dispatcher _dispatcher;

    internal ShellRunPreviewPresenter(PreviewViewModel preview, Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _preview = preview;
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public async Task PresentAsync(IReadOnlyList<RunPreview> previews)
    {
        ArgumentNullException.ThrowIfNull(previews);

        if (_dispatcher.CheckAccess())
        {
            // Already on the thread that owns the bindings; posting to it would only
            // make the caller wait for work it could do here.
            _preview.Show(previews);
            return;
        }

        await _dispatcher.InvokeAsync(() => _preview.Show(previews));
    }
}
