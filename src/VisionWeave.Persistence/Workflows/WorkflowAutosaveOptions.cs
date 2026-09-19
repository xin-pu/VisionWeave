using System.Globalization;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.Persistence.Workflows;

/// <summary>
/// Shapes how often an editing session writes the recoverable working copy of a
/// document. It lives beside the session because the working copy, the interval
/// that refreshes it, and the recovery that reads it are one policy.
/// </summary>
public sealed record WorkflowAutosaveOptions
{
    /// <summary>
    /// Gets the shortest interval an enabled autosave may use. A shorter one
    /// would write the working copy as fast as an editor can change the document.
    /// </summary>
    public static TimeSpan MinInterval { get; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets the longest interval an enabled autosave may use. Beyond it the copy
    /// stops protecting more than a few minutes of work, which is what a crash
    /// recovery is for.
    /// </summary>
    public static TimeSpan MaxInterval { get; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets the default options, which refresh the working copy every two minutes.
    /// </summary>
    public static WorkflowAutosaveOptions Default { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the working copy is written at all.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets how much time passes between two working-copy writes. It is only read
    /// while autosave is enabled.
    /// </summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Checks that the autosave policy is one the session can honor. The interval
    /// is only checked while autosave is enabled, because a disabled autosave
    /// never reads it and a leftover value should not stop the application.
    /// </summary>
    /// <returns>The problems found, or an empty list.</returns>
    public IReadOnlyList<NodeDiagnostic> Validate()
    {
        if (!Enabled || (Interval >= MinInterval && Interval <= MaxInterval))
        {
            return [];
        }

        return
        [
            new NodeDiagnostic(
                DiagnosticCodes.InvalidSetting,
                DiagnosticSeverity.Error,
                $"Setting '{nameof(WorkflowAutosaveOptions)}.{nameof(Interval)}' is {Interval.ToString("c", CultureInfo.InvariantCulture)}, but it must be a duration between {MinInterval.ToString("c", CultureInfo.InvariantCulture)} and {MaxInterval.ToString("c", CultureInfo.InvariantCulture)} while autosave is enabled.",
                null),
        ];
    }
}
