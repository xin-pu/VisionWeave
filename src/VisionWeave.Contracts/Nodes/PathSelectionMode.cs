namespace VisionWeave.Contracts.Nodes;

/// <summary>
///     Describes the location a path parameter asks the user to choose.
/// </summary>
public enum PathSelectionMode
{
    /// <summary>
    ///     An existing file that will be read.
    /// </summary>
    OpenFile,

    /// <summary>
    ///     A file destination that may not exist yet.
    /// </summary>
    SaveFile,

    /// <summary>
    ///     An existing directory.
    /// </summary>
    Folder,
}
