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
}
