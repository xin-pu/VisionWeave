namespace VisionWeave.Application.Editing;

/// <summary>
/// An edit that continues the undo unit of the command before it when both aim at
/// the same target. The history asks the earlier command, because the earlier
/// command holds the values that the merged unit has to restore.
/// </summary>
internal interface ICoalescingCommand
{
    /// <summary>
    /// Determines whether a later edit is a continuation of this one.
    /// </summary>
    /// <param name="next">The edit about to be applied.</param>
    /// <returns><see langword="true"/> when both belong to one undo unit.</returns>
    bool ContinuesInto(IDocumentCommand next);

    /// <summary>
    /// Takes over what a later edit of the same unit carries. That edit has already
    /// been applied, so the document holds its value while the history keeps this
    /// command; taking the value over is what makes the kept command describe the
    /// whole unit rather than only its first edit, so redoing the unit restores where
    /// the user ended instead of where they started.
    /// </summary>
    /// <param name="next">The edit that joined this unit.</param>
    void ContinueWith(IDocumentCommand next);
}
