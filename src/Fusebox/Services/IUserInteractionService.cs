namespace Fusebox.Services;

public interface IUserInteractionService
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText);

    Task ShowMessageAsync(string title, string message, string closeText);
}
