using System.Text.Json;
using Simulation.Application;
using Simulation.UI.ViewModels;
using Simulation.UI.Views;
using Xunit;
namespace Simulation.UI.Tests;

public class CompactKpiTests
{
    [Theory]
    [InlineData(1060)]
    [InlineData(1220)]
    [InlineData(1700)]
    public void FiveCardsFitDesktopWidth(double width)
    {
        var card=KpiDashboardView.ResponsiveCardWidth(width);
        Assert.InRange(card,190,290);
        Assert.True((card+8)*5<=width-16);
    }
    [Theory]
    [InlineData(320)]
    [InlineData(760)]
    public void NarrowCardsRemainReadable(double width)
    {
        var card=KpiDashboardView.ResponsiveCardWidth(width);
        Assert.InRange(card,190,290);
        Assert.True(card+8<=width-16);
    }
    [Fact]
    public void DetailSelectionAndWindowPersistThroughLiveUpdates()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();
        for(int i=0;i<30;i++)vm.Step();
        vm.KpiWindow=10;
        var state=JsonSerializer.Serialize(vm.Live!.Session.Capture());
        vm.OpenExplorer(TeamKpi.WorkVolumePerSek);vm.ExplorerExpanded=true;
        Assert.Equal(state,JsonSerializer.Serialize(vm.Live.Session.Capture()));
        Assert.Contains("1,000",vm.ExplorerPrimary!.Unit);
        vm.Step();
        Assert.True(vm.ExplorerExpanded);
        Assert.Equal(10,vm.KpiWindow);
        Assert.Equal(TeamKpi.WorkVolumePerSek,vm.ExplorerPrimary!.Observation.Definition.Id);
        Assert.Equal(vm.Day,vm.ExplorerPrimary.Observation.LastDay);
        vm.ExplorerExpanded=false;
        Assert.Equal(10,vm.KpiWindow);
    }
}
