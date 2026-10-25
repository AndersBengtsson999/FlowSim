using Simulation.Application;
using Simulation.UI.Insights;
using Xunit;
namespace Simulation.UI.Tests;
public sealed class InsightPresentationTests
{
    [Fact] public void PresentationPreservesEveryObservationAndPrioritizesActiveFindings()
    {
        var live=LiveSimulation.Start(new());for(var n=0;n<6;n++)live.Step();
        var report=SimulationInsights.Analyze(live);
        var source=report.Insights[0];
        TrackedInsight[] items=[new(source with {Id="previous",Relevant=false},InsightStatus.NoLongerRelevant),new(source with {Id="current",Relevant=false},InsightStatus.Informational),new(source with {Id="active",Relevant=true},InsightStatus.New)];
        var ordered=InsightPresentation.Ordered(items);
        Assert.Equal(new[]{"active","current","previous"},ordered.Select(i=>i.Insight.Id));
        Assert.All(items,i=>Assert.Contains(i,ordered));
        Assert.Same(items[2].Insight,ordered[0].Insight);
        Assert.Equal("Previous findings · no longer relevant",InsightPresentation.GroupTitle(items[0]));
        Assert.Equal("No longer relevant",items[0].StatusLabel);
        Assert.Contains("1 active finding",InsightPresentation.Overview(items));
        Assert.Contains("1 previous finding",InsightPresentation.Overview(items));
    }
    [Fact] public void DisplayWordingDoesNotChangeSourceTextOrExposeSampleCountsInRange()
    {
        const string source="Testing: No sustained pressure established.";
        Assert.Equal("Testing: No consistent queue pressure detected.",InsightPresentation.Plain(source));
        Assert.Equal("Testing: No sustained pressure established.",source);
        Assert.Equal("Days 32–35",InsightPresentation.Range(new(32,35,4,4)));
        Assert.Equal("No completed days",InsightPresentation.Range(new(null,null,0,4)));
    }
}
