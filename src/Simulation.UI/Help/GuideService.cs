using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Simulation.UI.Views;
namespace Simulation.UI.Help;

/// <summary>UI-only service. Intentionally has no reference to a LIVE view model or simulation session.</summary>
public static class GuideService
{
    public static GuideWindow? Current { get; private set; }
    private static GuideNavigation? navigation;
    public static void Open(Control origin,string id="getting-started")
    {
        var catalog=GuideCatalog.Default;
        if(Current is null)
        {
            navigation??=new(catalog);var window=new GuideWindow(catalog,navigation);Current=window;
            window.Closed+=(_,_)=>Current=null;
            var owner=(Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow??TopLevel.GetTopLevel(origin) as Window;
            if(owner is MainWindow main && OperatingSystem.IsMacOS())window.KeyBindings.Add(new(){Gesture=main.ExitGesture,Command=main.ExitCommand});
            if(owner is not null)window.Show(owner);else window.Show();
        }
        Current.Navigate(id);if(Current.WindowState==WindowState.Minimized)Current.WindowState=WindowState.Normal;Current.Activate();
    }
}
