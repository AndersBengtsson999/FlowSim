using Avalonia.Controls;
namespace Simulation.UI.Views;
public partial class ProductLaunchView : UserControl
{
    private void OpenGuide(object? sender,Avalonia.Interactivity.RoutedEventArgs e)
    { if(sender is Button {Tag:string id}) Simulation.UI.Help.GuideService.Open(this,id); }
    public ProductLaunchView() => InitializeComponent();
}
