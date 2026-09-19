using Microsoft.Extensions.Configuration;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.OpenCv.Preview;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Composition;

/// <summary>
/// The settings this host reads, bound from configuration and ready to be
/// checked. Each section belongs to the layer that owns the policy it shapes, so
/// the host only decides which file supplies the values and when they are
/// refused.
/// </summary>
internal sealed record VisionWeaveSettings(
    ExecutionOptions Execution,
    FramePreviewOptions Preview,
    WorkflowAutosaveOptions Autosave)
{
    internal const string SectionName = "VisionWeave";

    internal static VisionWeaveSettings Default { get; } = new(
        ExecutionOptions.Default,
        FramePreviewOptions.Default,
        WorkflowAutosaveOptions.Default);

    /// <summary>
    /// Loads the deployment and optional per-user configuration files without
    /// letting an unreadable file or an unbindable value escape the startup
    /// boundary.
    /// </summary>
    /// <param name="basePath">The directory containing the settings files.</param>
    /// <returns>The bound settings, or a safe diagnostic explaining the failure.</returns>
    internal static SettingsLoadResult Load(string basePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        try
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile("appsettings.user.json", optional: true, reloadOnChange: false)
                .Build();

            return SettingsLoadResult.Success(Bind(configuration));
        }
        catch (Exception exception) when (IsConfigurationFailure(exception))
        {
            return SettingsLoadResult.Failure(new NodeDiagnostic(
                DiagnosticCodes.InvalidSetting,
                DiagnosticSeverity.Error,
                "VisionWeave could not load its configuration. Ensure appsettings.json is present and contains valid settings.",
                null));
        }
    }

    /// <summary>
    /// Binds every section, leaving a key the file does not mention at the
    /// default its own options type declares.
    /// </summary>
    internal static VisionWeaveSettings Bind(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new VisionWeaveSettings(
            BindSection(configuration, nameof(Execution), ExecutionOptions.Default),
            BindSection(configuration, nameof(Preview), FramePreviewOptions.Default),
            BindSection(configuration, nameof(Autosave), WorkflowAutosaveOptions.Default));
    }

    /// <summary>
    /// Checks every section, in section order, so the reported problems are
    /// stable and the user can fix them in one pass.
    /// </summary>
    internal IReadOnlyList<NodeDiagnostic> Validate()
        => [.. Execution.Validate(), .. Preview.Validate(), .. Autosave.Validate()];

    private static T BindSection<T>(IConfiguration configuration, string name, T fallback)
        where T : class
        => configuration.GetSection($"{SectionName}:{name}").Get<T>() ?? fallback;

    private static bool IsConfigurationFailure(Exception exception)
        => exception is System.IO.IOException
            or UnauthorizedAccessException
            or System.IO.InvalidDataException
            or FormatException
            or InvalidOperationException;
}
