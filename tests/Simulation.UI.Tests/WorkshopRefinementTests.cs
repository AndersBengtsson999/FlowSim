using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Controls;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class WorkshopRefinementTests
{
    [Fact] public void PressureExplainsEvidenceRatherThanQueueSize()
    {
        var missing=QueuePressure.Classify(1000,null,"None",[],[],"Busy");
        Assert.Equal(QueuePressureKind.Normal,missing.Kind);Assert.Contains("Fewer than four",missing.Evidence);
        var growing=QueuePressure.Classify(8,2,"None",[2,4,6,8],[true,false,true],"Busy");
        Assert.Equal(QueuePressureKind.Growing,growing.Kind);Assert.DoesNotContain("potential bottleneck",growing.Condition);
        var sustained=QueuePressure.Classify(8,2,"None",[2,4,6,8],[true,true,true],"Busy");
        Assert.Contains("potential bottleneck",sustained.Condition);Assert.Contains("last three days",sustained.Evidence);
        var shrinking=QueuePressure.Classify(2,2,"None",[8,7,6,2],[true,true,true],"Busy");
        Assert.Equal(QueuePressureKind.Shrinking,shrinking.Kind);Assert.Equal("↓",shrinking.Marker);
        Assert.Contains("not proof",shrinking.Evidence);
    }
    [Theory] [InlineData("Testing queue",TeamKpi.TesterUtilization)] [InlineData("Review queue",TeamKpi.ReviewQueue)]
    [InlineData("Development",TeamKpi.DeveloperUtilization)] [InlineData("Release",TeamKpi.UnlaunchedDevelopmentCost)]
    public void StationMetricsUseExistingDefinitionsWithoutMutation(string station,TeamKpi expected)
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();for(int i=0;i<25;i++)vm.Step();
        var before=JsonSerializer.Serialize(vm.Live!.Session.Capture());
        var cards=vm.StationPerformance(station);Assert.Contains(cards,c=>c.Observation.Definition.Id==expected);
        foreach(var card in cards)
        {
            var authority=KpiDashboard.Project(vm.Live.Session,vm.KpiSettings with{Selected=[card.Observation.Definition.Id]}).Single();
            Assert.Equal(authority.Value,card.Observation.Value);
        }
        Assert.Equal(before,JsonSerializer.Serialize(vm.Live.Session.Capture()));
        Assert.Empty(vm.StationPerformance("unknown"));
    }
    [Fact] public void SelectionAndEvidenceSurviveLiveAndResetAfterChanges()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=30,DeveloperCount=5,TesterCount=1},WorkArrivalMode.ContinuousArrival,2);
        for(int i=0;i<20;i++)live.Step();
        live.Step();var details=StationDetails.For("Testing queue",GameSceneState.From(live));
        Assert.Equal("Testing queue",details.Title);Assert.Contains("Days 18–21",details.EvidencePeriod);
        live.Session.ApplyChanges(live.Session.Configuration with{Team=live.Session.Configuration.Team with{TesterCount=2}},"Test");
        live.Step();details=StationDetails.For("Testing queue",GameSceneState.From(live));
        Assert.Contains("1 observations",details.EvidencePeriod);Assert.Contains("Fewer than four",details.Observation);
        Assert.Equal(StationDetails.Empty,StationDetails.For(null,GameSceneState.From(live)));
    }
    [Theory] [InlineData(.3)] [InlineData(1)] [InlineData(2.5)]
    public void SelectionOutlineKeepsTwoScreenPixels(double scale) => Assert.Equal(2,StationMap.SelectionStroke(scale)*scale,9);
}
