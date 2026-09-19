namespace VisionWeave.Application.Execution;

/// <summary>
/// The overall outcome of a run. Independent branches may finish while another
/// branch is blocked, so the run status reports failure and cancellation only.
/// </summary>
public enum WorkflowRunStatus
{
    /// <summary>No node failed, and the run finished.</summary>
    Succeeded,

    /// <summary>At least one node failed, so its dependent branch did not produce a value.</summary>
    Failed,

    /// <summary>The run stopped because cancellation was requested.</summary>
    Cancelled,
}
