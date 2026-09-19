using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Composition;

/// <summary>
/// The safe result of loading host settings before the application has built its
/// dependency-injection container.
/// </summary>
/// <param name="Settings">The bound settings when loading succeeded.</param>
/// <param name="Diagnostics">The stable, user-safe load diagnostics.</param>
internal sealed record SettingsLoadResult(
    VisionWeaveSettings? Settings,
    IReadOnlyList<NodeDiagnostic> Diagnostics)
{
    /// <summary>Gets whether settings were loaded successfully.</summary>
    internal bool Succeeded => Settings is not null && Diagnostics.Count == 0;

    /// <summary>Creates a successful result.</summary>
    internal static SettingsLoadResult Success(VisionWeaveSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new SettingsLoadResult(settings, []);
    }

    /// <summary>Creates a failed result.</summary>
    internal static SettingsLoadResult Failure(NodeDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return new SettingsLoadResult(null, [diagnostic]);
    }
}
