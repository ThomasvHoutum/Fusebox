using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace MCPLauncher.Models;

public sealed class ServerRun : INotifyPropertyChanged
{
    private readonly StringBuilder _output = new();
    private DateTimeOffset? _endedAt;
    private string? _endReason;

    public ServerRun(int sequence)
    {
        Sequence = sequence;
        StartedAt = DateTimeOffset.Now;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Sequence { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? EndedAt
    {
        get => _endedAt;
        private set => SetField(ref _endedAt, value);
    }

    public string? EndReason
    {
        get => _endReason;
        private set => SetField(ref _endReason, value);
    }

    public string DisplayName => $"Run {Sequence} - {StartedAt:HH:mm:ss}";

    public string Output => _output.ToString();

    public void AppendLifecycle(string message) => Append($"[{DateTimeOffset.Now:HH:mm:ss}] Fusebox: {message}");

    public void AppendStandardOutput(string message) => Append(message);

    public void AppendStandardError(string message) => Append($"[stderr] {message}");

    public void Complete(string reason)
    {
        if (EndedAt is not null)
        {
            return;
        }

        EndedAt = DateTimeOffset.Now;
        EndReason = reason;
        AppendLifecycle(reason);
        OnPropertyChanged(nameof(DisplayName));
    }

    private void Append(string message)
    {
        if (_output.Length > 0)
        {
            _output.AppendLine();
        }

        _output.Append(message);
        OnPropertyChanged(nameof(Output));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
