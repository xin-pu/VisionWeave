using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Values;

namespace VisionWeave.Contracts.Execution;

/// <summary>
/// The outcome of one node execution: its terminal state, output values, and the
/// diagnostics it reported.
/// </summary>
public sealed record NodeExecutionResult
{
    /// <summary>Gets the terminal state of the execution.</summary>
    public required NodeExecutionStatus Status { get; init; }

    /// <summary>Gets the output values keyed by output port identifier.</summary>
    public IReadOnlyDictionary<string, PortValue> Outputs { get; init; } =
        new Dictionary<string, PortValue>(StringComparer.Ordinal);

    /// <summary>Gets the diagnostics reported by the node.</summary>
    public IReadOnlyList<NodeDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="outputs">The produced output values.</param>
    /// <returns>The result.</returns>
    public static NodeExecutionResult Success(IReadOnlyDictionary<string, PortValue> outputs)
        => new()
        {
            Status = NodeExecutionStatus.Succeeded,
            Outputs = outputs,
        };

    /// <summary>
    /// Creates a failed result from a diagnostic.
    /// </summary>
    /// <param name="diagnostic">The failure diagnostic.</param>
    /// <returns>The result.</returns>
    public static NodeExecutionResult Failure(NodeDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        return new NodeExecutionResult
        {
            Status = NodeExecutionStatus.Failed,
            Diagnostics = [diagnostic],
        };
    }

    /// <summary>
    /// Creates a cancelled result.
    /// </summary>
    /// <returns>The result.</returns>
    public static NodeExecutionResult Cancelled()
        => new()
        {
            Status = NodeExecutionStatus.Cancelled,
        };
}
