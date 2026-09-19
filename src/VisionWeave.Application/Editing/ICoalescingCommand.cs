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
}
