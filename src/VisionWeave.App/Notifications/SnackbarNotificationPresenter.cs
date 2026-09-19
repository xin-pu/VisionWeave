using Wpf.Ui;
using Wpf.Ui.Controls;
using VisionWeave.App.Presentation;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Notifications;

/// <summary>
/// Presents a condition in the shell's snackbar. It replaces the message box the
/// shell shipped with, so a failure is announced without blocking the window, and
/// it shows the stable code and the safe message only: the diagnostic's exception
/// belongs to the log, and a message that carried it would put a path or a payload
/// on the screen.
/// </summary>
internal sealed class SnackbarNotificationPresenter : IUserNotificationPresenter
{
    private const string Title = "VisionWeave";

    /// <summary>How long an announcement stays on screen before it fades.</summary>
    private static readonly TimeSpan VisibleFor = TimeSpan.FromSeconds(8);

    private readonly ISnackbarService _snackbar;

    /// <summary>
    /// Creates the presenter.
    /// </summary>
    /// <param name="snackbar">The snackbar service the announcement is shown through.</param>
    /// <remarks>
    /// The constructor is public because the host's service container resolves this
    /// type and only considers public constructors; the type itself stays internal
    /// to the application.
    /// </remarks>
    public SnackbarNotificationPresenter(ISnackbarService snackbar)
    {
        ArgumentNullException.ThrowIfNull(snackbar);

        _snackbar = snackbar;
    }

    /// <summary>
    /// Attaches the region the snackbar is drawn in. The window calls this while it
    /// builds itself, before any command can run, so a message never waits for a
    /// host that does not exist.
    /// </summary>
    /// <param name="host">The presenter the snackbar control is placed into.</param>
    internal void Attach(SnackbarPresenter host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _snackbar.SetSnackbarPresenter(host);
    }

    /// <inheritdoc />
    public void Present(NodeDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        if (_snackbar.GetSnackbarPresenter() is null)
        {
            // Nothing can be shown before the shell has attached its host. The
            // condition is not lost: the same diagnostic travels to the status area
            // and to the caller's result, so the shell is silent rather than wrong.
            return;
        }

        void Display()
            => _snackbar.Show(
                Title,
                Message(diagnostic),
                Appearance(diagnostic.Severity),
                Icon(diagnostic.Severity),
                VisibleFor);

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Display);
            return;
        }

        Display();
    }

    /// <summary>
    /// Composes what the user reads: the severity in words, the safe message, and
    /// the stable code, which is what a report or a search of the log needs. The
    /// severity is named as well as coloured, so an announcement stays readable
    /// when its hue does not reach the reader.
    /// </summary>
    private static string Message(NodeDiagnostic diagnostic)
        => $"{SeverityText.Of(diagnostic.Severity)}: {diagnostic.Message}{Environment.NewLine}{diagnostic.Code}";

    private static ControlAppearance Appearance(DiagnosticSeverity severity)
        => severity switch
        {
            DiagnosticSeverity.Error => ControlAppearance.Danger,
            DiagnosticSeverity.Warning => ControlAppearance.Caution,
            _ => ControlAppearance.Info,
        };

    /// <summary>
    /// Gives the announcement a shape as well as a colour, so the severity of a
    /// message that stays on screen for seconds does not rest on its hue alone.
    /// </summary>
    private static IconElement Icon(DiagnosticSeverity severity)
        => new SymbolIcon(severity switch
        {
            DiagnosticSeverity.Error => SymbolRegular.ErrorCircle24,
            DiagnosticSeverity.Warning => SymbolRegular.Warning24,
            _ => SymbolRegular.Info24,
        });
}
