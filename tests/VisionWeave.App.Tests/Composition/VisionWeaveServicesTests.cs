using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Composition;
using VisionWeave.App.Notifications;
using VisionWeave.App.Sessions;
using VisionWeave.App.ViewModels;

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
}
