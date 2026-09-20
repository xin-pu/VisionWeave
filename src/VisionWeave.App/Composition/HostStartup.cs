using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Composition;

/// <summary>
/// The host a startup composes: the settings beside the executable, the container
/// built from them, and the conditions that stop the shell from opening. The
/// window and the release check begin here, so the two run one composition path
/// instead of two that can disagree about what this build is.
/// </summary>
/// <param name="Services">The built container, or <see langword="null"/> when the settings could not be loaded.</param>
/// <param name="Settings">The bound settings, or <see langword="null"/> when they could not be loaded.</param>
/// <param name="Problems">The conditions that stop startup, in the order they are reported.</param>
internal sealed record HostStartup(
    ServiceProvider? Services,
    VisionWeaveSettings? Settings,
    IReadOnlyList<NodeDiagnostic> Problems)
{
    /// <summary>
    /// Gets whether this host can run.
    /// </summary>
    internal bool Succeeded => Services is not null && Problems.Count == 0;

    /// <summary>
    /// Loads the settings of a deployment folder, checks every section, and builds
    /// the container. The container is built before the settings are refused, so a
    /// rejected setting is reported by the same logging stack that would have run.
    /// </summary>
    /// <param name="baseDirectory">The folder that holds the settings files.</param>
    /// <returns>The composed host, or the diagnostics that refuse it.</returns>
    internal static HostStartup Compose(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        SettingsLoadResult load = VisionWeaveSettings.Load(baseDirectory);
        if (!load.Succeeded)
        {
            return new HostStartup(null, null, load.Diagnostics);
        }

        VisionWeaveSettings settings = load.Settings!;

        ServiceCollection services = new();
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Information));
        services.AddVisionWeave(settings);

        return new HostStartup(services.BuildServiceProvider(), settings, settings.Validate());
    }
}
