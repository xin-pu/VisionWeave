using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using VisionWeave.App.Commands;
using VisionWeave.App.Notifications;
using VisionWeave.App.Preview;
using VisionWeave.App.Sessions;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Execution;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Nodes;
using VisionWeave.OpenCv.Preview;
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

        // The pieces one run is made of: the ledger that watches native frame
        // lifetimes, the executors this build ships, the capture that validates a
        // document into a snapshot, the plan that orders it, and the runtime that
        // executes it.
        services.AddSingleton<ILeaseLedger, LeaseLedger>();
        services.AddSingleton<INodeExecutorResolver>(provider => new ExecutorRegistry(
            provider.GetRequiredService<ILeaseLedger>()));
        services.AddSingleton(provider => new WorkflowSnapshotFactory(
            provider.GetRequiredService<NodeDefinitionCatalog>()));
        services.AddSingleton<ExecutionPlanBuilder>();
        services.AddSingleton(provider => new WorkflowRunner(
            provider.GetRequiredService<INodeExecutorResolver>(),
            provider.GetRequiredService<ILeaseLedger>(),
            provider.GetRequiredService<ExecutionOptions>(),
            provider.GetRequiredService<IExecutionOutputObserver>()));

        // The managed preview a run publishes. Frames are converted where the run
        // executes and shown on the thread the window's bindings belong to, which is
        // the thread this registration runs on, so the dispatcher is captured here
        // rather than looked up while a preview is arriving.
        services.AddSingleton(provider => new FramePreviewConverter(
            provider.GetRequiredService<FramePreviewOptions>()));
        services.AddSingleton<PreviewViewModel>();
        services.AddSingleton<IExecutionOutputObserver>(provider => new RunPreviewObserver(
            provider.GetRequiredService<FramePreviewConverter>(),
            provider.GetRequiredService<NodeDefinitionCatalog>(),
            provider.GetRequiredService<IRunPreviewPresenter>()));
        services.AddSingleton<IRunPreviewPresenter>(provider => new ShellRunPreviewPresenter(
            provider.GetRequiredService<PreviewViewModel>(),
            Dispatcher.CurrentDispatcher));

        // The commands that run a document and stop the run: two gestures over one
        // running state, which is why they are one object.
        services.AddSingleton(provider => new RunWorkflowCommand(
            provider.GetRequiredService<AsyncCommandBoundary>(),
            provider.GetRequiredService<EditorSession>(),
            provider.GetRequiredService<WorkflowSnapshotFactory>(),
            provider.GetRequiredService<ExecutionPlanBuilder>(),
            provider.GetRequiredService<WorkflowRunner>(),
            provider.GetRequiredService<ShellStatus>()));

        // The durable half of what the shell reports: the status area keeps the
        // document state and the newest condition, while the snackbar announces a
        // condition once and lets it fade.
        services.AddSingleton<ShellStatus>();

        return services;
    }
}
