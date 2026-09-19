using System.Windows;
using VisionWeave.App.ViewModels;

namespace VisionWeave.App;

public partial class MainWindow : Window
{
    internal MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }
}
