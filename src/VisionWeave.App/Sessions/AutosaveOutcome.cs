namespace VisionWeave.App.Sessions;

/// <summary>
/// How an autosave attempt finished. Only <see cref="Written"/> means a working
/// copy reached the disk; the other outcomes separate "there was nothing to
/// write" from "this document must not be written" and from "the write was
/// attempted and failed", so a scheduler can tell a document it should stop
/// asking about from one it may retry.
/// </summary>
internal enum AutosaveOutcome
{
    /// <summary>The document had nothing to autosave, because it has no file yet or no unsaved changes.</summary>
    NotApplicable,

    /// <summary>The working copy was written.</summary>
    Written,

    /// <summary>The document must not be written at all, so no attempt was made.</summary>
    Refused,

    /// <summary>The write was attempted and failed in a way the session expects, such as an unusable path.</summary>
    Failed,
}
