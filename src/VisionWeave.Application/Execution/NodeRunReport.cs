using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.Application.Execution;

/// <summary>
/// What happened to one node in a run: its terminal state, how long it took, and
/// the diagnostics it or the runtime reported for it.
/// </summary>
/// <param name="NodeInstanceId">The node instance identifier.</param>
/// <param name="State">The terminal state of the node.</param>
/// <param name="Duration">How long the node was executing.</param>
/// <param name="Diagnostics">The diagnostics reported for this node.</param>
public sealed record NodeRunReport(
    Guid NodeInstanceId,
    NodeRunState State,
    TimeSpan Duration,
    IReadOnlyList<NodeDiagnostic> Diagnostics)
{
    /// <summary>
    /// Creates a report for a node that never executed.
    /// </summary>
    /// <param name="nodeInstanceId">The node instance identifier.</param>
    /// <param name="state">Either <see cref="NodeRunState.Blocked"/> or <see cref="NodeRunState.NotRun"/>.</param>
    /// <param name="diagnostic">The diagnostic that explains the state.</param>
    /// <returns>The report.</returns>
    public static NodeRunReport WithoutExecution(Guid nodeInstanceId, NodeRunState state, NodeDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return new NodeRunReport(nodeInstanceId, state, TimeSpan.Zero, [diagnostic]);
    }
}
