using Shouldly;
using VisionWeave.App.Notifications;
using VisionWeave.App.Tests.Support;
using VisionWeave.Contracts.Diagnostics;
using Wpf.Ui.Controls;

namespace VisionWeave.App.Tests.Notifications;

/// <summary>
/// Covers the presenter that replaced the shell's message box: an announcement
/// carries the safe message and the stable code, is dressed by severity, and waits
/// for the region it is drawn in rather than failing when the shell has none yet.
/// Each test runs on an STA thread, because a snackbar host and the icon an
/// announcement is shown with are WPF elements, exactly as they are in the shell.
/// </summary>
public sealed class SnackbarNotificationPresenterTests
{
    [Fact]
    public void Present_before_the_shell_has_a_host_shows_nothing()
        => StaThread.Run(() =>
        {
            RecordingSnackbarService snackbar = new();
            SnackbarNotificationPresenter presenter = new(snackbar);

            presenter.Present(Error());

            // A message that cannot be drawn is dropped rather than queued: the same
            // diagnostic also reached the status area and the caller's result.
            snackbar.Announcements.ShouldBeEmpty();
        });

    [Fact]
    public void Present_shows_the_safe_message_and_the_stable_code()
        => StaThread.Run(() =>
        {
            (RecordingSnackbarService snackbar, SnackbarNotificationPresenter presenter) = Attached();

            presenter.Present(Error());

            RecordingSnackbarService.Announcement announcement = snackbar.Announcements.ShouldHaveSingleItem();
            announcement.Title.ShouldBe("VisionWeave");
            announcement.Message.ShouldContain("The document could not be read.");
            announcement.Message.ShouldContain(DiagnosticCodes.UnreadableDocument);
            announcement.Message.ShouldStartWith("Error: ");
            announcement.Icon.ShouldNotBeNull("the severity is not left to the colour alone.");
            announcement.Timeout.ShouldBe(TimeSpan.FromSeconds(8));
        });

    [Fact]
    public void Present_never_shows_the_exception_the_diagnostic_carries()
        => StaThread.Run(() =>
        {
            (RecordingSnackbarService snackbar, SnackbarNotificationPresenter presenter) = Attached();

            presenter.Present(new NodeDiagnostic(
                DiagnosticCodes.UnreadableDocument,
                DiagnosticSeverity.Error,
                "The workflow file could not be read.",
                null,
                new InvalidOperationException(@"Denied for C:\private\workflow.vwflow")));

            RecordingSnackbarService.Announcement announcement = snackbar.Announcements.ShouldHaveSingleItem();
            announcement.Message.ShouldNotContain("private");
            announcement.Message.ShouldNotContain("Denied");
        });

    [Theory]
    [InlineData(DiagnosticSeverity.Error, "Error", ControlAppearance.Danger)]
    [InlineData(DiagnosticSeverity.Warning, "Warning", ControlAppearance.Caution)]
    [InlineData(DiagnosticSeverity.Information, "Information", ControlAppearance.Info)]
    public void Present_names_and_dresses_the_announcement_by_severity(
        DiagnosticSeverity severity,
        string expectedWord,
        ControlAppearance expectedAppearance)
        => StaThread.Run(() =>
        {
            (RecordingSnackbarService snackbar, SnackbarNotificationPresenter presenter) = Attached();

            presenter.Present(new NodeDiagnostic("VW-TEST-001", severity, "A condition."));

            RecordingSnackbarService.Announcement announcement = snackbar.Announcements.ShouldHaveSingleItem();
            announcement.Message.ShouldStartWith($"{expectedWord}: ");
            announcement.Appearance.ShouldBe(expectedAppearance);
        });

    private static (RecordingSnackbarService Snackbar, SnackbarNotificationPresenter Presenter) Attached()
    {
        RecordingSnackbarService snackbar = new();
        SnackbarNotificationPresenter presenter = new(snackbar);

        // The window attaches its host while it builds itself, so every announcement
        // after that point has somewhere to be drawn.
        presenter.Attach(new SnackbarPresenter());

        return (snackbar, presenter);
    }

    private static NodeDiagnostic Error()
        => new(
            DiagnosticCodes.UnreadableDocument,
            DiagnosticSeverity.Error,
            "The document could not be read. Check that the file exists and is a VisionWeave workflow.");
}
