using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Insights;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class QueueTrendDiagnosisTests
{
    public static LiveSimulation Reproduction()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=0,RandomSeed=12345,TestingEffort=5,TesterCount=1},WorkArrivalMode.ContinuousArrival,.4m);
        for(var day=0;day<70;day++)live.Step();return live;
    }
    [Fact] public void ElevenWaitingOnDay70WithSustainedGrowthNoLongerRemainsStable()
    {
        var live=Reproduction();var t=QueueTrend.From(live)["testing-wait"];
        Assert.Equal(11,live.CurrentSnapshot.WaitingForTestingCount);Assert.Equal(70,live.Session.CurrentDay);
        Assert.Equal(9,t.StartingCount);Assert.Equal(11,t.EndingCount);Assert.Equal(61,t.FirstDay);Assert.Equal(70,t.LastDay);
        Assert.Equal(.2,t.Slope!.Value,8);Assert.Equal(1.8,t.FittedChange!.Value,8);
        Assert.Equal(2,t.EntryThreshold);Assert.Equal(1,t.RetentionThreshold);
        Assert.Equal(QueueTrendKind.Stable,t.RawKind);Assert.Equal(QueueTrendKind.Growing,t.Kind);Assert.Equal(2,t.Confirmations);
        var before=JsonSerializer.Serialize(live.Session.Capture());
        Assert.Equal(t,GameSceneState.From(live).QueueTrends["testing-wait"]);
        Assert.Equal(t,SimulationInsights.Analyze(live).Insights.Single(i=>i.Id=="queue.testing-wait").QueueDirection);
        Assert.Contains("+0.2 items/day",StationDetails.For("Testing",GameSceneState.From(live)).Observation);
        Assert.Equal(before,JsonSerializer.Serialize(live.Session.Capture()));
    }
    [Theory][InlineData(0)][InlineData(1000)][InlineData(1000000)]
    public void PeriodicGradualGrowthAndReductionAreOffsetInvariant(int offset)
    {
        var live=Reproduction();var counts=live.Session.Days.Select(d=>d.WaitingForTestingCount).ToArray();
        Assert.Equal(QueueTrendKind.Growing,QueueTrend.Calculate(counts.Select((v,i)=>(i+1,offset+v))).Kind);
        Assert.Equal(QueueTrendKind.Shrinking,QueueTrend.Calculate(counts.Select((v,i)=>(i+1,offset+100-v))).Kind);
    }
    [Fact] public void SuppliedGradualSequenceIsGrowingWithoutRelaxingEntryThreshold()
    {
        int[] counts=[2,2,3,3,4,4,5,6,6,7,8,8,9,10,11];
        var trace=QueueTrend.Trace(counts.Select((v,i)=>(i+1,v)));
        Assert.Equal(QueueTrendKind.Growing,trace[^1].Kind);Assert.All(trace.Skip(3),t=>Assert.True(t.EntryThreshold>=2));
        Assert.True(trace.First(t=>t.Kind==QueueTrendKind.Growing).LastDay<=10);
    }
    [Theory][InlineData(1)][InlineData(2)][InlineData(10)][InlineData(100)]
    public void AlternatingNoiseAcrossSizesDoesNotBecomeDirectional(int amplitude)
    {
        var samples=Enumerable.Range(1,60).Select(day=>(day,1000+(day%2)*amplitude));
        Assert.All(QueueTrend.Trace(samples).Skip(3),t=>Assert.Equal(QueueTrendKind.Stable,t.Kind));
    }
    [Fact] public void GrowthEventuallyFlattensAndReversesWithoutStickyConfirmation()
    {
        var counts=Reproduction().Session.Days.Select(d=>d.WaitingForTestingCount).ToArray();
        var flat=counts.Concat(Enumerable.Repeat(11,12)).Select((v,i)=>(i+1,v));
        Assert.Equal(QueueTrendKind.Stable,QueueTrend.Calculate(flat).Kind);
        var reverse=counts.Concat(Enumerable.Range(1,10).Select(i=>11-i)).Select((v,i)=>(i+1,v));
        Assert.Equal(QueueTrendKind.Shrinking,QueueTrend.Calculate(reverse).Kind);
    }
    [Fact] public void DiagnosticsUseSameResultAndActualEndpointsForEveryPrefix()
    {
        var history=Reproduction().Session.Days.Select(d=>(Day:d.Day+1,Count:d.WaitingForTestingCount)).ToArray();
        var trace=QueueTrend.Trace(history);
        for(var i=0;i<history.Length;i++)Assert.Equal(QueueTrend.Calculate(history.Take(i+1)),trace[i]);
        Assert.Equal(history[^1].Count,trace[^1].EndingCount);
    }
}
