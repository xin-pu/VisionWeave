using System.Globalization;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.Application.Execution;

/// <summary>
/// The limits one run obeys. They are explicit so that a test can pin them
/// instead of depending on the machine the run happens to execute on.
/// </summary>
public sealed record ExecutionOptions
{
    /// <summary>
    /// Gets the smallest parallelism a run may use.
    /// </summary>
    public const int MinDegreeOfParallelism = 1;

    /// <summary>
    /// Gets the largest parallelism a run may use. It is far above any practical
    /// machine, so the bound only rejects a typo that would otherwise start an
    /// absurd number of nodes at once.
    /// </summary>
    public const int MaxDegreeOfParallelismLimit = 1_024;

    /// <summary>
    /// Gets the largest cache budget a run may reserve.
    /// </summary>
    public const long MaxCacheBudgetBytes = 64L * 1024 * 1024 * 1024;

    /// <summary>
    /// Gets the default options, which parallelize across the available cores,
    /// allow a short cancellation grace period, and cap the cache budget.
    /// </summary>
    public static ExecutionOptions Default { get; } = new();

    /// <summary>
    /// Gets the longest cancellation grace period a run may wait. A node that is
    /// still running after this has already ignored cancellation beyond any
    /// reasonable bound, so a longer wait only delays the quarantine report.
    /// </summary>
    public static TimeSpan MaxCancellationGracePeriod { get; } = TimeSpan.FromMinutes(5);

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

    /// <summary>
    /// Checks that every limit is inside the range the runtime can honor. A
    /// configured value is refused rather than clamped, because silently running
    /// with a different limit than the one that was written down hides the
    /// mistake until it shows up as unexplained behavior.
    /// </summary>
    /// <returns>The problems found, in declaration order, or an empty list.</returns>
    public IReadOnlyList<NodeDiagnostic> Validate()
    {
        List<NodeDiagnostic> diagnostics = [];

        if (MaxDegreeOfParallelism is < MinDegreeOfParallelism or > MaxDegreeOfParallelismLimit)
        {
            diagnostics.Add(Reject(
                nameof(MaxDegreeOfParallelism),
                MaxDegreeOfParallelism.ToString(CultureInfo.InvariantCulture),
                $"a count between {MinDegreeOfParallelism} and {MaxDegreeOfParallelismLimit}"));
        }

        if (CancellationGracePeriod < TimeSpan.Zero || CancellationGracePeriod > MaxCancellationGracePeriod)
        {
            diagnostics.Add(Reject(
                nameof(CancellationGracePeriod),
                CancellationGracePeriod.ToString("c", CultureInfo.InvariantCulture),
                $"a duration between {TimeSpan.Zero.ToString("c", CultureInfo.InvariantCulture)} and {MaxCancellationGracePeriod.ToString("c", CultureInfo.InvariantCulture)}"));
        }

        if (CacheBudgetBytes is < 0 or > MaxCacheBudgetBytes)
        {
            diagnostics.Add(Reject(
                nameof(CacheBudgetBytes),
                CacheBudgetBytes.ToString(CultureInfo.InvariantCulture),
                $"a byte count between 0 and {MaxCacheBudgetBytes.ToString(CultureInfo.InvariantCulture)}"));
        }

        return diagnostics;
    }

    private static NodeDiagnostic Reject(string property, string value, string expected)
        => new(
            DiagnosticCodes.InvalidSetting,
            DiagnosticSeverity.Error,
            $"Setting '{nameof(ExecutionOptions)}.{property}' is {value}, but it must be {expected}.",
            null);
}
