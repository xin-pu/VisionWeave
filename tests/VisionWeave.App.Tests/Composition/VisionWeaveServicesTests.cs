using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Composition;
using VisionWeave.App.Notifications;
using VisionWeave.App.Preview;
using VisionWeave.App.Sessions;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.App.Tests.Composition;

/// <summary>
/// Covers the services the shell is composed from, so a registration the host
/// needs fails here rather than when the window is created.
/// </summary>
public sealed class VisionWeaveServicesTests
{
    [Fact]
    public void AddVisionWeave_resolves_the_command_boundary_and_everything_it_reports_through()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddVisionWeave(VisionWeaveSettings.Default);

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<AsyncCommandBoundary>().ShouldNotBeNull();
        provider.GetRequiredService<IDocumentLoader>().ShouldBeOfType<DocumentLoader>();
        provider.GetRequiredService<IWorkflowFileChooser>().ShouldBeOfType<WindowsWorkflowFileChooser>();

        // The presenter the boundary reports through is the instance the window
        // attaches its snackbar region to, so a condition cannot be announced into a
        // presenter nothing is listening to.
        provider.GetRequiredService<IUserNotificationPresenter>()
            .ShouldBeOfType<SnackbarNotificationPresenter>()
            .ShouldBeSameAs(provider.GetRequiredService<SnackbarNotificationPresenter>());
    }

    [Fact]
    public void AddVisionWeave_resolves_the_editing_session_the_shell_starts_on()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddVisionWeave(VisionWeaveSettings.Default);

        using ServiceProvider provider = services.BuildServiceProvider();

        EditorSession session = provider.GetRequiredService<EditorSession>();

        session.Document.Name.ShouldBe(EditorSession.UntitledDocumentName);
        session.Path.ShouldBeNull();
        session.Projection.ShouldNotBeNull();
        session.IsDocumentValid.ShouldBeTrue();
    }

    [Fact]
    public void AddVisionWeave_resolves_one_status_area_over_the_editing_session()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddVisionWeave(VisionWeaveSettings.Default);

        using ServiceProvider provider = services.BuildServiceProvider();

        ShellStatus status = provider.GetRequiredService<ShellStatus>();

        status.ShouldBeSameAs(provider.GetRequiredService<ShellStatus>());
        status.DocumentState.ShouldBe("Not saved yet");
        status.BackgroundOperation.ShouldBe(ShellStatus.ReadyText);
    }

    [Fact]
    public void AddVisionWeave_resolves_the_run_the_shell_starts()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddVisionWeave(VisionWeaveSettings.Default);

        using ServiceProvider provider = services.BuildServiceProvider();

        // The run is one object over the objects the container owns: the ledger every
        // frame is reported to, the executors this build ships, the capture and the
        // plan builder, and the runtime. A registration that reaches for a type nothing
        // registered fails here rather than when the window is built.
        provider.GetRequiredService<RunWorkflowCommand>().ShouldNotBeNull();
        provider.GetRequiredService<PreviewViewModel>().ShouldNotBeNull();
        provider.GetRequiredService<WorkflowRunner>().ShouldNotBeNull();
        provider.GetRequiredService<WorkflowSnapshotFactory>().ShouldNotBeNull();
        provider.GetRequiredService<ExecutionPlanBuilder>().ShouldNotBeNull();
        provider.GetRequiredService<IExecutionOutputObserver>().ShouldBeOfType<RunPreviewObserver>();
        provider.GetRequiredService<ILeaseLedger>().ShouldBeSameAs(provider.GetRequiredService<ILeaseLedger>());

        // The preview a run publishes obeys the configured bound rather than a constant
        // of the composition, so a setting the user wrote is the one the shell runs with.
        provider.GetRequiredService<FramePreviewConverter>().MaxPixelArea
            .ShouldBe(VisionWeaveSettings.Default.Preview.MaxPixelArea);
    }
}
