using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Simulation.UI.Theming;
using Simulation.UI.ViewModels;

namespace Simulation.UI.Views;

public static class ProductDialogs
{
    private static Window Create(string title) => new()
    {
        Title = title, Width = 520, SizeToContent = SizeToContent.Height,
        CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = Brush.Parse(FlowPalette.Panel), Foreground = Brush.Parse(FlowPalette.Text),
        RequestedThemeVariant = ThemeVariant.Dark
    };
    public static Window CreateResetConfirmation()
    {
        var dialog = Create("Reset LIVE session?");
        var cancel = new Button { Name = "CancelReset", Content = "Cancel", IsCancel = true, MinWidth = 90 };
        var reset = new Button { Name = "ConfirmReset", Content = "Reset", MinWidth = 90, Foreground = Brush.Parse(FlowPalette.Error) };
        cancel.Click += (_, _) => dialog.Close(false);
        reset.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24), Spacing = 18,
            Children =
            {
                new TextBlock { Text = "Reset LIVE session?", FontSize = 20, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = "The current timeline and all its checkpoints will be discarded. Save the session first if you want to keep it. Saved files will not be deleted.", TextWrapping = TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 12, Children = { cancel, reset } }
            }
        };
        // Escape, the title-bar close button and Enter on initial focus all cancel.
        dialog.KeyBindings.Add(new() { Gesture = new(Key.Escape), Command = new RelayCommand(() => dialog.Close(false)) });
        dialog.Opened += (_, _) => cancel.Focus();
        return dialog;
    }
    public static Task<bool> ConfirmReset(Window owner) => CreateResetConfirmation().ShowDialog<bool>(owner);
    public static Window CreateAbout()
    {
        var dialog = Create("About FLOW");
        var close = new Button { Content = "Close", IsCancel = true, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24), Spacing = 18,
            Children = { new SelectableTextBlock { Name = "ProductIdentity", Text = ProductIdentity.Description, TextWrapping = TextWrapping.Wrap }, close }
        };
        dialog.KeyBindings.Add(new() { Gesture = new(Key.Escape), Command = new RelayCommand(dialog.Close) });
        if (OperatingSystem.IsMacOS()) dialog.KeyBindings.Add(new() { Gesture = new(Key.Q, KeyModifiers.Meta), Command = new RelayCommand(() =>
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop) desktop.TryShutdown();
        }) });
        return dialog;
    }
    public static Task ShowAbout(Window owner) => CreateAbout().ShowDialog(owner);
}
