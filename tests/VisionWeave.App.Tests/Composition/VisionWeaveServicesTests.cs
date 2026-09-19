using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Composition;
using VisionWeave.App.Notifications;
using VisionWeave.App.Sessions;

namespace VisionWeave.App.Tests.Composition;

/// <summary>
/// Covers the services the shell's command boundary is built from, so a
/// registration the host needs fails here rather than when the window is created.
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
        provider.GetRequiredService<IUserNotificationPresenter>().ShouldBeOfType<MessageBoxNotificationPresenter>();
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
}
