using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Fusebox.Services;
using Fusebox.ViewModels;
using Fusebox.Views;

namespace Fusebox;

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
