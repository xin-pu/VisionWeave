using Microsoft.Extensions.DependencyInjection;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Nodes;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.App.Composition;

/// <summary>
/// The composition root. It is the only place that knows which implementations
/// this build ships, so a layer downstream of it never has to name a concrete
/// OpenCV, persistence, or logging type.
/// </summary>
internal static class VisionWeaveServices
{
    /// <summary>
    /// Registers the services this host runs on.
    /// </summary>
    /// <param name="services">The collection to add to.</param>
    /// <param name="settings">The validated settings.</param>
    /// <returns>The same collection, so registration can be chained.</returns>
    internal static IServiceCollection AddVisionWeave(this IServiceCollection services, VisionWeaveSettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        services.AddSingleton(settings.Execution);
        services.AddSingleton(settings.Preview);
        services.AddSingleton(settings.Autosave);
        services.AddSingleton(TimeProvider.System);

        // Every provider contributes through the same path, so a duplicate node
        // type identifier fails composition instead of quietly shadowing the
        // definition that arrived first.
        services.AddSingleton<INodeDefinitionProvider, OpenCvNodeDefinitionProvider>();
        services.AddSingleton(provider => NodeDefinitionCatalog.FromProviders(
            provider.GetServices<INodeDefinitionProvider>()));

        services.AddSingleton<WorkflowValidator>();

        return services;
    }
}
