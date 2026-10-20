using System.Text.Json;
using Avalonia.Input;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Controls;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class StableInspectionTests
{
    private static StationDetails Details(params InspectedWorkItem[] items) => new("Testing",[],""){Items=items};
    private static InspectedWorkItem Item(string id,WorkItemStatus state=WorkItemStatus.WaitingForTesting) => new(id,state,false,"test");
    [Fact] public void StableIdentityExpansionAndCollectionSurvivePropertiesAndGroupChanges()
    {
        var presentation=new WorkflowInspectionPresentation();var session=Guid.NewGuid();var collection=presentation.Items;
        presentation.Refresh(session,Details(Item("A"),Item("B")));var a=presentation.Items.Single(i=>i.Id=="A");a.Expanded=true;
        var reset=false;presentation.Items.CollectionChanged+=(_,e)=>reset|=e.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Reset;
        presentation.Refresh(session,Details(Item("A",WorkItemStatus.Testing) with {StateDays="8 days"},Item("B")));
        Assert.Same(collection,presentation.Items);Assert.Same(a,presentation.Items.Single(i=>i.Id=="A"));Assert.True(a.Expanded);
        Assert.Equal("Active work",a.Data.Group);Assert.Equal("8 days",a.Data.StateDays);Assert.False(reset);
        presentation.Refresh(session,Details(Item("B")));Assert.DoesNotContain(presentation.Items,i=>i.Id=="A");Assert.Contains("left",presentation.MembershipNotice);
    }
    [Fact] public void SwitchingResetAndRestoringDiscardStaleInteractionObjects()
    {
        var p=new WorkflowInspectionPresentation();var id=Guid.NewGuid();p.Refresh(id,Details(Item("A")));var old=p.Items[0];old.Expanded=true;
        p.Refresh(id,Details(Item("A")) with {Title="Testing queue"});Assert.NotSame(old,p.Items[0]);Assert.False(p.Items[0].Expanded);
        old=p.Items[0];p.Refresh(Guid.NewGuid(),Details(Item("A")));Assert.NotSame(old,p.Items[0]);
        p.Refresh(Guid.Empty,StationDetails.Empty);Assert.Empty(p.Items);
    }
    [Fact] public void GroupsAreDisjointAndDoNotInferBlockageFromAge()
    {
        Assert.Equal("Waiting for capacity",(Item("A") with {StateDays="999 days"}).Group);
        Assert.Equal("Blocked work",(Item("B",WorkItemStatus.Backlog) with {PrerequisiteBlocked=true}).Group);
        Assert.Equal("Blocked work",new InspectedWorkItem("C",WorkItemStatus.Backlog,true,"").Group);
        Assert.Equal("Ready for release",Item("D",WorkItemStatus.ReadyForRelease).Group);
        Assert.Equal("Delivered work",Item("E",WorkItemStatus.Done).Group);
        Assert.Equal("Active work",Item("F",WorkItemStatus.Testing).Group);
    }
    [Theory]
    [InlineData(WorkItemStatus.WaitingForTesting,"Waiting for testing")]
    [InlineData(WorkItemStatus.WaitingForCodeReview,"Waiting for code review")]
    [InlineData(WorkItemStatus.ReadyForRelease,"Ready for release")]
    public void StatusNamesAreHumanReadable(WorkItemStatus state,string display) => Assert.Equal(display,WorkflowStateNames.Display(state));
    [Theory]
    [InlineData(KeyModifiers.None,1,true,false)] [InlineData(KeyModifiers.None,-1,false,false)]
    [InlineData(KeyModifiers.Meta,1,true,true)] [InlineData(KeyModifiers.Control,-1,false,true)]
    [InlineData(KeyModifiers.Control,1,true,false)] [InlineData(KeyModifiers.Meta,0,true,false)]
    public void OnlyExplicitZoomConsumesWheel(KeyModifiers modifiers,double delta,bool mac,bool expected) => Assert.Equal(expected,WorkshopWheel.IsZoom(modifiers,delta,mac));
    [Fact] public void ThirtyLiveDaysKeepRemainingRowsWithoutMutatingSimulation()
    {
        var live=LiveSimulation.Start(BaselineScenario.CreateRequest() with{NumberOfWorkItems=80,TesterCount=0},WorkArrivalMode.FixedBacklog);
        for(var n=0;n<60;n++)live.Step();
        var p=new WorkflowInspectionPresentation();
        void Refresh(){var s=GameSceneState.From(live);p.Refresh(s.SessionIdentity,StationDetails.For("Testing",s));}
        Refresh();var row=p.Items.First();row.Expanded=true;
        for(var n=0;n<30;n++){live.Step();var before=JsonSerializer.Serialize(live.Capture());Refresh();Assert.Same(row,p.Items.Single(i=>i.Id==row.Id));Assert.True(row.Expanded);Assert.Equal(before,JsonSerializer.Serialize(live.Capture()));}
        var checkpoint=live.Capture();live.Step();live=LiveSimulation.Restore(checkpoint);Refresh();Assert.NotSame(row,p.Items.Single(i=>i.Id==row.Id));
    }
}
