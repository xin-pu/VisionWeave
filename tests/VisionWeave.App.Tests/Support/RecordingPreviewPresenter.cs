using VisionWeave.App.Preview;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// Keeps every preview a run published, per node and flattened in the order they were
/// shown, so a test asserts what the shell would have drawn without opening a window. A
/// run presents from the thread it executes on, so both lists are entered under a gate
/// rather than assumed to be the test's own.
/// </summary>
internal sealed class RecordingPreviewPresenter : IRunPreviewPresenter
{
    private readonly List<RunPreview> _presented = [];
    private readonly List<IReadOnlyList<RunPreview>> _sets = [];
    private readonly object _gate = new();

    /// <summary>Gets the previews that were published, in order, flattened across the nodes that published them.</summary>
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

    /// <summary>Gets the images of every node that published any, one entry per node, in the order the nodes ran.</summary>
    internal IReadOnlyList<IReadOnlyList<RunPreview>> Sets
    {
        get
        {
            lock (_gate)
            {
                return [.. _sets];
            }
        }
    }

    /// <inheritdoc />
    public Task PresentAsync(IReadOnlyList<RunPreview> previews)
    {
        ArgumentNullException.ThrowIfNull(previews);

        lock (_gate)
        {
            _sets.Add([.. previews]);
            _presented.AddRange(previews);
        }

        return Task.CompletedTask;
    }
}
