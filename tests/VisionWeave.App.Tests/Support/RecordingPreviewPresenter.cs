using VisionWeave.App.Preview;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// Keeps every preview a run published, so a test asserts what the shell would have
/// drawn without opening a window. A run presents from the thread it executes on,
/// so the list is entered under a gate rather than assumed to be the test's own.
/// </summary>
internal sealed class RecordingPreviewPresenter : IRunPreviewPresenter
{
    private readonly List<RunPreview> _presented = [];
    private readonly object _gate = new();

    /// <summary>Gets the previews that were published, in order.</summary>
    internal IReadOnlyList<RunPreview> Presented
    {
        get
        {
            lock (_gate)
            {
                return [.. _presented];
            }
        }
    }

    /// <inheritdoc />
    public Task PresentAsync(RunPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        lock (_gate)
        {
            _presented.Add(preview);
        }

        return Task.CompletedTask;
    }
}
