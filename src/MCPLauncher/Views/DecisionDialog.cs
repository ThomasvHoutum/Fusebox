using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using IconPacks.Avalonia.Lucide;

namespace MCPLauncher.Views;

public sealed class DecisionDialog : Window
{
    private static readonly IBrush PanelBrush = new SolidColorBrush(Color.Parse("#181C19"));
    private static readonly IBrush RaisedBrush = new SolidColorBrush(Color.Parse("#252720"));
    private static readonly IBrush BorderLineBrush = new SolidColorBrush(Color.Parse("#3A3A30"));
    private static readonly IBrush PrimaryTextBrush = new SolidColorBrush(Color.Parse("#EBE5D8"));
    private static readonly IBrush SecondaryTextBrush = new SolidColorBrush(Color.Parse("#A7A49A"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#C5A057"));
    private static readonly IBrush WarningSurfaceBrush = new SolidColorBrush(Color.Parse("#352E1D"));
    private static readonly IBrush CopperBrush = new SolidColorBrush(Color.Parse("#B77A4F"));

    public DecisionDialog(string title, string message, string confirmText, string? cancelText = null)
    {
        var isDecision = cancelText is not null;

        Title = title;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var confirmButton = CreateActionButton(
            isDecision ? PackIconLucideKind.Square : PackIconLucideKind.Check,
            confirmText,
            Math.Max(120, confirmText.Length * 7 + 48));
        if (isDecision)
        {
            confirmButton.Classes.Add("stop");
        }

        confirmButton.Click += (_, _) => Close(true);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        if (cancelText is not null)
        {
            var cancelButton = CreateActionButton(PackIconLucideKind.X, cancelText, 104);
            cancelButton.Click += (_, _) => Close(false);
            actions.Children.Add(cancelButton);
        }

        actions.Children.Add(confirmButton);

        var icon = new PackIconLucide
        {
            Width = 22,
            Height = 22,
            Foreground = isDecision ? WarningBrush : CopperBrush,
            Kind = isDecision ? PackIconLucideKind.TriangleAlert : PackIconLucideKind.Info
        };

        var iconSurface = new Border
        {
            Width = 44,
            Height = 44,
            VerticalAlignment = VerticalAlignment.Top,
            Background = isDecision ? WarningSurfaceBrush : RaisedBrush,
            BorderBrush = isDecision ? new SolidColorBrush(Color.Parse("#5B4C2D")) : BorderLineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = icon
        };
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;

        var copy = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = title.TrimEnd('?'),
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = PrimaryTextBrush
                },
                CreateMessageContent(message)
            }
        };

        var body = new Grid
        {
            Margin = new Thickness(24, 24, 24, 22),
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 16,
            Children =
            {
                iconSurface,
                copy
            }
        };
        Grid.SetColumn(copy, 1);

        var footer = new Border
        {
            Padding = new Thickness(20, 14, 20, 18),
            Background = PanelBrush,
            BorderBrush = BorderLineBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = actions
        };

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            Children =
            {
                body,
                footer
            }
        };
        Grid.SetRow(footer, 1);
        Content = layout;
    }

    private static Button CreateActionButton(PackIconLucideKind iconKind, string text, double minWidth)
    {
        return new Button
        {
            MinWidth = minWidth,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 7,
                Children =
                {
                    new PackIconLucide
                    {
                        Width = 15,
                        Height = 15,
                        Kind = iconKind,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = text,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
    }

    private static Control CreateMessageContent(string message)
    {
        var sections = message.Replace("\r", string.Empty).Split(["\n\n"], 2, StringSplitOptions.None);
        if (sections.Length != 2)
        {
            return CreateBodyText(message);
        }

        var items = sections[1]
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith('•') || line.StartsWith('-'))
            .Select(line => line.TrimStart('•', '-', ' '))
            .Where(line => line.Length > 0)
            .ToList();
        if (items.Count == 0)
        {
            return CreateBodyText(message);
        }

        var itemList = new StackPanel { Spacing = 2 };
        foreach (var item in items)
        {
            itemList.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 9,
                Children =
                {
                    new PackIconLucide
                    {
                        Width = 15,
                        Height = 15,
                        Margin = new Thickness(0, 1, 0, 0),
                        Foreground = CopperBrush,
                        Kind = PackIconLucideKind.Server
                    },
                    new TextBlock
                    {
                        Text = item,
                        FontWeight = FontWeight.Medium,
                        Foreground = PrimaryTextBrush
                    }
                }
            });
            Grid.SetColumn(itemList.Children[^1], 0);
            if (itemList.Children[^1] is Grid itemRow)
            {
                Grid.SetColumn(itemRow.Children[1], 1);
            }
        }

        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                CreateBodyText(sections[0]),
                new Border
                {
                    Padding = new Thickness(12, 10),
                    Background = PanelBrush,
                    BorderBrush = BorderLineBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Child = itemList
                }
            }
        };
    }

    private static TextBlock CreateBodyText(string message) =>
        new()
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 404,
            FontSize = 13,
            Foreground = SecondaryTextBrush,
            LineHeight = 20
        };
}
