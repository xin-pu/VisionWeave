namespace VisionWeave.Contracts.Ports;

/// <summary>
/// Decides whether a value produced by one port type may feed another. The
/// relation is declared rather than inferred, so no implicit runtime coercion
/// can appear between two nodes.
/// </summary>
public sealed class PortCompatibility
{
    private readonly HashSet<PortCompatibilityRule> _rules;

    /// <summary>
    /// Initializes the relation from the declared rules.
    /// </summary>
    /// <param name="rules">The accepted source-to-target assignments.</param>
    public PortCompatibility(IEnumerable<PortCompatibilityRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = [.. rules];
    }

    /// <summary>
    /// Gets the relation used by the built-in catalog, where only identical port
    /// types are accepted.
    /// </summary>
    public static PortCompatibility BuiltIn { get; } = new([]);

    /// <summary>
    /// Gets the declared rules.
    /// </summary>
    public IReadOnlyCollection<PortCompatibilityRule> Rules => _rules;

    /// <summary>
    /// Determines whether a value of the source port type may be assigned to the
    /// target port type.
    /// </summary>
    /// <param name="source">The port type produced upstream.</param>
    /// <param name="target">The port type accepted downstream.</param>
    /// <returns><see langword="true"/> when the assignment is accepted.</returns>
    public bool IsAccepted(PortTypeId source, PortTypeId target)
        => source == target || _rules.Contains(new PortCompatibilityRule(source, target));
}
