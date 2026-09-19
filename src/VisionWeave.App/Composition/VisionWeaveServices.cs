using Microsoft.Extensions.DependencyInjection;
using VisionWeave.App.Commands;
using VisionWeave.App.Notifications;
using VisionWeave.App.Sessions;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Nodes;
using VisionWeave.OpenCv.Nodes;
using Wpf.Ui;

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

        // The shell's command and error boundary: one place runs an asynchronous
        // operation, one place shows the failure it reports, and one log records
        // the failure the operation did not anticipate. The window attaches the
        // snackbar region to the presenter it resolves here, and the boundary
        // reports through that same instance, so a condition cannot be shown twice
        // or land in a presenter nothing is attached to.
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<SnackbarNotificationPresenter>();
        services.AddSingleton<IUserNotificationPresenter>(
            provider => provider.GetRequiredService<SnackbarNotificationPresenter>());
        services.AddSingleton<AsyncCommandBoundary>();
        services.AddSingleton<IDocumentLoader, DocumentLoader>();

        // The shell's one question about a file. It is a seam with a Windows
        // implementation behind it, so the open flow is testable without a window.
        services.AddSingleton<IWorkflowFileChooser, WindowsWorkflowFileChooser>();

        // The one editing session the shell presents. It composes the document, its
        // undo stack, the selection, and the validation projection, so the layers
        // that own those pieces stay unaware of each other.
        services.AddSingleton<EditorSession>();

        // The durable half of what the shell reports: the status area keeps the
        // document state and the newest condition, while the snackbar announces a
        // condition once and lets it fade.
        services.AddSingleton<ShellStatus>();

        return services;
    }
}
