using System.Collections.ObjectModel;
using System.ComponentModel;
using Fusebox.Commands;
using Fusebox.Models;
using Fusebox.Services;

namespace Fusebox.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IConfigurationService _configuration;
    private readonly IManagedProcessService _processes;
    private readonly IFileLauncher _fileLauncher;
    private readonly IUserInteractionService _interactions;
    private ServerRuntime? _selectedServer;
    private ProfileRuntime? _selectedProfile;
    private CatalogNavigationItem? _selectedNavigationItem;
    private CatalogFilterOption? _selectedCatalogFilter;
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
        RefreshCommand = new AsyncCommand(RefreshAsync);
        StartCommand = new AsyncCommand<ServerRuntime>(StartAsync);
        StopCommand = new AsyncCommand<ServerRuntime>(
            server => RunExclusiveAsync(() => StopWithEscalationAsync(server, "Stopped by user.")));
        RestartCommand = new AsyncCommand<ServerRuntime>(RestartAsync);
        StartAllCommand = new AsyncCommand(StartAllAsync, () => CanStartAll);
        StopAllCommand = new AsyncCommand(StopAllAsync, () => CanStopAll);
        RestartAllCommand = new AsyncCommand(RestartAllAsync, () => CanRestartAll);
        NavigateToServerCommand = new AsyncCommand<ServerRuntime>(server =>
        {
            NavigateToServer(server);
            return Task.CompletedTask;
        });

        CatalogFilters =
        [
            new CatalogFilterOption(CatalogFilter.All, "All"),
            new CatalogFilterOption(CatalogFilter.Servers, "Servers"),
            new CatalogFilterOption(CatalogFilter.Profiles, "Profiles")
        ];
        _selectedCatalogFilter = CatalogFilters[0];
    }

    public ObservableCollection<ServerRuntime> Servers { get; } = [];

    public ObservableCollection<ServerRuntime> VisibleServers { get; } = [];

    public ObservableCollection<ProfileRuntime> Profiles { get; } = [];

    public ObservableCollection<CatalogNavigationItem> NavigationItems { get; } = [];

    public ObservableCollection<CatalogNavigationItem> VisibleNavigationItems { get; } = [];

    public IReadOnlyList<CatalogFilterOption> CatalogFilters { get; }

    public AsyncCommand OpenConfigurationCommand { get; }
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand<ServerRuntime> StartCommand { get; }
    public AsyncCommand<ServerRuntime> StopCommand { get; }
    public AsyncCommand<ServerRuntime> RestartCommand { get; }
    public AsyncCommand StartAllCommand { get; }
    public AsyncCommand StopAllCommand { get; }
    public AsyncCommand RestartAllCommand { get; }
    public AsyncCommand<ServerRuntime> NavigateToServerCommand { get; }

    public ServerRuntime? SelectedServer
    {
        get => _selectedServer;
        set
        {
            value?.SelectLatestRun();
            SetField(ref _selectedServer, value);
        }
    }

    public CatalogNavigationItem? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (!SetField(ref _selectedNavigationItem, value))
            {
                return;
            }

            _selectedProfile = value?.Profile;
            OnPropertyChanged(nameof(SelectedProfile));
            if (value?.Server is { } server)
            {
                server.SelectLatestRun();
                _selectedServer = server;
                OnPropertyChanged(nameof(SelectedServer));
            }

            OnPropertyChanged(nameof(IsServerWorkspaceVisible));
            OnPropertyChanged(nameof(IsProfileWorkspaceVisible));
            UpdateProfileScope();
        }
    }

    public CatalogFilterOption? SelectedCatalogFilter
    {
        get => _selectedCatalogFilter;
        set
        {
            if (SetField(ref _selectedCatalogFilter, value))
            {
                ApplyCatalogFilter();
            }
        }
    }

    public bool IsServerWorkspaceVisible => SelectedNavigationItem?.IsServer == true;
    public bool IsProfileWorkspaceVisible => SelectedNavigationItem?.IsProfile == true;

    public ProfileRuntime? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (value is null)
            {
                return;
            }

            SelectedNavigationItem = NavigationItems.FirstOrDefault(item => ReferenceEquals(item.Profile, value));
        }
    }

    public bool HasNamedProfiles => Profiles.Count > 1;

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
    public bool HasVisibleNavigationItems => VisibleNavigationItems.Count > 0;
    public bool HasNoVisibleNavigationItems => !HasVisibleNavigationItems;
    public bool IsProfileEmpty => IsProfileWorkspaceVisible && VisibleServers.Count == 0;
    public string CatalogCountDisplay
    {
        get
        {
            var profiles = Profiles.Count;
            var servers = Servers.Count;
            return $"{profiles} {(profiles == 1 ? "profile" : "profiles")} · {servers} {(servers == 1 ? "server" : "servers")}";
        }
    }

    public string ProfileCountSummary
    {
        get
        {
            var running = BulkScope.Count(server => server.State is ServerState.Running or ServerState.Stopping);
            var stopped = BulkScope.Count - running;
            return $"{running} running · {stopped} stopped";
        }
    }

    public bool CanStartAll => !_isBusy && IsProfileWorkspaceVisible && BulkScope.Any(server => server.CanStart);
    public bool CanStopAll => !_isBusy && IsProfileWorkspaceVisible && BulkScope.Any(server => server.CanStop);
    public bool CanRestartAll => !_isBusy && IsProfileWorkspaceVisible && BulkScope.Any(server => server.CanRestart || server.CanStart);
    public string StartAllToolTip => CanStartAll ? "Start every stopped or faulted server in this scope." : "No included server can currently start.";
    public string StopAllToolTip => CanStopAll ? "Stop every running server in this scope." : "No included server is running.";
    public string RestartAllToolTip => CanRestartAll
        ? "Restart running servers and start stopped or faulted servers."
        : "No included server can currently restart or start.";

    private IReadOnlyList<ServerRuntime> BulkScope => SelectedProfile?.Servers ?? [];

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
            Status = "Fusebox could not prepare its configuration file.";
        }
    }

    public Task RefreshAsync() => RefreshConfigurationAsync(false);

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

        if (!await ConfirmServersAsync(
                "Close Fusebox?",
                "Closing will stop these managed servers:",
                running,
                "Stop servers and close"))
        {
            return false;
        }

        _isBusy = true;
        RaiseBulkState();
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
            RaiseBulkState();
        }
    }

    public Task StartAllAsync() => RunExclusiveAsync(() => RunBulkAsync(BulkOperation.Start));
    public Task StopAllAsync() => RunExclusiveAsync(() => RunBulkAsync(BulkOperation.Stop));
    public Task RestartAllAsync() => RunExclusiveAsync(() => RunBulkAsync(BulkOperation.Restart));

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

            StartServer(server, latest);
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

            StartServer(server, latest);
            SelectedServer = server;
            Status = server.State == ServerState.Running
                ? $"Restarted {server.Title}."
                : $"{server.Title} failed to restart. Open its run for details.";
        });
    }

    private async Task RunBulkAsync(BulkOperation operation)
    {
        var scope = BulkScope.DistinctBy(server => server.Id, StringComparer.Ordinal).ToList();
        if (scope.Count == 0)
        {
            Status = $"{ScopeTitle()} contains no applicable servers.";
            return;
        }

        Dictionary<string, ServerDefinition>? latest = null;
        if (operation is BulkOperation.Start or BulkOperation.Restart)
        {
            latest = await ReadLatestBulkDefinitionsAsync(scope);
            if (latest is null)
            {
                return;
            }
        }

        var running = scope.Where(server => server.CanStop).ToList();
        if (operation is BulkOperation.Stop or BulkOperation.Restart &&
            running.Count > 1 &&
            !await ConfirmServersAsync(
                operation == BulkOperation.Stop ? $"Stop {ScopeTitle()}?" : $"Restart {ScopeTitle()}?",
                operation == BulkOperation.Stop
                    ? "The following running servers will be stopped:"
                    : "The following running servers will be stopped and started again:",
                running,
                operation == BulkOperation.Stop ? "Stop all" : "Restart all"))
        {
            Status = $"{OperationName(operation)} canceled.";
            return;
        }

        var changed = 0;
        var skipped = 0;
        var failed = 0;
        foreach (var server in scope)
        {
            if (operation == BulkOperation.Start)
            {
                if (!server.CanStart || latest is null || !latest.TryGetValue(server.Id, out var definition))
                {
                    skipped++;
                    continue;
                }

                StartServer(server, definition);
                if (server.State == ServerState.Running) changed++; else failed++;
            }
            else if (operation == BulkOperation.Stop)
            {
                if (!server.CanStop)
                {
                    skipped++;
                    continue;
                }

                if (await StopWithEscalationAsync(server, "Stopped by profile bulk action.")) changed++; else failed++;
            }
            else
            {
                if (latest is null || !latest.TryGetValue(server.Id, out var definition) ||
                    (!server.CanRestart && !server.CanStart))
                {
                    skipped++;
                    continue;
                }

                if (server.CanRestart &&
                    !await StopWithEscalationAsync(server, "Restarted by profile bulk action."))
                {
                    failed++;
                    continue;
                }

                StartServer(server, definition);
                if (server.State == ServerState.Running) changed++; else failed++;
            }
        }

        Status = FormatBulkSummary(operation, changed, skipped, failed);
        RaiseBulkState();
    }

    private async Task<Dictionary<string, ServerDefinition>?> ReadLatestBulkDefinitionsAsync(
        IReadOnlyCollection<ServerRuntime> scope)
    {
        var result = await _configuration.LoadAsync();
        if (!result.IsReadable)
        {
            BlockLaunchActions(result.GlobalErrors);
            Status = "Bulk start and restart are blocked because the configuration could not be read. No running servers were stopped.";
            return null;
        }

        if (SelectedProfile?.Id is string profileId)
        {
            var profileMatches = result.ProfileEntries.Where(entry => entry.Id == profileId).ToList();
            if (profileMatches.Count != 1 || !profileMatches[0].IsValid)
            {
                var error = profileMatches.SingleOrDefault()?.Error ?? $"Profile '{profileId}' no longer exists.";
                ConfigurationWarning = error;
                Status = "The selected profile is invalid or was removed. No running servers were stopped.";
                return null;
            }
        }

        var entries = CollapseEntries(result.Entries);
        var definitions = entries
            .Where(entry => entry.IsValid)
            .ToDictionary(entry => entry.Id, entry => entry.Definition!, StringComparer.Ordinal);
        var errors = new List<string>();
        foreach (var server in scope)
        {
            if (definitions.TryGetValue(server.Id, out var definition))
            {
                server.ApplyConfiguration(definition, null);
            }
            else
            {
                var error = entries.FirstOrDefault(entry => entry.Id == server.Id)?.Error
                    ?? $"The entry '{server.Id}' no longer exists.";
                server.ApplyConfiguration(server.Definition, error);
                errors.Add($"{server.Title}: {error}");
            }
        }

        if (errors.Count > 0)
        {
            ConfigurationWarning = string.Join(Environment.NewLine, errors);
        }

        return definitions;
    }

    private void StartServer(ServerRuntime server, ServerDefinition definition)
    {
        server.ApplyConfiguration(definition, null);
        _processes.Start(server, definition);
    }

    private async Task<ServerDefinition?> ReadLatestDefinitionAsync(ServerRuntime server)
    {
        var result = await _configuration.LoadAsync();
        if (!result.IsReadable)
        {
            BlockLaunchActions(result.GlobalErrors);
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
            BlockLaunchActions(result.GlobalErrors);
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
            Unsubscribe(server);
            Servers.Remove(server);
        }

        var ordered = entries.Select(ApplyEntry).ToList();
        Servers.Clear();
        foreach (var server in ordered)
        {
            Servers.Add(server);
        }

        SetActionsBlocked(false);
        RebuildProfiles(result.ProfileEntries);
        var errors = result.GlobalErrors
            .Concat(entries.Where(entry => entry.Error is not null).Select(entry => $"{entry.Id}: {entry.Error}"))
            .Concat(result.ProfileEntries.Where(entry => entry.Error is not null).Select(entry => $"{entry.Id}: {entry.Error}"))
            .Concat(result.ProfileEntries
                .Where(entry => entry.Definition?.Warnings.Count > 0)
                .SelectMany(entry => entry.Definition!.Warnings.Select(warning => $"{entry.Id}: {warning}")))
            .ToList();
        ConfigurationWarning = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null;
        RebuildNavigation();
        Status = errors.Count > 0
            ? "Configuration refreshed with one or more warnings. Valid servers and profiles remain available."
            : initialLoad ? "Configuration loaded." : "Configuration refreshed.";
    }

    private ServerRuntime ApplyEntry(ConfigurationEntry entry)
    {
        var server = Servers.FirstOrDefault(existing => existing.Id == entry.Id);
        if (server is null)
        {
            server = new ServerRuntime(entry.Id, entry.Definition, entry.Error);
            Subscribe(server);
            return server;
        }

        server.ApplyConfiguration(entry.Definition, entry.Error);
        return server;
    }

    private void RebuildProfiles(IReadOnlyList<ProfileEntry> entries)
    {
        var selectedId = SelectedProfile?.Id;
        Profiles.Clear();
        Profiles.Add(new ProfileRuntime(
            null,
            "All servers",
            "The complete configured server catalog.",
            Servers.Select(server => server.Id).ToList(),
            [],
            Servers.ToList()));
        foreach (var definition in entries.Where(entry => entry.IsValid).Select(entry => entry.Definition!))
        {
            var ids = definition.ServerIds.ToHashSet(StringComparer.Ordinal);
            Profiles.Add(new ProfileRuntime(
                definition.Id,
                definition.Title,
                definition.Description,
                definition.ServerIds,
                definition.Warnings,
                Servers.Where(server => ids.Contains(server.Id)).ToList()));
        }

        OnPropertyChanged(nameof(HasNamedProfiles));
        _selectedProfile = Profiles.FirstOrDefault(profile => profile.Id == selectedId) ?? Profiles[0];
        OnPropertyChanged(nameof(SelectedProfile));
    }

    private void RebuildNavigation()
    {
        var selectedKey = SelectedNavigationItem?.Key;
        NavigationItems.Clear();
        foreach (var profile in Profiles)
        {
            NavigationItems.Add(new CatalogNavigationItem(profile));
        }
        foreach (var server in Servers)
        {
            NavigationItems.Add(new CatalogNavigationItem(server));
        }

        ApplyCatalogFilter();
        SelectedNavigationItem =
            VisibleNavigationItems.FirstOrDefault(item => item.Key == selectedKey) ??
            VisibleNavigationItems.FirstOrDefault(item => ReferenceEquals(item.Profile, _selectedProfile)) ??
            VisibleNavigationItems.FirstOrDefault();
        OnPropertyChanged(nameof(CatalogCountDisplay));
    }

    private void ApplyCatalogFilter()
    {
        var filter = SelectedCatalogFilter?.Value ?? CatalogFilter.All;
        VisibleNavigationItems.Clear();
        foreach (var item in NavigationItems.Where(item =>
                     filter == CatalogFilter.All ||
                     filter == CatalogFilter.Profiles && item.IsProfile ||
                     filter == CatalogFilter.Servers && item.IsServer))
        {
            VisibleNavigationItems.Add(item);
        }

        if (NavigationItems.Count > 0 &&
            (SelectedNavigationItem is null || !VisibleNavigationItems.Contains(SelectedNavigationItem)))
        {
            SelectedNavigationItem = VisibleNavigationItems.FirstOrDefault();
        }

        RaiseCollectionProperties();
    }

    private void UpdateProfileScope()
    {
        VisibleServers.Clear();
        foreach (var server in BulkScope)
        {
            VisibleServers.Add(server);
        }
        RaiseCollectionProperties();
        RaiseBulkState();
    }

    public void NavigateToServer(ServerRuntime server)
    {
        SelectedCatalogFilter = CatalogFilters.First(option => option.Value == CatalogFilter.All);
        SelectedNavigationItem = NavigationItems.FirstOrDefault(item => ReferenceEquals(item.Server, server));
    }

    private async Task<bool> ConfirmRemovedServersAsync(IReadOnlyCollection<ServerRuntime> servers) =>
        await ConfirmServersAsync(
            "Refresh will stop running servers",
            "These servers were removed from the configuration and must be stopped before their rows can be removed:",
            servers,
            "Stop and refresh");

    private async Task<bool> ConfirmServersAsync(
        string title,
        string message,
        IEnumerable<ServerRuntime> servers,
        string confirmText)
    {
        var names = string.Join(Environment.NewLine, servers.Select(server => $"• {server.Title}"));
        return await _interactions.ConfirmAsync(title, $"{message}\n\n{names}", confirmText, "Cancel");
    }

    private async Task<bool> StopWithEscalationAsync(ServerRuntime server, string endReason)
    {
        var result = await _processes.StopAsync(server, endReason);
        if (result is StopResult.Stopped or StopResult.NotRunning)
        {
            RaiseBulkState();
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
                $"{server.Title} is still running. Stop it through the operating system before closing Fusebox.",
                "Close");
        }

        RaiseBulkState();
        return stopped;
    }

    private async Task RunExclusiveAsync(Func<Task> action)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        RaiseBulkState();
        try
        {
            await action();
        }
        finally
        {
            _isBusy = false;
            RaiseBulkState();
        }
    }

    private void BlockLaunchActions(IEnumerable<string> errors)
    {
        SetActionsBlocked(true);
        ConfigurationWarning = string.Join(Environment.NewLine, errors);
    }

    private void SetActionsBlocked(bool blocked)
    {
        foreach (var server in Servers)
        {
            server.ActionsBlocked = blocked;
        }
        RaiseBulkState();
    }

    private void Subscribe(ServerRuntime server) => server.PropertyChanged += OnServerPropertyChanged;
    private void Unsubscribe(ServerRuntime server) => server.PropertyChanged -= OnServerPropertyChanged;

    private void OnServerPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(ServerRuntime.State) or nameof(ServerRuntime.CanStart) or
            nameof(ServerRuntime.CanStop) or nameof(ServerRuntime.CanRestart))
        {
            RaiseBulkState();
        }
    }

    private void RaiseCollectionProperties()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasServers));
        OnPropertyChanged(nameof(IsProfileEmpty));
        OnPropertyChanged(nameof(CatalogCountDisplay));
        OnPropertyChanged(nameof(HasVisibleNavigationItems));
        OnPropertyChanged(nameof(HasNoVisibleNavigationItems));
        OnPropertyChanged(nameof(ProfileCountSummary));
    }

    private void RaiseBulkState()
    {
        OnPropertyChanged(nameof(ProfileCountSummary));
        OnPropertyChanged(nameof(CanStartAll));
        OnPropertyChanged(nameof(CanStopAll));
        OnPropertyChanged(nameof(CanRestartAll));
        OnPropertyChanged(nameof(StartAllToolTip));
        OnPropertyChanged(nameof(StopAllToolTip));
        OnPropertyChanged(nameof(RestartAllToolTip));
        StartAllCommand.RaiseCanExecuteChanged();
        StopAllCommand.RaiseCanExecuteChanged();
        RestartAllCommand.RaiseCanExecuteChanged();
    }

    private string ScopeTitle() => SelectedProfile?.Title ?? "All servers";

    private string FormatBulkSummary(BulkOperation operation, int changed, int skipped, int failed)
    {
        var verb = operation switch
        {
            BulkOperation.Start => "started",
            BulkOperation.Stop => "stopped",
            _ => "restarted"
        };
        var skippedLabel = operation switch
        {
            BulkOperation.Start => "already running or unavailable",
            BulkOperation.Stop => "already stopped or unavailable",
            _ => "unavailable"
        };
        return $"{ScopeTitle()} {verb}: {changed} {verb}, {skipped} {skippedLabel}, {failed} failed.";
    }

    private static string OperationName(BulkOperation operation) => operation switch
    {
        BulkOperation.Start => "Start all",
        BulkOperation.Stop => "Stop all",
        _ => "Restart all"
    };

    private static bool IsRunning(ServerRuntime server) =>
        server.State is ServerState.Running or ServerState.Stopping;

    private static IReadOnlyList<ConfigurationEntry> CollapseEntries(IReadOnlyList<ConfigurationEntry> entries) =>
        entries
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(group => group.Count() == 1
                ? group.Single()
                : new ConfigurationEntry(group.Key, null, $"The key '{group.Key}' is duplicated. Every server key must be unique."))
            .ToList();

    private enum BulkOperation
    {
        Start,
        Stop,
        Restart
    }
}
