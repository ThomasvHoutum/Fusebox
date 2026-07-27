using System.Collections.ObjectModel;
using MCPLauncher.Commands;
using MCPLauncher.Models;
using MCPLauncher.Services;

namespace MCPLauncher.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IConfigurationService _configuration;
    private readonly IManagedProcessService _processes;
    private readonly IFileLauncher _fileLauncher;
    private readonly IUserInteractionService _interactions;
    private ServerRuntime? _selectedServer;
    private string? _configurationWarning;
    private string _status = "Loading configuration…";
    private bool _isBusy;

    public MainWindowViewModel(
        IConfigurationService configuration,
        IManagedProcessService processes,
        IFileLauncher fileLauncher,
        IUserInteractionService interactions)
    {
        _configuration = configuration;
        _processes = processes;
        _fileLauncher = fileLauncher;
        _interactions = interactions;

        OpenConfigurationCommand = new AsyncCommand(OpenConfigurationAsync);
        RefreshCommand = new AsyncCommand(() => RefreshConfigurationAsync(false));
        StartCommand = new AsyncCommand<ServerRuntime>(StartAsync);
        StopCommand = new AsyncCommand<ServerRuntime>(server => RunExclusiveAsync(() => StopWithEscalationAsync(server, "Stopped by user.")));
        RestartCommand = new AsyncCommand<ServerRuntime>(RestartAsync);
    }

    public ObservableCollection<ServerRuntime> Servers { get; } = [];

    public AsyncCommand OpenConfigurationCommand { get; }

    public AsyncCommand RefreshCommand { get; }

    public AsyncCommand<ServerRuntime> StartCommand { get; }

    public AsyncCommand<ServerRuntime> StopCommand { get; }

    public AsyncCommand<ServerRuntime> RestartCommand { get; }

    public ServerRuntime? SelectedServer
    {
        get => _selectedServer;
        set => SetField(ref _selectedServer, value);
    }

    public string? ConfigurationWarning
    {
        get => _configurationWarning;
        private set
        {
            if (SetField(ref _configurationWarning, value))
            {
                OnPropertyChanged(nameof(HasConfigurationWarning));
            }
        }
    }

    public bool HasConfigurationWarning => ConfigurationWarning is not null;

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public bool IsEmpty => Servers.Count == 0;

    public bool HasServers => !IsEmpty;

    public string ServerCountDisplay => Servers.Count == 1 ? "1 server" : $"{Servers.Count} servers";

    public async Task InitializeAsync()
    {
        try
        {
            var created = await _configuration.EnsureConfigurationExistsAsync();
            await RefreshConfigurationAsync(true);
            if (created)
            {
                Status = "Configuration created. Add a server, save the file, and press Refresh.";
            }
        }
        catch (Exception exception)
        {
            ConfigurationWarning = $"Configuration setup failed: {exception.Message}";
            Status = "MCP Launcher could not prepare its configuration file.";
        }
    }

    public async Task<bool> TryCloseAsync()
    {
        if (_isBusy)
        {
            return false;
        }

        var running = Servers.Where(IsRunning).ToList();
        if (running.Count == 0)
        {
            return true;
        }

        var names = string.Join(Environment.NewLine, running.Select(server => $"• {server.Title}"));
        var confirmed = await _interactions.ConfirmAsync(
            "Close MCP Launcher?",
            $"Closing will stop these managed servers:\n\n{names}",
            "Stop servers and close",
            "Cancel");
        if (!confirmed)
        {
            return false;
        }

        _isBusy = true;
        try
        {
            foreach (var server in running)
            {
                if (!await StopWithEscalationAsync(server, "Terminated during application shutdown."))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task OpenConfigurationAsync()
    {
        try
        {
            _fileLauncher.Open(_configuration.ConfigurationPath);
            Status = $"Opened {_configuration.ConfigurationPath}";
        }
        catch (Exception exception)
        {
            await _interactions.ShowMessageAsync("Could not open configuration", exception.Message, "Close");
        }
    }

    private async Task StartAsync(ServerRuntime server)
    {
        await RunExclusiveAsync(async () =>
        {
            var latest = await ReadLatestDefinitionAsync(server);
            if (latest is null)
            {
                return;
            }

            server.ApplyConfiguration(latest, null);
            _processes.Start(server, latest);
            SelectedServer = server;
            Status = server.State == ServerState.Running
                ? $"Started {server.Title}."
                : $"{server.Title} failed to start. Open its run for details.";
        });
    }

    private async Task RestartAsync(ServerRuntime server)
    {
        await RunExclusiveAsync(async () =>
        {
            var latest = await ReadLatestDefinitionAsync(server);
            if (latest is null)
            {
                return;
            }

            if (!await StopWithEscalationAsync(server, "Restarted by user."))
            {
                Status = $"Restart of {server.Title} was canceled.";
                return;
            }

            server.ApplyConfiguration(latest, null);
            _processes.Start(server, latest);
            SelectedServer = server;
            Status = $"Restarted {server.Title}.";
        });
    }

    private async Task<ServerDefinition?> ReadLatestDefinitionAsync(ServerRuntime server)
    {
        var result = await _configuration.LoadAsync();
        if (!result.IsReadable)
        {
            SetActionsBlocked(true);
            ConfigurationWarning = string.Join(Environment.NewLine, result.GlobalErrors);
            Status = "Start and Restart are blocked until the configuration can be read and refreshed.";
            return null;
        }

        var matches = result.Entries.Where(entry => entry.Id == server.Id).ToList();
        var entry = matches.Count == 1 ? matches[0] : null;
        if (entry?.Definition is null || entry.Error is not null)
        {
            var error = entry?.Error ?? $"The entry '{server.Id}' no longer exists or is not uniquely identifiable.";
            server.ApplyConfiguration(server.Definition, error);
            ConfigurationWarning = error;
            Status = $"{server.Title} was not started because its latest configuration is invalid.";
            return null;
        }

        return entry.Definition;
    }

    private Task RefreshConfigurationAsync(bool initialLoad) =>
        RunExclusiveAsync(() => RefreshCoreAsync(initialLoad));

    private async Task RefreshCoreAsync(bool initialLoad)
    {
        var result = await _configuration.LoadAsync();
        if (!result.IsReadable)
        {
            SetActionsBlocked(true);
            ConfigurationWarning = string.Join(Environment.NewLine, result.GlobalErrors);
            Status = Servers.Count == 0
                ? "Configuration could not be loaded. Open the file to correct it."
                : "Configuration could not be loaded. Showing the last successfully loaded catalog; Start and Restart are blocked.";
            return;
        }

        var entries = CollapseEntries(result.Entries);
        var incomingIds = entries.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        var removed = Servers.Where(server => !incomingIds.Contains(server.Id)).ToList();
        var removedRunning = removed.Where(IsRunning).ToList();

        if (removedRunning.Count > 0 && !await ConfirmRemovedServersAsync(removedRunning))
        {
            Status = "Refresh canceled. The catalog and running servers were not changed.";
            return;
        }

        foreach (var server in removedRunning)
        {
            if (!await StopWithEscalationAsync(server, "Removed from configuration."))
            {
                Status = $"Refresh stopped because {server.Title} is still running.";
                return;
            }
        }

        foreach (var server in removed)
        {
            Servers.Remove(server);
        }

        var ordered = entries.Select(ApplyEntry).ToList();
        Servers.Clear();
        foreach (var server in ordered)
        {
            Servers.Add(server);
        }

        SetActionsBlocked(false);
        var errors = result.GlobalErrors
            .Concat(entries.Where(entry => entry.Error is not null).Select(entry => $"{entry.Id}: {entry.Error}"))
            .ToList();
        ConfigurationWarning = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null;
        SelectedServer = SelectedServer is null
            ? Servers.FirstOrDefault()
            : Servers.FirstOrDefault(server => server.Id == SelectedServer.Id) ?? Servers.FirstOrDefault();
        RaiseCollectionProperties();
        Status = errors.Count > 0
            ? "Configuration refreshed with one or more errors. Valid entries remain available."
            : initialLoad ? "Configuration loaded." : "Configuration refreshed.";
    }

    private ServerRuntime ApplyEntry(ConfigurationEntry entry)
    {
        var server = Servers.FirstOrDefault(existing => existing.Id == entry.Id);
        if (server is null)
        {
            return new ServerRuntime(entry.Id, entry.Definition, entry.Error);
        }

        server.ApplyConfiguration(entry.Definition, entry.Error);
        return server;
    }

    private async Task<bool> ConfirmRemovedServersAsync(IEnumerable<ServerRuntime> servers)
    {
        var names = string.Join(Environment.NewLine, servers.Select(server => $"• {server.Title}"));
        return await _interactions.ConfirmAsync(
            "Refresh will stop running servers",
            $"These servers were removed from the configuration and must be stopped before their rows can be removed:\n\n{names}",
            "Stop and refresh",
            "Cancel");
    }

    private async Task<bool> StopWithEscalationAsync(ServerRuntime server, string endReason)
    {
        var result = await _processes.StopAsync(server, endReason);
        if (result is StopResult.Stopped or StopResult.NotRunning)
        {
            Status = $"Stopped {server.Title}.";
            return true;
        }

        var force = await _interactions.ConfirmAsync(
            $"{server.Title} did not stop",
            "The process resisted termination. Force quit terminates its process tree. Cancel leaves it running.",
            "Force quit",
            "Cancel");
        if (!force)
        {
            server.State = ServerState.Running;
            return false;
        }

        var stopped = await _processes.ForceQuitAsync(server, endReason);
        if (!stopped)
        {
            await _interactions.ShowMessageAsync(
                "Force quit failed",
                $"{server.Title} is still running. Stop it through the operating system before closing MCP Launcher.",
                "Close");
        }

        return stopped;
    }

    private async Task RunExclusiveAsync(Func<Task> action)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            await action();
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void SetActionsBlocked(bool blocked)
    {
        foreach (var server in Servers)
        {
            server.ActionsBlocked = blocked;
        }
    }

    private void RaiseCollectionProperties()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasServers));
        OnPropertyChanged(nameof(ServerCountDisplay));
    }

    private static bool IsRunning(ServerRuntime server) =>
        server.State is ServerState.Running or ServerState.Stopping;

    private static IReadOnlyList<ConfigurationEntry> CollapseEntries(IReadOnlyList<ConfigurationEntry> entries) =>
        entries
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(group => group.Count() == 1
                ? group.Single()
                : new ConfigurationEntry(group.Key, null, $"The key '{group.Key}' is duplicated. Every server key must be unique."))
            .ToList();
}
