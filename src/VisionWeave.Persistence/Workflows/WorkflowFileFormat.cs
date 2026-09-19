namespace VisionWeave.Persistence.Workflows;

/// <summary>
/// Declares the workflow file format. The schema version is a document-level
/// integer that decides whether a stored file can be read as it is or must be
/// migrated before it is used.
/// </summary>
public static class WorkflowFileFormat
{
    /// <summary>
    /// Gets the schema version this build writes and reads without migration.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Gets the suffix of the recoverable autosave working copy that is written
    /// next to a document.
    /// </summary>
    public const string WorkingCopySuffix = ".autosave";
}
