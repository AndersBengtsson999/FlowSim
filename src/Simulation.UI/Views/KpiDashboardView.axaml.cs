using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using System.ComponentModel;
using Simulation.UI.ViewModels;
namespace Simulation.UI.Views;
public partial class KpiDashboardView : UserControl
{
    public static readonly StyledProperty<Orientation> LayoutOrientationProperty = AvaloniaProperty.Register<KpiDashboardView,Orientation>(nameof(LayoutOrientation),Orientation.Horizontal);
    public Orientation LayoutOrientation { get=>GetValue(LayoutOrientationProperty);set=>SetValue(LayoutOrientationProperty,value); }
    public static readonly StyledProperty<double> ChartHeightProperty = AvaloniaProperty.Register<KpiDashboardView,double>(nameof(ChartHeight),160);
    public double ChartHeight { get=>GetValue(ChartHeightProperty);set=>SetValue(ChartHeightProperty,value); }
    private LiveViewModel? observed;
    private Window? expandedWindow;
    private bool expandedView;
    private bool? pairedCharts;
    public KpiDashboardView()
    {
        InitializeComponent();SizeChanged+=(_,_)=>RefreshPresentation();DataContextChanged+=(_,_)=>Observe();
        AttachedToVisualTree+=(_,_)=>Observe();
        DetachedFromVisualTree+=(_,_)=>{if(observed is not null)observed.PropertyChanged-=Changed;observed=null;expandedWindow?.Close();};
    }
    private void Observe()
    {
        if(observed is not null)observed.PropertyChanged-=Changed;
        observed=DataContext as LiveViewModel;
        if(observed is not null)observed.PropertyChanged+=Changed;
        RefreshPresentation();
    }
    private void Changed(object? sender,PropertyChangedEventArgs e)=>RefreshPresentation();
    private void RefreshPresentation()
    {
        ExplorerSurface.IsVisible=observed?.ExplorerOpen==true && (expandedView || !observed.ExplorerExpanded);
        CardStrip.IsVisible=!expandedView;
        ExpandExplorer.IsVisible=!expandedView && observed?.ExplorerOpen==true;
        DetachedNote.IsVisible=!expandedView && observed?.ExplorerExpanded==true;
        var pair=expandedView && Bounds.Width>=780 && observed?.HasComparison==true;
        if(pairedCharts!=pair){pairedCharts=pair;MetricGraphs.ColumnDefinitions=new(pair ? "*,*" : "*");MetricGraphs.RowDefinitions=new(pair ? "Auto" : "Auto,Auto");Grid.SetRow(SecondaryMetric,pair?0:1);Grid.SetColumn(SecondaryMetric,pair?1:0);SecondaryMetric.Margin=new Thickness(pair?12:0,0,0,0);}

    }
    private void OpenExplorerClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { if(sender is Control { DataContext: KpiCard card } && DataContext is LiveViewModel vm) vm.OpenExplorer(card.Observation.Definition.Id); }
    private void CloseExplorerClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { if(expandedView) (TopLevel.GetTopLevel(this) as Window)?.Close();else if(DataContext is LiveViewModel vm)vm.CloseExplorer(); }
    private void RemoveComparisonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if(DataContext is LiveViewModel vm) vm.ClearComparison(); }
    private void ExpandExplorerClicked(object? sender,Avalonia.Interactivity.RoutedEventArgs e)=>OpenExpandedExplorer();
    public void OpenExpandedExplorer()
    {
        if(expandedWindow is not null){expandedWindow.Activate();return;}
        if(DataContext is not LiveViewModel vm || TopLevel.GetTopLevel(this) is not Window owner)return;
        if(!vm.ExplorerOpen)vm.OpenExplorer(vm.KpiSettings.Selected!.FirstOrDefault());
        var view=new KpiDashboardView { expandedView=true, ChartHeight=300, DataContext=vm, Margin=new Thickness(12) };
        view.RefreshPresentation();view.CloseExplorerButton.Content="Return to compact view";
        var window=new Window {Title="FLOW · KPI Explorer",Width=860,Height=900,MinWidth=620,MinHeight=520,Content=view,Background=Avalonia.Media.Brush.Parse("#EFF3F4")};
        window.RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Light;
        window.KeyBindings.Add(new KeyBinding {Gesture=new(Key.Escape),Command=new RelayCommand(window.Close)});
        if(OperatingSystem.IsMacOS() && owner is MainWindow main)
            window.KeyBindings.Add(new KeyBinding {Gesture=main.ExitGesture,Command=main.ExitCommand});
        window.SizeChanged+=(_,_)=>view.ExplorerScroll.MaxHeight=Math.Max(300,window.ClientSize.Height-110);
        window.Closed+=(_,_)=>{expandedWindow=null;vm.ExplorerExpanded=false;};
        expandedWindow=window;vm.ExplorerExpanded=true;window.Show(owner);
        if(window.Screens.ScreenFromWindow(owner) is { } screen)
            window.Position=new PixelPoint(Math.Max(screen.WorkingArea.X,screen.WorkingArea.Right-(int)(window.Width*screen.Scaling)),screen.WorkingArea.Y+30);
    }
}
