using System.Text.Json;
using System.Text.Json.Nodes;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
namespace Simulation.Application.Tests;
public sealed class DependencyIntegrationTests
{
    static LiveSimulation Start(DependencySettings settings) => LiveSimulation.Start(new() {ResidualDependencies=settings,NumberOfWorkItems=0,RandomSeed=12345},WorkArrivalMode.AlwaysAvailable);
    static void Until(LiveSimulation live,int day){while(live.Session.CurrentDay<day)Assert.True(live.Step());}
    [Fact]
    public void LiveInterventionsOnlyAffectFutureArrivalsAndKeepLabels()
    {
        var live=Start(new());Until(live,30);var existing=live.Session.WorkItems.ToDictionary(w=>w.Id,w=>w.ResidualDependency);
        live.Session.ApplyChanges(live.Session.Configuration with{ResidualDependencies=new(1,5)},"Dependency assumption");Until(live,60);
        Assert.All(live.Session.WorkItems.Where(w=>existing.ContainsKey(w.Id)),w=>Assert.Equal(existing[w.Id],w.ResidualDependency));
        Assert.All(live.Session.WorkItems.Where(w=>w.CreatedDay>=30),w=>Assert.NotNull(w.ResidualDependency));
        var assignments=live.Session.WorkItems.ToDictionary(w=>w.Id,w=>w.ResidualDependency);
        live.Session.ApplyChanges(live.Session.Configuration with{ResidualDependencies=new(.2,100)},"Longer wait");Until(live,80);
        Assert.All(live.Session.WorkItems.Where(w=>assignments.ContainsKey(w.Id)),w=>Assert.Equal(assignments[w.Id],w.ResidualDependency));
        Assert.Equal("Dependency assumption",live.Session.Changes[0].Label);
        var comparison=LivePerformance.Compare(live.Session,live.Session.Changes[0],20);
        Assert.Equal((11,30),(comparison.Before.FirstDay,comparison.Before.LastDay));Assert.Equal((31,50),(comparison.After.FirstDay,comparison.After.LastDay));
        Assert.True(comparison.After.DependenciesRelevant);
    }
    [Fact]
    public void SaveLoadAndCheckpointReproduceContinuationWithDependenciesAndSkills()
    {
        var live=Start(new(.5,5));live.Session.ApplyChanges(live.Session.Configuration with{Skills=new(1,.8),Release=new(ReleaseMode.Scheduled,2,5)});
        Until(live,30);var checkpoint=live.CreateCheckpoint("Dependencies");var saved=LiveSessionJson.Save(live);Until(live,100);
        var expected=JsonSerializer.Serialize(live.Session.Capture());
        var loaded=LiveSessionJson.Load(saved);Until(loaded,100);Assert.Equal(expected,JsonSerializer.Serialize(loaded.Session.Capture()));
        live.RestoreCheckpoint(checkpoint.Id);Until(live,100);Assert.Equal(expected,JsonSerializer.Serialize(live.Session.Capture()));
        Assert.Contains(live.Session.WorkItems,w=>w.RequiresSpecialist && w.ResidualDependency is {WaitingDays:>0});
        Assert.All(live.Session.WorkItems.Where(w=>w.DevelopmentStartedDay is not null && w.ResidualDependency is not null),w=>Assert.True(w.DevelopmentStartedDay>=w.ResidualDependency!.ResolutionDay));
    }
    [Fact]
    public void ZeroWaitPreservesAllExistingMechanismsCostsAndRandomStreams()
    {
        var a=Start(new());var b=Start(new(1,0));
        foreach(var live in new[]{a,b}) live.Session.ApplyChanges(live.Session.Configuration with{
            Skills=new(2,.5),Productivity=new(1.4,.8,1.2),Release=new(ReleaseMode.Scheduled,2,5),
            Debt=new(){ShortcutRate=.4,Repayment=.2,CreationFactor=2,Tolerance=.1},Quality=new(){Enabled=true,CodeReviewDefectProbability=.2,TestingDefectProbability=.2}});
        Until(a,100);Until(b,100);
        static string Observations(LiveSimulation live)
        {
            var node=JsonNode.Parse(JsonSerializer.Serialize(live.Session.Capture()))!;
            void Strip(JsonNode n){if(n is JsonObject obj){obj.Remove("ResidualDependency");obj.Remove("ResidualDependencies");foreach(var child in obj.Select(p=>p.Value).OfType<JsonNode>())Strip(child);}else if(n is JsonArray arr)foreach(var child in arr.OfType<JsonNode>())Strip(child);}
            Strip(node);return node.ToJsonString();
        }
        Assert.Equal(Observations(a),Observations(b));
    }
    [Fact]
    public void TrendMatchesAuthoritativeDerivedHistoryAndMetricsRemainFinite()
    {
        var live=Start(new(.5,5));Until(live,100);
        var series=LivePerformanceTrend.Project(live.Session,LiveTrendMetric.WaitingForDependency,20,null);
        Assert.Equal(live.Session.Days.Select(d=>(double?)d.WaitingForDependencyCount),series.Points.Select(p=>p.Value));
        foreach(var d in live.Session.Days){Assert.InRange(d.WaitingForDependencyCount,0,d.BacklogCount);Assert.Equal(d.Items.Count(w=>w.CreatedDay<=d.Day && w.State!=WorkItemStatus.Backlog && !w.State.IsDelivered()),d.TotalWip);}
        var p=LivePerformance.Rolling(live.Session,50);Assert.True(double.IsFinite(p.Throughput));Assert.True(double.IsFinite(p.WaitingForDependency.Average));Assert.True(double.IsFinite(p.SystemCostPerReleasedItem!.Value));
    }
    [Fact]
    public void LegacySettingsLoadDisabledWithoutReassigningExistingItems()
    {
        var old=LiveSessionJson.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","model04-AlwaysAvailable-live.json")));
        Assert.Equal(new DependencySettings(),old.Session.Configuration.ResidualDependencies);
        Assert.All(old.Session.WorkItems,w=>Assert.Null(w.ResidualDependency));var ids=old.Session.WorkItems.Select(w=>w.Id).ToHashSet();
        old.Session.ApplyChanges(old.Session.Configuration with{ResidualDependencies=new(1,5)});Until(old,old.Session.CurrentDay+20);
        Assert.All(old.Session.WorkItems.Where(w=>ids.Contains(w.Id)),w=>Assert.Null(w.ResidualDependency));
    }
}
