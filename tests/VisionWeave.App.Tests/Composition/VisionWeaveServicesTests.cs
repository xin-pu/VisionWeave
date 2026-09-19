using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Composition;
using VisionWeave.App.Notifications;

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
}
