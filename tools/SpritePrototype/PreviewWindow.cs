using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SpritePrototype;

public sealed class PreviewWindow : Window
{
    public SpriteAssets Assets { get; } = new();
    public SpriteWorldControl World { get; }
    public Border Stage { get; }
    private readonly TextBlock status;
    public PreviewWindow()
    {
        Title = "Software Workshop · Multi-worker Sprite Prototype B1.2";
        Width = 1280; Height = 980; MinWidth = 800; MinHeight = 640;
        Background = Brush.Parse("#0C1520");
        World = new(Assets);
        var heading = new StackPanel { Spacing = 7, Children =
        {
            Text("SOFTWARE WORKSHOP", 13, "#FFEB00"),
            Text("A small team. A shared workshop.", 30, "#F1F5F7"),
            Text("SPRITE STUDY  /  B1.2     •     Separate preview, no LIVE session", 12, "#93ABBE")
        }};
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        controls.Children.Add(Action("Office", () => SetOffice(true)));
        controls.Children.Add(Action("Single desk", () => SetOffice(false)));
        var anchors = new CheckBox { Content = "Anchors", VerticalAlignment = VerticalAlignment.Center };
        anchors.IsCheckedChanged += (_, _) => { World.ShowAnchors = anchors.IsChecked == true; World.InvalidateVisual(); };
        var light = new CheckBox { Content = "Light floor surround", VerticalAlignment = VerticalAlignment.Center };
        light.IsCheckedChanged += (_, _) => SetLight(light.IsChecked == true);
        controls.Children.Add(anchors); controls.Children.Add(light);
        Stage = new Border { Background = Brush.Parse("#152430"), CornerRadius = new(16),
            Child = new Viewbox { Stretch = Stretch.Uniform, Child = World } };
        status = Text("Four illustrative workers · Three reusable workstations · Static scene · No LIVE assignments", 13, "#9BB4C6");
        var footer = new StackPanel { Spacing = 8, Children =
        {
            Text("A  WORKING     /     B  WAITING     /     C  COLLABORATING", 12, "#FFEB00"),
            Text("One developer working, one waiting, two solving the same task.", 18, "#DFEAF0"), status
        }};
        var layout = new Grid { RowDefinitions = new("Auto,Auto,*,Auto"), Margin = new(28), RowSpacing = 18 };
        layout.Children.Add(heading); Grid.SetRow(controls, 1); layout.Children.Add(controls);
        Grid.SetRow(Stage, 2); layout.Children.Add(Stage); Grid.SetRow(footer, 3); layout.Children.Add(footer);
        Content = layout;
        Closed += (_, _) => Assets.Dispose();
    }
    public void SetOffice(bool office)
    {
        World.SetOffice(office);
        status.Text = office ? "Four illustrative workers · Three reusable workstations · Static scene · No LIVE assignments" : "Original B1.1 study · Independent character, desk, computer, package and plant";
    }
    public void SetBehind(bool behind)
    {
        World.SetObjects(SpriteCatalog.Demo.Select(o => o.Id == "developer"
            ? o with { Position = behind ? new(-.9, -.7) : SpriteCatalog.DeveloperPosition } : o));
        status.Text = behind ? "Depth check · Developer behind desk · Placement changed manually; no animation" :
            "Four illustrative workers · Three reusable workstations · Static scene · No LIVE assignments";
    }
    public void SetLight(bool light) => Stage.Background = Brush.Parse(light ? "#DBE5E4" : "#152430");
    private static TextBlock Text(string text, double size, string color) => new()
    { Text = text, FontSize = size, Foreground = Brush.Parse(color), TextWrapping = TextWrapping.Wrap };
    private static Button Action(string text, Action action)
    {
        var button = new Button { Content = text, Padding = new(12, 8) };
        button.Click += (_, _) => action(); return button;
    }
}
