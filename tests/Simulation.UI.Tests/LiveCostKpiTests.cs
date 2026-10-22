using System.Globalization;
using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Simulation.UI.Controls;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class LiveCostKpiTests
{
    private static KpiCard Ratio(double? current, double? previous) => new(new(KpiDashboard.Definitions.Single(d=>d.Id==TeamKpi.WorkVolumePerSek),current,previous,21,40,20,20,new([new(40,current)],[])));
    [Fact] public void RatioScalesOnlyPresentationIncludingHistoryAndTooltip()
    {
        var card=Ratio(.000139,.000127);var raw=JsonSerializer.Serialize(card.Observation);
        Assert.Equal(.139.ToString("N3",CultureInfo.CurrentCulture),card.Value);
        Assert.Equal("effort units / 1,000 SEK",card.Unit);Assert.Contains("internally released",card.Help);
        Assert.Equal(.139,card.Trend.Points.Single().Value!.Value,12);
        var chart=new LivePerformanceTrendChart{Series=card.Trend,Unit=card.Unit,NumberFormat=card.NumberFormat};
        Assert.Contains(card.Value+" "+card.Unit,chart.DescribeAtDay(40));
        Assert.Contains(9.4.ToString("N1",CultureInfo.CurrentCulture)+"%",card.Change);
        Assert.Equal(raw,JsonSerializer.Serialize(card.Observation));
    }
    [Theory] [InlineData(null,1d,"unavailable")] [InlineData(1d,null,"unavailable")] [InlineData(1d,0d,"zero")] [InlineData(0d,0d,"zero")]
    public void UndefinedComparisonsNeverInventZeroOrInfinity(double? value,double? previous,string expected)
    { Assert.Contains(expected,Ratio(value,previous).Change);Assert.Contains("N/A",Ratio(value,previous).Change); }
    [Fact] public void ZeroWithValidBaselineIsMinusOneHundredPercent()
    { Assert.Contains((-100d).ToString("N1",CultureInfo.CurrentCulture)+"%",Ratio(0,.1).Change); }
    [Fact] public void PinnedBalanceAndBothExplorerSlotsTrackLaunchAndRestore()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=40,DeveloperCount=5,TesterCount=2,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=1,Release=new(ReleaseMode.Scheduled,3,10)},WorkArrivalMode.ContinuousArrival,2);
        for(int i=0;i<60;i++)live.Step();
        using var vm=new LiveViewModel();vm.Load(live);
        Assert.Single(vm.DisplayCards,c=>c.IsBalance);
        var c=vm.UnlaunchedCostCard!;Assert.Same(vm.LaunchCostHistory,c.BalanceHistory);
        Assert.Contains("Day 60",c.Period);Assert.DoesNotContain("20d",c.Period);Assert.Contains("WIP:",c.Components);
        vm.OpenExplorer(TeamKpi.UnlaunchedDevelopmentCost);Assert.True(vm.ExplorerPrimary!.IsBalance);
        vm.ComparisonKpi=vm.ExplorerOptions.Single(d=>d.Id==TeamKpi.ReleasedThroughput);Assert.True(vm.HasComparison);
        vm.PrimaryKpi=vm.ComparisonKpi;vm.ComparisonKpi=vm.ExplorerOptions.Single(d=>d.Id==TeamKpi.UnlaunchedDevelopmentCost);
        Assert.True(vm.ExplorerSecondary!.IsBalance);
        vm.CheckpointCommand.Execute(null);vm.SelectedCheckpoint=vm.Checkpoints.Single();
        var before=vm.UnlaunchedCostCard!.BalanceHistory!.Current;
        vm.LaunchProductCommand.Execute(null);
        var after=vm.UnlaunchedCostCard!.BalanceHistory!.Current;
        Assert.True(before.AwaitingLaunch>0);Assert.Equal(before.Incurred,after.Incurred);Assert.Equal(before.WorkInProgress,after.Unlaunched);
        Assert.Equal(before.Unlaunched-before.AwaitingLaunch,after.Unlaunched);
        vm.RestoreCommand.Execute(null);Assert.Equal(before,vm.UnlaunchedCostCard!.BalanceHistory!.Current);
        using var loaded=new LiveViewModel();loaded.Load(LiveSessionJson.Load(LiveSessionJson.Save(vm.Live!)));
        Assert.Equal(vm.UnlaunchedCostCard.Value,loaded.UnlaunchedCostCard!.Value);Assert.Equal(vm.KpiSettings.Selected,loaded.KpiSettings.Selected);Assert.Equal(vm.KpiSettings.Explorer,loaded.KpiSettings.Explorer);
        var state=JsonSerializer.Serialize(vm.Live!.Session.Capture());vm.KpiWindow=40;
        Assert.Equal(before,vm.UnlaunchedCostCard.BalanceHistory!.Current);Assert.Equal(state,JsonSerializer.Serialize(vm.Live.Session.Capture()));
    }
}
