using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Insights;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class LaunchInsightsUsabilityTests
{
    private static TrackedInsight Finding(string id,bool active,InsightStatus status,int lastDay)
    {
        var source=SimulationInsights.Analyze(LiveSimulation.Start(new())).Insights[0];
        return new(source with {Id=id,Relevant=active,Period=new(lastDay-3,lastDay,4,4)},status);
    }
    [Fact] public void DefaultsUseExistingActiveOrderThenInformationThenLatestHistory()
    {
        var history=Finding("old",false,InsightStatus.NoLongerRelevant,10);
        var recent=Finding("recent",false,InsightStatus.NoLongerRelevant,20);
        var info=Finding("info",false,InsightStatus.Informational,30);
        var active=Finding("active",true,InsightStatus.New,5);
        var active2=Finding("active2",true,InsightStatus.Updated,40);
        Assert.Equal("active",InsightPresentation.Selection([history,info,active2,active],null));
        Assert.Equal("info",InsightPresentation.Selection([history,info],null));
        Assert.Equal("recent",InsightPresentation.Selection([history,recent],null));
        Assert.Null(InsightPresentation.Selection([],null));
    }
    [Fact] public void ManualSelectionSurvivesClassificationChangesUntilEntryDisappears()
    {
        var old=Finding("selected",false,InsightStatus.NoLongerRelevant,10);
        var active=Finding("active",true,InsightStatus.New,20);
        Assert.Equal("selected",InsightPresentation.Selection([old,active],"selected"));
        Assert.Equal("active",InsightPresentation.Selection([active],"selected"));
        Assert.Equal("selected",InsightPresentation.Selection([old with {Insight=old.Insight with {Relevant=true}},active],"selected"));
        Assert.Null(InsightPresentation.Selection([],"selected"));
    }
    private static LiveViewModel Ready()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=30,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=1},WorkArrivalMode.ContinuousArrival,2);
        for(var n=0;n<20;n++)live.Step();
        var vm=new LiveViewModel();vm.Load(live);return vm;
    }
    [Fact] public void RunningActionsAreDisabledAndExplainPauseWithoutChangingState()
    {
        using var vm=Ready();vm.Resume();
        Assert.False(vm.LaunchProductCommand.CanExecute(null));Assert.False(vm.PlanLaunchCommand.CanExecute(null));
        Assert.Equal("Pause the simulation to enable customer launch.",vm.LaunchNowHelp);Assert.Equal("Pause the simulation to enable customer launch.",vm.ScheduleLaunchHelp);
        var before=JsonSerializer.Serialize(vm.Live!.Session.Capture());
        vm.LaunchProductCommand.Execute(null);vm.PlanLaunchCommand.Execute(null);
        Assert.Equal(before,JsonSerializer.Serialize(vm.Live.Session.Capture()));Assert.True(vm.IsRunning);
    }
    [Fact] public void ExistingCommandsScheduleThenLaunchOnlyInternalReleases()
    {
        using var vm=Ready();Assert.True(vm.AwaitingCustomerLaunch>0);Assert.True(vm.LaunchProductCommand.CanExecute(null));
        Assert.Empty(vm.Live!.Session.ProductDelivery.CustomerLaunches);
        var awaiting=vm.AwaitingCustomerLaunch;var releases=vm.Live.Session.ProductDelivery.InternalReleases.Count;
        vm.PlanningUnit="Working days";vm.LaunchDelay="3";vm.PlanLaunchCommand.Execute(null);
        Assert.Equal($"Scheduled Day {vm.Day+3}",vm.CustomerLaunchPlan);
        vm.LaunchProductCommand.Execute(null);
        Assert.Equal(awaiting,Assert.Single(vm.Live.Session.ProductDelivery.CustomerLaunches).ItemIds.Count);
        Assert.Equal(releases,vm.Live.Session.ProductDelivery.InternalReleases.Count);
        Assert.Equal(0,vm.AwaitingCustomerLaunch);Assert.False(vm.LaunchProductCommand.CanExecute(null));
        Assert.Contains("No internally released",vm.LaunchNowHelp);
    }
    [Fact] public void CompactChoicePersistsWithinSessionAndResetsOnLoad()
    {
        using var vm=Ready();Assert.False(vm.ProductDetailsExpanded);vm.ProductDetailsExpanded=true;vm.Step();Assert.True(vm.ProductDetailsExpanded);
        vm.Visualization="Flow Board";vm.Visualization="Game View";Assert.True(vm.ProductDetailsExpanded);
        vm.ProductDetailsExpanded=false;vm.Step();Assert.False(vm.ProductDetailsExpanded);
        vm.ProductDetailsExpanded=true;vm.Load(LiveSimulation.Restore(vm.Live!.Capture()));Assert.False(vm.ProductDetailsExpanded);
        Assert.Contains("awaiting customer launch",vm.ProductCompactSummary);
    }
    [Fact] public void MissingSessionAndInterventionExplainDisabledActions()
    {
        using var vm=new LiveViewModel();Assert.Contains("Start LIVE",vm.LaunchNowHelp);Assert.False(vm.PlanLaunchCommand.CanExecute(null));
        vm.Start();vm.Pause();vm.ChangeCommand.Execute(null);
        Assert.Contains("intervention",vm.LaunchNowHelp);Assert.False(vm.LaunchProductCommand.CanExecute(null));
    }
}
