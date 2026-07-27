using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MCPLauncher.Services;
using MCPLauncher.ViewModels;
using MCPLauncher.Views;

namespace MCPLauncher;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow? mainWindow = null;
            var viewModel = new MainWindowViewModel(
                new ConfigurationService(),
                new ManagedProcessService(),
                new SystemFileLauncher(),
                new AvaloniaUserInteractionService(() => mainWindow!));
            mainWindow = new MainWindow(viewModel);
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
