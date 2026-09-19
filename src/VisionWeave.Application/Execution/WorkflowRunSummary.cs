using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.Application.Execution;

/// <summary>
/// Everything one run produced: which nodes ran and how they ended, the
/// diagnostics of the whole run, and the operation identifier that correlates
/// this run with its logs and previews.
/// </summary>
public sealed record WorkflowRunSummary
{
    /// <summary>
    /// Gets the identifier that correlates every artifact of this run.
    /// </summary>
    public required Guid OperationId { get; init; }

    /// <summary>
    /// Gets the document the run executed.
    /// </summary>
    public required Guid DocumentId { get; init; }

    /// <summary>
    /// Gets the document revision the run executed.
    /// </summary>
    public required long Revision { get; init; }

    /// <summary>
    /// Gets how long the run took.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the report of every node the plan covered.
    /// </summary>
    public required IReadOnlyList<NodeRunReport> Nodes { get; init; }

    /// <summary>
    /// Gets the diagnostics of the run, including the ones reported by nodes.
    /// </summary>
    public required IReadOnlyList<NodeDiagnostic> Diagnostics { get; init; }

    /// <summary>
    /// Gets a value indicating whether cancellation was requested during the run.
    /// </summary>
    public required bool WasCancelled { get; init; }

    /// <summary>
    /// Gets the identifiers of the nodes a quarantined executor was still
    /// running when the run stopped. Their resources are released by the
    /// quarantine itself, so they are reported separately from completed nodes.
    /// </summary>
    public required IReadOnlyList<Guid> QuarantinedNodeIds { get; init; }

    /// <summary>
    /// Gets the overall outcome: a failure outranks a cancellation, because a
    /// run that stopped early because of a defect is not merely cancelled.
    /// </summary>
    public WorkflowRunStatus Status
        => Nodes.Any(node => node.State == NodeRunState.Failed)
            ? WorkflowRunStatus.Failed
            : WasCancelled || QuarantinedNodeIds.Count > 0
                ? WorkflowRunStatus.Cancelled
                : WorkflowRunStatus.Succeeded;

    /// <summary>
    /// Gets the state of one node.
    /// </summary>
    /// <param name="nodeInstanceId">The node instance identifier.</param>
    /// <returns>The terminal state, or <see langword="null"/> when the plan did not cover the node.</returns>
    public NodeRunState? StateOf(Guid nodeInstanceId)
        => Nodes.FirstOrDefault(node => node.NodeInstanceId == nodeInstanceId)?.State;

    /// <summary>
    /// Determines whether a diagnostic with the given code was reported.
    /// </summary>
    /// <param name="code">A stable code from <see cref="DiagnosticCodes"/>.</param>
    /// <returns><see langword="true"/> when the code was reported.</returns>
    public bool HasCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return Diagnostics.Any(diagnostic => string.Equals(diagnostic.Code, code, StringComparison.Ordinal));
    }
}
