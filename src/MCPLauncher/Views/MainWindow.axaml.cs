using Avalonia.Controls;
using MCPLauncher.ViewModels;

namespace MCPLauncher.Views;

public sealed partial class MainWindow : Window
{
    private bool _closeApproved;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
    }

    public MainWindowViewModel ViewModel { get; } = null!;

    private async void OnOpened(object? sender, EventArgs eventArgs) => await ViewModel.InitializeAsync();

    private async void OnClosing(object? sender, WindowClosingEventArgs eventArgs)
    {
        if (_closeApproved)
        {
            return;
        }

        eventArgs.Cancel = true;
        if (!await ViewModel.TryCloseAsync())
        {
            return;
        }

        _closeApproved = true;
        Close();
    }
}
