namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// A disposable that records that it was released, and can be told to fail, so a
/// test can prove that the runtime releases a node's private resources and that a
/// failing release is reported without turning a successful node into a failure.
/// </summary>
internal sealed class TrackingDisposable : IDisposable
{
    private readonly Exception? _failure;
    private int _disposeCount;

    /// <summary>
    /// Initializes the disposable.
    /// </summary>
    /// <param name="failure">The exception to throw when released, when the release must fail.</param>
    internal TrackingDisposable(Exception? failure = null) => _failure = failure;

    /// <summary>
    /// Gets the number of times the resource was released.
    /// </summary>
    internal int DisposeCount => Volatile.Read(ref _disposeCount);

    /// <summary>
    /// Gets a value indicating whether the resource was released at least once.
    /// </summary>
    internal bool WasDisposed => DisposeCount > 0;

    /// <inheritdoc />
    public void Dispose()
    {
        Interlocked.Increment(ref _disposeCount);

        if (_failure is not null)
        {
            throw _failure;
        }
    }
}
