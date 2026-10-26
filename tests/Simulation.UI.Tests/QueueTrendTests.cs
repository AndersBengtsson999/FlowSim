using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Insights;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class QueueTrendTests
{
    private static QueueTrend Trend(params int[] counts)=>QueueTrend.Calculate(counts.Select((v,i)=>(i+1,v)));
    [Fact] public void GrowingAndShrinkingAreDetectedFromWholeWindow()
    {
        Assert.Equal(QueueTrendKind.Growing,Trend(1,2,3,4,5,6,7,8,9,8).Kind);
        Assert.Equal(QueueTrendKind.Shrinking,Trend(12,11,10,9,8,7,6,5,4,5).Kind);
    }
    [Fact] public void SmallFluctuationsAndAlternatingChangesRemainStable()
    {
        Assert.Equal(QueueTrendKind.Stable,Trend(10,11,10,9,10,11,10,9,10,11).Kind);
        Assert.Equal(QueueTrendKind.Stable,Trend(10,12,10,12,10,12,10,12,10,12).Kind);
        Assert.Equal(QueueTrendKind.Stable,Trend(0,0,0,0,0,0,0,0,0,0).Kind);
    }
    [Fact] public void EntryNeedsTwoWindowsAndDoesNotOverreactToOneItem()
    {
        Assert.Equal(QueueTrendKind.Stable,Trend(1,2,3,4).Kind);
        Assert.Equal(QueueTrendKind.Growing,Trend(1,2,3,4,5).Kind);
        Assert.Equal(QueueTrendKind.Stable,Trend(10,10,10,10,10,11,11,11,11,11).Kind);
    }
    [Fact] public void SustainedReversalIsDetectedWithinOneWindow()
    {
        var up=Enumerable.Range(0,20).ToArray();
        var reversed=up.Concat(Enumerable.Range(1,10).Select(i=>19-i)).ToArray();
        Assert.Equal(QueueTrendKind.Growing,Trend(up).Kind);
        Assert.Equal(QueueTrendKind.Shrinking,Trend(reversed).Kind);
        Assert.Equal(QueueTrendKind.Stable,Trend(up.Concat(Enumerable.Repeat(19,12)).ToArray()).Kind);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(3)]
    public void ShortHistoryIsInsufficient(int count)=>Assert.Equal(QueueTrendKind.InsufficientData,Trend(Enumerable.Range(0,count).ToArray()).Kind);
    [Fact] public void ActualDayWindowDoesNotInventMissingSamples()
    {
        var trend=QueueTrend.Calculate(new[]{(1,1),(2,2),(3,3),(4,4),(5,5),(30,6)});
        Assert.Equal(QueueTrendKind.InsufficientData,trend.Kind);Assert.Equal(1,trend.Samples);Assert.Equal(30,trend.FirstDay);
    }
    private static LiveSimulation Live()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=80,DeveloperCount=6,TesterCount=1,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=5,DevelopmentWipLimit=20,CodeReviewWipLimit=20,TestingWipLimit=6},WorkArrivalMode.ContinuousArrival,3);
        for(var n=0;n<35;n++)live.Step();return live;
    }
    [Fact] public void ChangesResetObservationsAtNextDayAndRecover()
    {
        var live=Live();live.Session.ApplyChanges(live.Session.Configuration with {TestingWipLimit=7},"WIP");
        Assert.All(QueueTrend.From(live).Values,t=>Assert.Equal(QueueTrendKind.InsufficientData,t.Kind));
        for(var n=0;n<3;n++)live.Step();Assert.Equal(QueueTrendKind.InsufficientData,QueueTrend.From(live)["testing-wait"].Kind);
        for(var n=0;n<7;n++)live.Step();var trend=QueueTrend.From(live)["testing-wait"];
        Assert.Equal(36,trend.FirstDay);Assert.Equal(45,trend.LastDay);Assert.NotEqual(QueueTrendKind.InsufficientData,trend.Kind);
    }
    [Fact] public void GameAndInsightsShareLabelsAndDoNotMutateHistoryOrPressure()
    {
        var live=Live();var before=JsonSerializer.Serialize(live.Session.Capture());var pressure=JsonSerializer.Serialize(QueuePressure.From(live));
        var scene=GameSceneState.From(live);var report=SimulationInsights.Analyze(live);
        foreach(var key in new[]{"backlog","review-wait","testing-wait"}) {
            var insight=report.Insights.Single(i=>i.Id=="queue."+key);
            Assert.Equal(scene.QueueTrends[key],insight.QueueDirection);
            Assert.Contains(insight.QueueDirection!.Label,InsightPresentation.Title(new(insight,InsightStatus.New)));
        }
        Assert.Contains(scene.QueueTrends["testing-wait"].Label,StationDetails.For("Testing",scene).CompactSummary);
        Assert.Equal(before,JsonSerializer.Serialize(live.Session.Capture()));Assert.Equal(pressure,JsonSerializer.Serialize(QueuePressure.From(live)));
        Assert.Equal(JsonSerializer.Serialize(QueueTrend.From(live)),JsonSerializer.Serialize(QueueTrend.From(LiveSimulation.Restore(live.Capture()))));
    }
    [Fact] public void CallingOnlyAtEndMatchesEveryDayAndRepeatedReads()
    {
        var live=Live();var restored=LiveSimulation.Restore(live.Capture());
        for(var n=0;n<12;n++){live.Step();QueueTrend.From(live);restored.Step();}
        Assert.Equal(JsonSerializer.Serialize(QueueTrend.From(live)),JsonSerializer.Serialize(QueueTrend.From(restored)));
    }
}
