namespace VisionWeave.App.Commands;

/// <summary>
/// How a command's operation finished. A cancelled command is not a failure: the
/// caller asked for it, and the shell reports nothing to the user.
/// </summary>
internal enum CommandCompletion
{
    /// <summary>The operation finished and reported the conditions it observed.</summary>
    Completed,

    /// <summary>The operation observed the cancellation the caller requested.</summary>
    Cancelled,

    /// <summary>The operation failed in a way it did not anticipate; the failure was logged once.</summary>
    Failed,
}
