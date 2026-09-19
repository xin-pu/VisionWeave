using System.Windows;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Notifications;

/// <summary>
/// Presents a condition in a message box. It is the placeholder the shell ships
/// until its status area and snackbar exist, and it shows the stable code and the
/// safe message only: the diagnostic's exception belongs to the log, and a
/// message that carried it would leak a path or a payload onto the screen.
/// </summary>
internal sealed class MessageBoxNotificationPresenter : IUserNotificationPresenter
{
    private const string Caption = "VisionWeave";

    /// <inheritdoc />
    public void Present(NodeDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        Show(diagnostic);

        void Show(NodeDiagnostic message)
        {
            void Display()
                => MessageBox.Show(
                    $"{message.Message}{Environment.NewLine}{Environment.NewLine}{message.Code}",
                    Caption,
                    MessageBoxButton.OK,
                    Icon(message.Severity));

            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(Display);
                return;
            }

            Display();
        }
    }

    private static MessageBoxImage Icon(DiagnosticSeverity severity)
        => severity switch
        {
            DiagnosticSeverity.Error => MessageBoxImage.Error,
            DiagnosticSeverity.Warning => MessageBoxImage.Warning,
            _ => MessageBoxImage.Information,
        };
}
