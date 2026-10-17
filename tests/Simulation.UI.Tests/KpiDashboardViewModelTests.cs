using System.Text.Json;
using Simulation.Application;
using Simulation.Infrastructure;
using Simulation.UI.Controls;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public sealed class KpiDashboardViewModelTests
{
    [Fact]
    public void SelectionHasFourItemLimitAndDoesNotMutateSimulation()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();var state=JsonSerializer.Serialize(vm.Live!.Session.Capture());
        Assert.Equal(4,vm.KpiCards.Count);Assert.Equal(20,vm.KpiWindow);
        vm.SelectKpi(TeamKpi.TotalWip,true);Assert.DoesNotContain(vm.KpiCards,c=>c.Observation.Definition.Id==TeamKpi.TotalWip);
        vm.SelectKpi(TeamKpi.ReworkEffort,false);vm.KpiChoices.Single(c=>c.Definition.Id==TeamKpi.TotalWip).Selected=true;
        Assert.Equal(4,vm.KpiCards.Count);Assert.Contains(vm.KpiCards,c=>c.Name=="Total WIP");
        vm.KpiWindow=40;vm.DashboardVisible=false;
        Assert.Equal(state,JsonSerializer.Serialize(vm.Live.Session.Capture()));
        foreach(var choice in vm.KpiChoices)choice.Selected=false;
        Assert.Empty(vm.KpiCards);Assert.True(vm.NoKpis);Assert.All(vm.KpiChoices,c=>Assert.True(c.CanSelect));
    }
    [Fact]
    public void PlaybackInterventionsRestoreAndResetRefreshWithoutIndependentClock()
    {
        using var vm=new LiveViewModel();vm.Start();for(int i=0;i<40;i++)vm.Tick();vm.Pause();
        Assert.All(vm.KpiCards,c=>Assert.Equal(40,c.Observation.LastDay));
        vm.CheckpointCommand.Execute(null);vm.SelectedCheckpoint=vm.Checkpoints.Single();var expected=JsonSerializer.Serialize(vm.KpiCards);
        vm.Speed=5;vm.Tick();Assert.Equal(expected,JsonSerializer.Serialize(vm.KpiCards));
        vm.BeginChange();vm.Draft.NumberOfTesters="3";vm.Draft.DevelopmentWipLimit="2";vm.ApplyChanges();
        vm.Resume();vm.Tick();vm.Pause();Assert.All(vm.KpiCards,c=>Assert.Equal(41,c.Observation.LastDay));
        vm.RestoreCommand.Execute(null);Assert.Equal(expected,JsonSerializer.Serialize(vm.KpiCards));
        vm.KpiWindow=10;vm.Reset();Assert.Empty(vm.KpiCards);Assert.Equal(10,vm.KpiWindow);
        vm.Start();vm.Pause();vm.Step();Assert.All(vm.KpiCards,c=>Assert.Equal(1,c.Observation.AvailableDays));Assert.All(vm.KpiCards,c=>Assert.Contains("1/10",c.Period));
    }
    [Fact]
    public void SaveLoadRestoresSelectionsWindowVisibilityAndAuthoritativeCards()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();vm.KpiWindow=10;
        vm.SelectKpi(TeamKpi.ReworkEffort,false);vm.SelectKpi(TeamKpi.TechnicalDebt,true);vm.DashboardVisible=false;
        for(int i=0;i<25;i++)vm.Step();var saved=LiveSessionJson.Save(vm.Live!);
        using var loaded=new LiveViewModel();loaded.Load(LiveSessionJson.Load(saved));
        Assert.Equal(vm.KpiSettings.Selected,loaded.KpiSettings.Selected);Assert.Equal(10,loaded.KpiWindow);Assert.False(loaded.DashboardVisible);
        Assert.Equal(JsonSerializer.Serialize(vm.KpiCards),JsonSerializer.Serialize(loaded.KpiCards));
    }
    [Fact]
    public void PresentationUsesNeutralDeltasPercentagePointsAndUnavailableText()
    {
        var d=KpiDashboard.Definitions.Single(x=>x.Id==TeamKpi.DeveloperUtilization);
        var card=new KpiCard(new(d,75,50,21,40,20,20,new([new(40,75)],[])));
        Assert.Equal("Δ +25 pp",card.Change);Assert.Contains("1–20",card.ComparisonHelp);
        var missing=new KpiCard(card.Observation with{Value=null,Previous=null});Assert.Equal("N/A",missing.Value);Assert.Contains("N/A",missing.Change);
        var chart=new LivePerformanceTrendChart{Series=missing.Trend,Unit="%",SamplesOnly=true};Assert.Contains("Day 40",chart.DescribeAtDay(40));
    }
}
