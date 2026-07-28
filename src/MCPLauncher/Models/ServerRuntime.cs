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
    private Process? _process;

    public ServerRuntime(string id, ServerDefinition? definition, string? configurationError)
    {
        Id = id;
        _definition = definition;
        _configurationError = configurationError;
        Runs.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(RunCountDisplay));
            OnPropertyChanged(nameof(HasNoRuns));
        };
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

    public string PortValueDisplay => Definition?.Port?.ToString() ?? "-";

    public string CommandDisplay
    {
        get
        {
            if (Definition is null)
            {
                return "-";
            }

            return Definition.Arguments.Count == 0
                ? Definition.Command
                : $"{Definition.Command} {string.Join(" ", Definition.Arguments.Select(FormatArgument))}";
        }
    }

    public string? ConfigurationError
    {
        get => _configurationError;
        private set
        {
            if (SetField(ref _configurationError, value))
            {
                OnPropertyChanged(nameof(ConfigurationErrorDisplay));
                OnPropertyChanged(nameof(HasServerNotice));
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
                OnPropertyChanged(nameof(HasServerNotice));
            }
        }
    }

    public string PendingChangesDisplay => HasPendingChanges ? "Changes apply after restart" : string.Empty;

    public bool HasServerNotice => HasPendingChanges || ConfigurationError is not null;

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
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsTransitioning));
                OnPropertyChanged(nameof(IsFailed));
                RaiseActionProperties();
            }
        }
    }

    public string StateDisplay => State == ServerState.Failed ? "Faulted" : State.ToString();

    public bool IsRunning => State == ServerState.Running;

    public bool IsTransitioning => State is ServerState.Starting or ServerState.Stopping;

    public bool IsFailed => State == ServerState.Failed;

    public ObservableCollection<ServerRun> Runs { get; } = [];

    public ServerRun? SelectedRun
    {
        get => _selectedRun;
        set => SetField(ref _selectedRun, value ?? Runs.LastOrDefault());
    }

    public string RunCountDisplay => Runs.Count.ToString();

    public bool HasNoRuns => Runs.Count == 0;

    public Process? Process
    {
        get => _process;
        set
        {
            if (SetField(ref _process, value))
            {
                OnPropertyChanged(nameof(ProcessIdDisplay));
            }
        }
    }

    public string ProcessIdDisplay
    {
        get
        {
            try
            {
                return Process?.Id.ToString() ?? "-";
            }
            catch (InvalidOperationException)
            {
                return "-";
            }
        }
    }

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

    public void SelectLatestRun() => SelectedRun = Runs.LastOrDefault();

    private void RaiseDisplayProperties()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(PortDisplay));
        OnPropertyChanged(nameof(PortValueDisplay));
        OnPropertyChanged(nameof(CommandDisplay));
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

    private static string FormatArgument(string argument) =>
        argument.Any(char.IsWhiteSpace) ? $"\"{argument.Replace("\"", "\\\"")}\"" : argument;
}
