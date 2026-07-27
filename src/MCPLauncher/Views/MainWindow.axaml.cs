using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MCPLauncher.Models;
using MCPLauncher.Services;

namespace MCPLauncher.Views;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ConfigurationService _configurationService = new();
    private readonly ManagedProcessService _processService = new();
    private ServerRuntime? _selectedServer;
    private bool _isClosingApproved;
    private bool _isBusy;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ServerRuntime> Servers { get; } = [];

    public ServerRuntime? SelectedServer
    {
        get => _selectedServer;
        set => SetField(ref _selectedServer, value);
    }

    public string ServerCountDisplay => Servers.Count == 1 ? "1 server" : $"{Servers.Count} servers";

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        try
        {
            var created = await _configurationService.EnsureConfigurationExistsAsync();
            await RefreshConfigurationAsync(isInitialLoad: true);
            if (created)
            {
                SetStatus("Configuration created. Add a server, save the file, and press Refresh.");
            }
        }
        catch (Exception exception)
        {
            SetConfigurationWarning($"Configuration setup failed: {exception.Message}");
            SetStatus("MCP Launcher could not prepare its configuration file.");
        }
    }

    private void OnOpenConfigurationClick(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            _configurationService.OpenConfigurationFile();
            SetStatus($"Opened {_configurationService.ConfigurationPath}");
        }
        catch (Exception exception)
        {
            _ = ShowMessageAsync("Could not open configuration", exception.Message, "Close");
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs eventArgs) =>
        await RefreshConfigurationAsync(isInitialLoad: false);

    private async void OnStartClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { CommandParameter: ServerRuntime server } || _isBusy)
        {
            return;
        }

        await StartAsync(server);
    }

    private async void OnStopClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { CommandParameter: ServerRuntime server } || _isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            await StopWithEscalationAsync(server, "Stopped by user.");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async void OnRestartClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { CommandParameter: ServerRuntime server } || _isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            var latest = await ReadLatestDefinitionAsync(server);
            if (latest is null)
            {
                return;
            }

            var stopped = await StopWithEscalationAsync(server, "Restarted by user.");
            if (!stopped)
            {
                SetStatus($"Restart of {server.Title} was canceled.");
                return;
            }

            server.ApplyConfiguration(latest, null);
            _processService.Start(server, latest);
            SelectedServer = server;
            SetStatus($"Restarted {server.Title}.");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task StartAsync(ServerRuntime server)
    {
        _isBusy = true;
        try
        {
            var latest = await ReadLatestDefinitionAsync(server);
            if (latest is null)
            {
                return;
            }

            server.ApplyConfiguration(latest, null);
            _processService.Start(server, latest);
            SelectedServer = server;
            SetStatus(server.State == ServerState.Running
                ? $"Started {server.Title}."
                : $"{server.Title} failed to start. Open its run for details.");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task<ServerDefinition?> ReadLatestDefinitionAsync(ServerRuntime server)
    {
        var result = await _configurationService.LoadAsync();
        if (!result.IsReadable)
        {
            SetActionsBlocked(true);
            SetConfigurationWarning(string.Join(Environment.NewLine, result.GlobalErrors));
            SetStatus("Start and Restart are blocked until the configuration can be read and refreshed.");
            return null;
        }

        var matches = result.Entries.Where(entry => entry.Id == server.Id).ToList();
        var entry = matches.Count == 1 ? matches[0] : null;
        if (entry?.Definition is null || entry.Error is not null)
        {
            var error = entry?.Error ?? $"The entry '{server.Id}' no longer exists or is not uniquely identifiable.";
            server.ApplyConfiguration(server.Definition, error);
            SetConfigurationWarning(error);
            SetStatus($"{server.Title} was not started because its latest configuration is invalid.");
            return null;
        }

        return entry.Definition;
    }

    private async Task RefreshConfigurationAsync(bool isInitialLoad)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            var result = await _configurationService.LoadAsync();
            if (!result.IsReadable)
            {
                SetActionsBlocked(true);
                var message = string.Join(Environment.NewLine, result.GlobalErrors);
                SetConfigurationWarning(message);
                SetStatus(Servers.Count == 0
                    ? "Configuration could not be loaded. Open the file to correct it."
                    : "Configuration could not be loaded. Showing the last successfully loaded catalog; Start and Restart are blocked.");
                return;
            }

            var entries = CollapseEntries(result.Entries);
            var incomingIds = entries.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
            var removed = Servers.Where(server => !incomingIds.Contains(server.Id)).ToList();
            var removedRunning = removed.Where(server => server.State is ServerState.Running or ServerState.Stopping).ToList();

            if (removedRunning.Count > 0)
            {
                var names = string.Join(Environment.NewLine, removedRunning.Select(server => $"• {server.Title}"));
                var confirmed = await ShowDecisionAsync(
                    "Refresh will stop running servers",
                    $"These servers were removed from the configuration and must be stopped before their rows can be removed:\n\n{names}",
                    "Stop and refresh",
                    "Cancel");
                if (!confirmed)
                {
                    SetStatus("Refresh canceled. The catalog and running servers were not changed.");
                    return;
                }

                foreach (var server in removedRunning)
                {
                    if (!await StopWithEscalationAsync(server, "Removed from configuration."))
                    {
                        SetStatus($"Refresh stopped because {server.Title} is still running.");
                        return;
                    }
                }
            }

            foreach (var server in removed)
            {
                Servers.Remove(server);
            }

            var ordered = new List<ServerRuntime>();
            foreach (var entry in entries)
            {
                var server = Servers.FirstOrDefault(existing => existing.Id == entry.Id);
                if (server is null)
                {
                    server = new ServerRuntime(entry.Id, entry.Definition, entry.Error);
                }
                else
                {
                    server.ApplyConfiguration(entry.Definition, entry.Error);
                }

                ordered.Add(server);
            }

            Servers.Clear();
            foreach (var server in ordered)
            {
                Servers.Add(server);
            }

            SetActionsBlocked(false);
            var errors = result.GlobalErrors
                .Concat(entries.Where(entry => entry.Error is not null).Select(entry => $"{entry.Id}: {entry.Error}"))
                .ToList();
            SetConfigurationWarning(errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null);
            SelectedServer = SelectedServer is null
                ? Servers.FirstOrDefault()
                : Servers.FirstOrDefault(server => server.Id == SelectedServer.Id) ?? Servers.FirstOrDefault();
            UpdateCollectionDisplay();
            SetStatus(errors.Count > 0
                ? "Configuration refreshed with one or more errors. Valid entries remain available."
                : isInitialLoad ? "Configuration loaded." : "Configuration refreshed.");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task<bool> StopWithEscalationAsync(ServerRuntime server, string endReason)
    {
        var result = await _processService.StopAsync(server, endReason);
        if (result is StopResult.Stopped or StopResult.NotRunning)
        {
            SetStatus($"Stopped {server.Title}.");
            return true;
        }

        var force = await ShowDecisionAsync(
            $"{server.Title} did not stop",
            "The process resisted termination. Force quit terminates its process tree. Cancel leaves it running.",
            "Force quit",
            "Cancel");
        if (!force)
        {
            server.State = ServerState.Running;
            return false;
        }

        var stopped = await _processService.ForceQuitAsync(server, endReason);
        if (!stopped)
        {
            await ShowMessageAsync("Force quit failed", $"{server.Title} is still running. Stop it through the operating system before closing MCP Launcher.", "Close");
        }

        return stopped;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs eventArgs)
    {
        if (_isClosingApproved)
        {
            return;
        }

        eventArgs.Cancel = true;
        if (_isBusy)
        {
            return;
        }

        var running = Servers.Where(server => server.State is ServerState.Running or ServerState.Stopping).ToList();
        if (running.Count == 0)
        {
            _isClosingApproved = true;
            Close();
            return;
        }

        var names = string.Join(Environment.NewLine, running.Select(server => $"• {server.Title}"));
        var confirmed = await ShowDecisionAsync(
            "Close MCP Launcher?",
            $"Closing will stop these managed servers:\n\n{names}",
            "Stop servers and close",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        _isBusy = true;
        try
        {
            foreach (var server in running)
            {
                if (!await StopWithEscalationAsync(server, "Terminated during application shutdown."))
                {
                    return;
                }
            }

            _isClosingApproved = true;
            Close();
        }
        finally
        {
            _isBusy = false;
        }
    }

    private static IReadOnlyList<ConfigurationEntry> CollapseEntries(IReadOnlyList<ConfigurationEntry> entries) =>
        entries
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(group => group.Count() == 1
                ? group.Single()
                : new ConfigurationEntry(group.Key, null, $"The key '{group.Key}' is duplicated. Every server key must be unique."))
            .ToList();

    private void SetConfigurationWarning(string? message)
    {
        ConfigurationWarningIcon.Text = message is null ? string.Empty : "⚠";
        ToolTip.SetTip(ConfigurationWarningIcon, message);
    }

    private void SetActionsBlocked(bool blocked)
    {
        foreach (var server in Servers)
        {
            server.ActionsBlocked = blocked;
        }
    }

    private void UpdateCollectionDisplay()
    {
        EmptyState.IsVisible = Servers.Count == 0;
        ServerList.IsVisible = Servers.Count > 0;
        OnPropertyChanged(nameof(ServerCountDisplay));
    }

    private void SetStatus(string message) => StatusText.Text = message;

    private Task<bool> ShowDecisionAsync(string title, string message, string confirmText, string cancelText) =>
        new DecisionDialog(title, message, confirmText, cancelText).ShowDialog<bool>(this);

    private Task<bool> ShowMessageAsync(string title, string message, string closeText) =>
        new DecisionDialog(title, message, closeText).ShowDialog<bool>(this);

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
