namespace VisionWeave.Contracts.Execution;

/// <summary>
/// The terminal state of one node execution.
/// </summary>
public enum NodeExecutionStatus
{
    /// <summary>The node produced its declared outputs.</summary>
    Succeeded,

    /// <summary>The node failed and its dependent branch is blocked.</summary>
    Failed,

    /// <summary>The node observed cancellation.</summary>
    Cancelled,

    /// <summary>The node was not started because an upstream dependency failed.</summary>
    Blocked,

    /// <summary>The node was skipped by policy, such as a disabled instance.</summary>
    Skipped,
}
