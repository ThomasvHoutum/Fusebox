using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MCPLauncher.Models;

public sealed class ServerRuntime : INotifyPropertyChanged
{
    private ServerDefinition? _definition;
    private string? _configurationError;
    private bool _hasPendingChanges;
    private bool _actionsBlocked;
    private ServerState _state;
    private ServerRun? _selectedRun;

    public ServerRuntime(string id, ServerDefinition? definition, string? configurationError)
    {
        Id = id;
        _definition = definition;
        _configurationError = configurationError;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    public ServerDefinition? Definition
    {
        get => _definition;
        private set
        {
            if (SetField(ref _definition, value))
            {
                RaiseDisplayProperties();
            }
        }
    }

    public string Title => Definition?.Title ?? Id;

    public string Description => Definition?.Description ?? "This entry cannot be launched until its configuration is corrected.";

    public string PortDisplay => Definition?.Port is int port ? $"Port {port}" : string.Empty;

    public string? ConfigurationError
    {
        get => _configurationError;
        private set
        {
            if (SetField(ref _configurationError, value))
            {
                OnPropertyChanged(nameof(ConfigurationErrorDisplay));
                RaiseActionProperties();
            }
        }
    }

    public string ConfigurationErrorDisplay => ConfigurationError is null ? string.Empty : $"Configuration error: {ConfigurationError}";

    public bool HasPendingChanges
    {
        get => _hasPendingChanges;
        private set
        {
            if (SetField(ref _hasPendingChanges, value))
            {
                OnPropertyChanged(nameof(PendingChangesDisplay));
            }
        }
    }

    public string PendingChangesDisplay => HasPendingChanges ? "Changes apply after restart" : string.Empty;

    public bool ActionsBlocked
    {
        get => _actionsBlocked;
        set
        {
            if (SetField(ref _actionsBlocked, value))
            {
                RaiseActionProperties();
            }
        }
    }

    public ServerState State
    {
        get => _state;
        set
        {
            if (SetField(ref _state, value))
            {
                OnPropertyChanged(nameof(StateDisplay));
                RaiseActionProperties();
            }
        }
    }

    public string StateDisplay => State.ToString();

    public ObservableCollection<ServerRun> Runs { get; } = [];

    public ServerRun? SelectedRun
    {
        get => _selectedRun;
        set => SetField(ref _selectedRun, value);
    }

    public Process? Process { get; set; }

    public ServerRun? CurrentRun { get; set; }

    public ServerDefinition? ActiveDefinition { get; private set; }

    public bool ExitWasRequested { get; set; }

    public string RequestedEndReason { get; set; } = "Stopped by user.";

    public bool CanStart => !ActionsBlocked && Definition is not null && ConfigurationError is null && State is ServerState.Stopped or ServerState.Failed;

    public bool CanStop => State is ServerState.Running;

    public bool CanRestart => !ActionsBlocked && State is ServerState.Running;

    public void ApplyConfiguration(ServerDefinition? definition, string? error)
    {
        if (definition is null && State is ServerState.Running or ServerState.Stopping)
        {
            definition = Definition;
        }

        Definition = definition;
        ConfigurationError = error;
        HasPendingChanges = State == ServerState.Running && (error is not null || ActiveDefinition != definition);
        RaiseActionProperties();
    }

    public void ConfigurationAppliedToProcess()
    {
        ActiveDefinition = Definition;
        HasPendingChanges = false;
    }

    public ServerRun BeginRun()
    {
        var run = new ServerRun(Runs.Count + 1);
        Runs.Add(run);
        SelectedRun = run;
        CurrentRun = run;
        return run;
    }

    private void RaiseDisplayProperties()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(PortDisplay));
        RaiseActionProperties();
    }

    private void RaiseActionProperties()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanRestart));
    }

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
