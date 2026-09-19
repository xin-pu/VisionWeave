using VisionWeave.Contracts.Execution;

namespace VisionWeave.IntegrationTests.Support;

/// <summary>
/// Stands in for the execution-scoped resource scope the runtime hands to an
/// executor. It records what was registered and how often disposal ran, so a test
/// can tell a resource the executor owns from a buffer it transferred to the
/// value it returned.
/// </summary>
internal sealed class TestResourceScope : IExecutionResourceScope
{
    private readonly object _gate = new();
    private readonly List<IDisposable> _owned = [];

    /// <summary>
    /// Gets the number of times the scope was disposed.
    /// </summary>
    internal int DisposalCount { get; private set; }

    /// <summary>
    /// Gets the resources the executor registered.
    /// </summary>
    internal IReadOnlyList<IDisposable> Owned
    {
        get
        {
            lock (_gate)
            {
                return [.. _owned];
            }
        }
    }

    /// <inheritdoc />
    public void Own(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        lock (_gate)
        {
            _owned.Add(resource);
        }
    }

    /// <summary>
    /// Disposes every registered resource once.
    /// </summary>
    internal void DisposeAll()
    {
        IDisposable[] owned;

        lock (_gate)
        {
            owned = [.. _owned];
            _owned.Clear();
            DisposalCount++;
        }

        foreach (IDisposable resource in owned)
        {
            resource.Dispose();
        }
    }
}
