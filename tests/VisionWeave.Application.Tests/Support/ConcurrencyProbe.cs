namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// Measures how many bodies ran at the same time, so a test can prove that the
/// run limit was honoured without depending on timing alone.
/// </summary>
internal sealed class ConcurrencyProbe
{
    private readonly object _gate = new();
    private int _active;
    private int _peak;

    /// <summary>
    /// Gets the number of bodies that ran at the same time at most.
    /// </summary>
    internal int Peak
    {
        get
        {
            lock (_gate)
            {
                return _peak;
            }
        }
    }

    /// <summary>
    /// Runs a body while it is counted as active.
    /// </summary>
    /// <typeparam name="TResult">The result of the body.</typeparam>
    /// <param name="body">The body to run.</param>
    /// <returns>The result of the body.</returns>
    internal async Task<TResult> TrackAsync<TResult>(Func<Task<TResult>> body)
    {
        ArgumentNullException.ThrowIfNull(body);

        lock (_gate)
        {
            _active++;
            _peak = Math.Max(_peak, _active);
        }

        try
        {
            return await body().ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _active--;
            }
        }
    }
}
