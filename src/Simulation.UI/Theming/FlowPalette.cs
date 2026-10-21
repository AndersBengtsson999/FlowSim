using Avalonia.Media;
namespace Simulation.UI.Theming;

/// <summary>Shared permanent-dark palette for XAML resources and drawn charts. Artwork is excluded.</summary>
public static class FlowPalette
{
    public const string App = "#101820";
    public static Color AppColor => Color.Parse(App);
    public const string Sidebar = "#15222E";
    public static Color SidebarColor => Color.Parse(Sidebar);
    public const string Content = "#172532";
    public static Color ContentColor => Color.Parse(Content);
    public const string Panel = "#20313F";
    public static Color PanelColor => Color.Parse(Panel);
    public const string Elevated = "#293D4D";
    public static Color ElevatedColor => Color.Parse(Elevated);
    public const string Text = "#F0F4F7";
    public static Color TextColor => Color.Parse(Text);
    public const string Secondary = "#A9BBC8";
    public static Color SecondaryColor => Color.Parse(Secondary);
    public const string Muted = "#9AAEBC";
    public static Color MutedColor => Color.Parse(Muted);
    public const string Accent = "#68A9D3";
    public static Color AccentColor => Color.Parse(Accent);
    public const string Hover = "#34566D";
    public static Color HoverColor => Color.Parse(Hover);
    public const string Selected = "#29485F";
    public static Color SelectedColor => Color.Parse(Selected);
    public const string Success = "#55C995";
    public static Color SuccessColor => Color.Parse(Success);
    public const string Warning = "#E9B65D";
    public static Color WarningColor => Color.Parse(Warning);
    public const string Error = "#E47777";
    public static Color ErrorColor => Color.Parse(Error);
    public const string Border = "#344A5B";
    public static Color BorderColor => Color.Parse(Border);
    public const string OnAccent = "#101820";
    public static Color OnAccentColor => Color.Parse(OnAccent);
    public const string QueueWarning = "#493D29";
    public static Color QueueWarningColor => Color.Parse(QueueWarning);
    public const string QueueError = "#492F35";
    public static Color QueueErrorColor => Color.Parse(QueueError);
    public const string Completed = "#203D36";
    public static Color CompletedColor => Color.Parse(Completed);
    public const string Purple = "#C29AD9";
    public static Color PurpleColor => Color.Parse(Purple);
    public const string Workshop = "#101A25";
    public static Color WorkshopColor => Color.Parse(Workshop);
}
