using System.Windows.Input;

namespace Fusebox.Commands;

public sealed class AsyncCommand<T>(Func<T, Task> execute, Predicate<T>? canExecute = null) : ICommand
    where T : class
{
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        !_isExecuting && parameter is T value && (canExecute?.Invoke(value) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter) || parameter is not T value)
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await execute(value);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
