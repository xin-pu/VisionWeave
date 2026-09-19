using System.Windows;
using VisionWeave.App.Notifications;
using VisionWeave.App.ViewModels;

namespace VisionWeave.App;

public partial class MainWindow : Window
{
    internal MainWindow(MainWindowViewModel viewModel, SnackbarNotificationPresenter notifications)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(notifications);

        InitializeComponent();
        DataContext = viewModel;

        // The region the snackbar is drawn in exists only once the markup is built,
        // so the presenter is attached here — before the window can run a command
        // that reports a condition.
        notifications.Attach(SnackbarHost);
    }
}
