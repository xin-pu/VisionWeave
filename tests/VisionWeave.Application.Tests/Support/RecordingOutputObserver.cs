using VisionWeave.Application.Execution;

namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// An output observer a test drives, which records every published output so the
/// test can assert what a preview renderer would have seen and when.
/// </summary>
internal sealed class RecordingOutputObserver : IExecutionOutputObserver
{
    private readonly List<NodeOutputs> _observed = [];
    private readonly Func<NodeOutputs, CancellationToken, Task> _observe;
    private int _invocations;

    /// <summary>
    /// Initializes the observer.
    /// </summary>
    /// <param name="observe">The delegate that reacts to a published output.</param>
    internal RecordingOutputObserver(Func<NodeOutputs, CancellationToken, Task> observe) => _observe = observe;

    /// <summary>
    /// Gets the number of outputs observed.
    /// </summary>
    internal int Invocations => Volatile.Read(ref _invocations);

    /// <summary>
    /// Gets the outputs observed so far.
    /// </summary>
    internal IReadOnlyList<NodeOutputs> Observed
    {
        get
        {
            lock (_observed)
            {
                return [.. _observed];
            }
        }
    }

    /// <inheritdoc />
    public async Task ObserveAsync(NodeOutputs outputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outputs);

        Interlocked.Increment(ref _invocations);

        lock (_observed)
        {
            _observed.Add(outputs);
        }

        await _observe(outputs, cancellationToken).ConfigureAwait(false);
    }
}
