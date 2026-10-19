using Simulation.Application;
using Simulation.Infrastructure;
using Simulation.UI.Controls;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class ScenarioKpiExplorerTests
{
    [Fact]
    public async Task ModesPreserveCardsAndRollingWindowAndRejectStaleExperiments()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();vm.OpenExplorer(TeamKpi.CycleTime);vm.KpiWindow=40;
        vm.ComparisonKpi=vm.ExplorerOptions.Single(d=>d.Id==TeamKpi.TotalTeamCost);var selected=vm.KpiSettings.Selected;
        vm.ExplorerMode="Compare Scenarios";Assert.True(vm.CompareScenarios);Assert.Contains("No active",vm.ScenarioStatus);
        vm.EvaluationDays=10;vm.BeginGuidedExperiment();vm.Draft.NumberOfDevelopers="8";vm.ApplyChanges();await vm.BaselineTask;
        Assert.Equal(0,vm.Day);vm.Step();Assert.Equal(1,vm.ScenarioComparison!.ObservedDays);Assert.Equal(7,vm.ScenarioRows.Count);
        vm.Speed=5;vm.Tick();Assert.Equal(1,vm.ScenarioComparison.ObservedDays);vm.Resume();vm.Tick();vm.Pause();Assert.Equal(2,vm.ScenarioComparison.ObservedDays);
        vm.ScenarioKpi=vm.ScenarioOptions.Single(d=>d.Id==TeamKpi.TotalTeamCost);
        Assert.Equal(vm.ScenarioBaseline.Points.Select(p=>p.Day),vm.ScenarioIntervention.Points.Select(p=>p.Day));
        vm.ExplorerMode="Compare KPIs";Assert.Equal(TeamKpi.TotalTeamCost,vm.ComparisonKpi!.Id);Assert.Equal(40,vm.KpiWindow);Assert.Equal(selected,vm.KpiSettings.Selected);
        vm.ExplorerMode="Compare Scenarios";vm.SelectedCheckpoint=vm.Checkpoints.Single();vm.RestoreCommand.Execute(null);
        Assert.NotNull(vm.ScenarioComparison!.Unavailable);Assert.Empty(vm.ScenarioRows);
        vm.Load(LiveSessionJson.Load(LiveSessionJson.Save(vm.Live!)));Assert.Empty(vm.ScenarioRows);
        vm.Reset();Assert.Empty(vm.ScenarioRows);
    }
    [Fact]
    public void SharedChartScaleAndHoverPreserveOriginalUnitsAndMissingValues()
    {
        var chart=new LivePerformanceTrendChart { SamplesOnly=true,Unit="SEK",Series=new([new(101,10),new(102,null)],[]),ComparisonSeries=new([new(101,100),new(102,-5)],[]) };
        Assert.Equal((-5d,100d),chart.ValueScale);
        var tip=chart.DescribeAtDay(101);Assert.Contains("Baseline: 10 SEK",tip);Assert.Contains("Intervention: 100 SEK",tip);Assert.Contains("Difference: 90 SEK",tip);
        Assert.Contains("Difference: N/A",chart.DescribeAtDay(102));
    }
    [Fact]
    public void ScenarioPickerKeepsStableItemsDuringSelectionNotifications()
    {
        using var vm=new LiveViewModel();var items=vm.ScenarioOptions;var notifications=0;
        vm.PropertyChanged+=(_,_)=>{Assert.Same(items,vm.ScenarioOptions);notifications++;};
        vm.ExplorerMode="Compare Scenarios";vm.ScenarioKpi=items.Single(d=>d.Id==TeamKpi.TotalTeamCost);
        var count=notifications;vm.ExplorerMode="Compare Scenarios";vm.ScenarioKpi=vm.ScenarioKpi;
        Assert.Equal(count,notifications);Assert.Same(items,vm.ScenarioOptions);
    }
}
