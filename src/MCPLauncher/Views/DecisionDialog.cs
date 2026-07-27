using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MCPLauncher.Views;

public sealed class DecisionDialog : Window
{
    public DecisionDialog(string title, string message, string confirmText, string? cancelText = null)
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var confirmButton = new Button
        {
            Content = confirmText,
            MinWidth = 90
        };
        confirmButton.Click += (_, _) => Close(true);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        if (cancelText is not null)
        {
            var cancelButton = new Button
            {
                Content = cancelText,
                MinWidth = 90
            };
            cancelButton.Click += (_, _) => Close(false);
            actions.Children.Add(cancelButton);
        }

        actions.Children.Add(confirmButton);

        Content = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 416
                },
                actions
            }
        };
    }
}
