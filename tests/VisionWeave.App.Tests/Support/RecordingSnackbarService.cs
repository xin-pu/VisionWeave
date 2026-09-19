using Wpf.Ui;
using Wpf.Ui.Controls;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// Keeps every announcement the presenter asked the snackbar to show, so a test
/// asserts what the user would have read instead of opening a window. The
/// presenter host is remembered as well, because a presenter that was never
/// attached must show nothing at all.
/// </summary>
internal sealed class RecordingSnackbarService : ISnackbarService
{
    private readonly List<Announcement> _announcements = [];
    private SnackbarPresenter? _host;

    /// <summary>Gets the announcements that were shown, in order.</summary>
    internal IReadOnlyList<Announcement> Announcements => _announcements;

    /// <summary>Gets a value indicating whether a host was attached.</summary>
    internal bool HasHost => _host is not null;

    /// <inheritdoc />
    public TimeSpan DefaultTimeOut { get; set; } = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public void SetSnackbarPresenter(SnackbarPresenter presenter) => _host = presenter;

    /// <inheritdoc />
    public SnackbarPresenter GetSnackbarPresenter() => _host!;

    /// <inheritdoc />
    public void Show(
        string title,
        string message,
        ControlAppearance appearance,
        IconElement? icon,
        TimeSpan timeout)
        => _announcements.Add(new Announcement(title, message, appearance, icon, timeout));

    /// <summary>One announcement the snackbar was asked to show.</summary>
    /// <param name="Title">The title it was shown under.</param>
    /// <param name="Message">The body it was shown with.</param>
    /// <param name="Appearance">How it was dressed.</param>
    /// <param name="Icon">The shape it was shown with.</param>
    /// <param name="Timeout">How long it stays on screen.</param>
    internal sealed record Announcement(
        string Title,
        string Message,
        ControlAppearance Appearance,
        IconElement? Icon,
        TimeSpan Timeout);
}
