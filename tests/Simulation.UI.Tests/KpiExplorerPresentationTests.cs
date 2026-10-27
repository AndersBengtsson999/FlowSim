using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class KpiExplorerPresentationTests
{
    [Fact] public void ExploreIsDefaultAndModeSwitchesPreserveSelections()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();vm.Step();
        vm.OpenExplorer(TeamKpi.CycleTime);vm.KpiWindow=40;
        Assert.True(vm.ExploreKpi);Assert.False(vm.CompareScenarios);Assert.False(vm.ShowKpiComparison);
        vm.ExplorerMode="Compare KPIs";Assert.True(vm.ComparisonEmpty);
        vm.ComparisonKpi=vm.ExplorerOptions.Single(d=>d.Id==TeamKpi.TotalTeamCost);
        Assert.True(vm.ShowKpiComparison);Assert.NotEqual(vm.ExplorerPrimary!.Unit,vm.ExplorerSecondary!.Unit);
        foreach(var mode in vm.ExplorerModes.Append("Explore one KPI")) {
            vm.ExplorerMode=mode;Assert.Equal(TeamKpi.CycleTime,vm.PrimaryKpi!.Id);
            Assert.Equal(TeamKpi.TotalTeamCost,vm.ComparisonKpi!.Id);Assert.Equal(40,vm.KpiWindow);
        }
        Assert.False(vm.ShowKpiComparison);Assert.True(vm.HasComparison);
    }
    [Fact] public void AllDefinitionsValuesAndSamplesRemainAuthoritative()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();for(var n=0;n<45;n++)vm.Step();
        var state=JsonSerializer.Serialize(vm.Live!.Session.Capture());
        Assert.Equal(Enum.GetValues<TeamKpi>().Length,vm.ExplorerOptions.Count);
        foreach(var definition in vm.ExplorerOptions) {
            vm.OpenExplorer(definition.Id);
            var card=vm.ExplorerPrimary!;
            var expected=KpiDashboard.Project(vm.Live.Session,new(20,[definition.Id]),120).Single();
            Assert.Equal(expected.Value,card.Observation.Value);Assert.Equal(expected.Trend.Points,card.Observation.Trend.Points);
            Assert.Equal(KpiDisplay.Unit(definition),card.Unit);Assert.Contains(definition.Description,card.ExplorerDescription);
            Assert.False(string.IsNullOrWhiteSpace(card.ShortDefinition));Assert.False(string.IsNullOrWhiteSpace(card.Limitations));
        }
        Assert.Equal(state,JsonSerializer.Serialize(vm.Live.Session.Capture()));
    }
    [Theory]
    [InlineData(TeamKpi.TotalWip)] [InlineData(TeamKpi.DevelopmentQueue)] [InlineData(TeamKpi.ReviewQueue)]
    [InlineData(TeamKpi.TestingQueue)] [InlineData(TeamKpi.TechnicalDebt)] [InlineData(TeamKpi.WipAge)]
    [InlineData(TeamKpi.UnlaunchedDevelopmentCost)]
    public void SnapshotAndBalanceHistoryIgnoreRollingWindow(TeamKpi id)
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();for(var n=0;n<45;n++)vm.Step();vm.OpenExplorer(id);
        var old=vm.ExplorerPrimary!;
        vm.KpiWindow=10;var current=vm.ExplorerPrimary!;
        Assert.Equal(old.Observation.Value,current.Observation.Value);Assert.Equal(old.Trend.Points,current.Trend.Points);
        Assert.Contains("does not change",current.WindowExplanation);Assert.Contains("Day 45",current.ValuePeriod);
    }
    [Fact] public void BalanceBreakdownAndMissingDataArePreserved()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=20});for(var n=0;n<10;n++)live.Step();
        var saved=live.Session.Capture();var legacy=SimulationSession.Restore(saved with {Days=saved.Days.Select(d=>d with{Economics=null}).ToArray()});
        foreach(var session in new[]{live.Session,legacy}) {
            var card=new KpiCard(KpiDashboard.Project(session,new(20,[TeamKpi.UnlaunchedDevelopmentCost]),120).Single());
            var current=card.BalanceHistory!.Current;
            Assert.Equal(current.Unlaunched,current.WorkInProgress+current.AwaitingLaunch);
            Assert.Equal($"{current.WorkInProgress:N0} SEK",card.WipBalance);
            Assert.Equal($"{current.AwaitingLaunch:N0} SEK",card.AwaitingBalance);
            Assert.Equal($"{current.Unlaunched:N0} SEK",card.TotalBalance);
            Assert.Contains("not lost revenue",card.FinancialQualification);Assert.Contains("never invented",card.Limitations);
        }
    }
}
