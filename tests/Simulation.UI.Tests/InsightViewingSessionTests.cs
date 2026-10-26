using Simulation.Application;
using Simulation.UI.Insights;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class InsightViewingSessionTests
{
    private static TrackedInsight Item(string id,bool active=true,QueueTrendKind trend=QueueTrendKind.Growing)
    {
        var source=SimulationInsights.Analyze(LiveSimulation.Start(new())).Insights[0];
        return new(source with {Id=id,Relevant=active,QueueDirection=new(trend,1,10,10,1)},active?InsightStatus.New:InsightStatus.Informational);
    }
    [Fact] public void ChangingPriorityAndTrendsNeverMoveEntriesOrGroups()
    {
        var a=Item("a");var b=Item("b",false);var view=new InsightViewingSession();view.Update([b,a]);view.Select("b");
        var headings=view.Entries.Select(e=>e.Heading).ToArray();
        foreach(var trend in new[]{QueueTrendKind.Growing,QueueTrendKind.Stable,QueueTrendKind.Shrinking}) {
            view.Update([Item("b",true,trend),Item("a",false,trend)]);
            Assert.Equal(new[]{"a","b"},view.Entries.Select(e=>e.Id));Assert.Equal(headings,view.Entries.Select(e=>e.Heading));
            Assert.Equal("b",view.SelectedId);Assert.Equal(trend,view.Entries[1].Item.Insight.QueueDirection!.Kind);
        }
    }
    [Fact] public void ExistingTrackerRetainsHistoricalFindingsInTheirOriginalSlots()
    {
        var a=Item("a");var b=Item("b");var tracker=new InsightTracker();
        var report=new InsightReport(10,new(1,10,10,20),[a.Insight,b.Insight]);tracker.Observe(report);
        var view=new InsightViewingSession();view.Update(tracker.Items);view.Select("a");
        tracker.Observe(report with {Insights=[b.Insight]});view.Update(tracker.Items);
        Assert.Equal("a",view.Entries[0].Id);Assert.True(view.Entries[0].Available);
        Assert.Equal(InsightStatus.NoLongerRelevant,view.Entries[0].Item.Status);Assert.Equal("a",view.SelectedId);
    }
    [Fact] public void NewEntriesAppendDeterministicallyWithoutMovingExistingItems()
    {
        var view=new InsightViewingSession();view.Update([Item("z",false)]);
        view.Update([Item("b"),Item("z"),Item("a")]);
        Assert.Equal(new[]{"a","b","z"},view.Entries.Select(e=>e.Id));Assert.Equal("Active findings",view.Entries[0].Heading);
        view.Update([Item("c"),Item("a"),Item("z"),Item("b")]);
        Assert.Equal(new[]{"a","b","c","z"},view.Entries.Select(e=>e.Id));Assert.Null(view.Entries[2].Heading);Assert.Equal("z",view.SelectedId);
    }
    [Fact] public void OrdinaryUpdatesKeepCollectionAndRowIdentity()
    {
        var view=new InsightViewingSession();view.Update([Item("a"),Item("b",false)]);
        var collection=view.Entries;var row=view.Entries[0];var changes=0;var notifications=0;
        collection.CollectionChanged+=(_,_)=>changes++;
        row.PropertyChanged+=(_,_)=>notifications++;
        for(var i=0;i<20;i++)view.Update([Item("b"),Item("a",false,QueueTrendKind.Shrinking)]);
        Assert.Same(collection,view.Entries);Assert.Same(row,view.Entries[0]);
        Assert.Equal(0,changes);Assert.True(notifications>0);
        Assert.Equal(QueueTrendKind.Shrinking,row.Item.Insight.QueueDirection!.Kind);
    }
    [Fact] public void MissingSelectionIsUnavailableRatherThanSilentlyReplaced()
    {
        var view=new InsightViewingSession();view.Update([Item("a"),Item("b")]);view.Select("a");view.Update([Item("b")]);
        Assert.Equal("a",view.SelectedId);Assert.False(view.Entries[0].Available);
        view.Update([Item("a",false),Item("b")]);Assert.True(view.Entries[0].Available);Assert.Equal("a",view.SelectedId);
    }
    [Fact] public void ReopeningRebuildsPriorityAndDefaultSelection()
    {
        var view=new InsightViewingSession();view.Update([Item("a"),Item("b",false)]);view.Select("a");
        var latest=new[]{Item("a",false),Item("b")};view.Update(latest);
        var reopened=new InsightViewingSession();reopened.Update(latest);
        Assert.Equal(new[]{"b","a"},reopened.Entries.Select(e=>e.Id));Assert.Equal("b",reopened.SelectedId);Assert.Equal("a",view.SelectedId);
        var identical=new InsightViewingSession();identical.Update(latest.Reverse());Assert.Equal(reopened.Entries.Select(e=>e.Id),identical.Entries.Select(e=>e.Id));
    }
    [Fact] public void EmptyOpeningAndHistoricalFallbackUseExistingSelectionRules()
    {
        var view=new InsightViewingSession();view.Update([]);Assert.Null(view.SelectedId);
        var old=Item("a",false) with {Status=InsightStatus.NoLongerRelevant};
        var recent=old with {Insight=old.Insight with {Id="b",Period=new(20,30,10,20)}};
        view.Update([old,recent]);Assert.Equal("b",view.SelectedId);
        var reopened=new InsightViewingSession();reopened.Update([old,Item("information",false)]);Assert.Equal("information",reopened.SelectedId);
    }
}
