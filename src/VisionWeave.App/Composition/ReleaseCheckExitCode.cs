namespace VisionWeave.App.Composition;

/// <summary>
/// What a release check reports to whoever started it. A package that cannot run
/// must say so with a code, because the machine that runs the check may be looking
/// at nothing else.
/// </summary>
internal enum ReleaseCheckExitCode
{
    /// <summary>The published build composed its host and ran a native operation.</summary>
    Passed = 0,

    /// <summary>The settings beside the executable are missing or rejected.</summary>
    SettingsRejected = 1,

    /// <summary>The host could not compose, or the operation through OpenCV failed.</summary>
    OperationFailed = 2,
}
