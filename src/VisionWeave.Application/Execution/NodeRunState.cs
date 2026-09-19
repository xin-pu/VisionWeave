namespace VisionWeave.Application.Execution;

/// <summary>
/// The terminal state of one node in a run. Only terminal states are reported,
/// so a run summary never contains a node that is still executing, except when a
/// node was quarantined after ignoring cancellation.
/// </summary>
public enum NodeRunState
{
    /// <summary>The node produced its outputs.</summary>
    Succeeded,

    /// <summary>The node reported a failure.</summary>
    Failed,

    /// <summary>The node observed cancellation, or was quarantined while stopping.</summary>
    Cancelled,

    /// <summary>The node was not executed because a producer it requires did not produce a value.</summary>
    Blocked,

    /// <summary>The node was never started because the run ended first.</summary>
    NotRun,
}
