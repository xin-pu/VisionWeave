namespace VisionWeave.Application.Editing;

/// <summary>
/// Shapes how one editing session groups document changes into undo units.
/// </summary>
public sealed class DocumentEditingOptions
{
    /// <summary>
    /// Gets the options an editor starts with.
    /// </summary>
    public static DocumentEditingOptions Default { get; } = new();

    /// <summary>
    /// Gets how long successive edits of the same node parameter keep joining the
    /// undo unit they started. Typing a value or dragging a numeric spinner emits
    /// many changes in a moment; the user thinks of them as one edit, so undo
    /// should return to the value the parameter held before the first of them.
    /// </summary>
    public TimeSpan ParameterCoalescingWindow { get; init; } = TimeSpan.FromMilliseconds(400);
}
