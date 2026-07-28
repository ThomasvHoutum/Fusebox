using Avalonia.Controls;
using MCPLauncher.Views;

namespace MCPLauncher.Services;

public sealed class AvaloniaUserInteractionService(Func<Window> owner) : IUserInteractionService
{
    public Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText) =>
        new DecisionDialog(title, message, confirmText, cancelText).ShowDialog<bool>(owner());

    public async Task ShowMessageAsync(string title, string message, string closeText) =>
        await new DecisionDialog(title, message, closeText).ShowDialog<bool>(owner());
}
