namespace VisionWeave.Application.Execution;

/// <summary>
/// The limits one run obeys. They are explicit so that a test can pin them
/// instead of depending on the machine the run happens to execute on.
/// </summary>
public sealed record ExecutionOptions
{
    /// <summary>
    /// Gets the default options, which parallelize across the available cores,
    /// allow a short cancellation grace period, and cap the cache budget.
    /// </summary>
    public static ExecutionOptions Default { get; } = new();

    /// <summary>
    /// Gets the greatest number of nodes the runtime starts at the same time.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = Math.Max(1, Environment.ProcessorCount);

    /// <summary>
    /// Gets how long the runtime waits for a node that is already running to
    /// observe cancellation before it stops waiting and quarantines the node.
    /// </summary>
    public TimeSpan CancellationGracePeriod { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the cache budget in bytes. Zero disables caching.
    /// </summary>
    public long CacheBudgetBytes { get; init; } = 512L * 1024 * 1024;
}
