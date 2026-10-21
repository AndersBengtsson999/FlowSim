using Avalonia.Media;
using Simulation.UI.Theming;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class DarkThemeTests
{
    [Theory]
    [InlineData(FlowPalette.Text,FlowPalette.App)]
    [InlineData(FlowPalette.Text,FlowPalette.Panel)]
    [InlineData(FlowPalette.Secondary,FlowPalette.Panel)]
    [InlineData(FlowPalette.Muted,FlowPalette.Panel)]
    [InlineData(FlowPalette.OnAccent,FlowPalette.Accent)]
    [InlineData(FlowPalette.Warning,FlowPalette.Panel)]
    [InlineData(FlowPalette.Error,FlowPalette.Panel)]
    [InlineData(FlowPalette.Text,FlowPalette.Selected)]
    public void TextPairsMeetAAContrast(string foreground,string background)
    {
        static double L(string color)
        {
            var c=Color.Parse(color);
            static double Linear(byte b){var v=b/255d;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
            return .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);
        }
        var a=L(foreground);var b=L(background);Assert.True((Math.Max(a,b)+.05)/(Math.Min(a,b)+.05)>=4.5);
    }
    [Fact] public void WorkspaceNavigationStillKeepsOneActiveDestination()
    {
        var main=new MainWindowViewModel();var vm=main.Simple;
        try
        {
            foreach(var command in new[]{vm.OpenLiveCommand,vm.OpenRunCommand,vm.OpenChangesCommand,vm.OpenExploreCommand,vm.OpenExperimentsCommand,vm.AdvancedCommand})
            {
                command.Execute(null);
                Assert.Equal(1,new[]{vm.LiveVisible,vm.RunVisible,vm.AnalyzeCompareVisible,vm.ExploreVisible,vm.ExperimentsVisible,vm.AdvancedVisible}.Count(v=>v));
            }
        }
        finally { vm.Live.Dispose(); }
    }
}
