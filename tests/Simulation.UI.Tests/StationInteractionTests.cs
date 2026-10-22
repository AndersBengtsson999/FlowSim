using System.Text.Json;
using Avalonia;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;

public sealed class StationInteractionTests
{
    [Theory]
    [InlineData(307, 209, "Backlog")] [InlineData(365, 225, "Backlog")]
    [InlineData(555, 165, "Development")] [InlineData(639, 225, "Development")]
    [InlineData(823, 256, "Code Review")] [InlineData(937, 282, "Code Review")]
    [InlineData(710, 404, "Testing")] [InlineData(670, 471, "Testing")]
    [InlineData(449, 448, "Ready for Release")] [InlineData(448, 492, "Ready for Release")]
    [InlineData(214, 350, "Release")] [InlineData(284, 354, "Release")]
    [InlineData(450, 310, "Technical Debt")]
    public void FurnitureAndQueueTargetsMatchStation(double x, double y, string station) => Assert.Equal(station, StationMap.HitTest(new(x, y)));
    [Fact] public void DesignatedTargetsNeverOverlapAndOutsideDoesNotSelect()
    {
        for (var y = 0; y <= 620; y += 3)
        for (var x = 0; x <= 1080; x += 3)
            Assert.InRange(StationMap.Regions.Count(r => r.Contains(new(x, y))), 0, 1);
        Assert.Null(StationMap.HitTest(new(20, 20))); Assert.Null(StationMap.HitTest(new(1050, 600)));
    }
    [Fact] public void KeyboardOrderWrapsWithoutInventingStations()
    {
        Assert.Equal("Backlog", StationMap.Move(null, 1)); Assert.Equal("Technical Debt", StationMap.Move(null, -1));
        var selected = "Backlog";
        for (var i = 0; i < 7; i++) selected = StationMap.Move(selected, 1);
        Assert.Equal("Backlog", selected); Assert.Equal("Technical Debt", StationMap.Move(selected, -1));
    }
    [Theory] [InlineData(900, 600, false)] [InlineData(1600, 850, true)] [InlineData(1120, 700, true)] [InlineData(1119, 700, false)]
    public void ResponsiveLayoutKeepsPositiveWorldAndOnlyReservesOpenPanel(double width, double remaining, bool side)
    {
        Assert.Equal(side, StationMap.SidePanel(width));
        var closed = StationMap.FitHeight(remaining, 110, side, false);
        var opened = StationMap.FitHeight(remaining, 110, side, true);
        Assert.InRange(opened, 240, remaining); Assert.True(opened <= closed);
        if (side) Assert.Equal(closed, opened); else Assert.Equal(210, closed - opened);
    }
    private static LiveSimulation Start(int testers = 0) => LiveSimulation.Start(BaselineScenario.CreateRequest() with
    { NumberOfWorkItems = 38, DeveloperCount = 5, TesterCount = testers, DevelopmentEffort = 1, CodeReviewEffort = 1,
      DevelopmentWipLimit = 20, CodeReviewWipLimit = 20, TestingWipLimit = 3 }, WorkArrivalMode.FixedBacklog);
    [Fact] public void TestingUsesExactWaitingWipStaffAndObservedCapacity()
    {
        var live = Start(); for (var i = 0; i < 100; i++) live.Step();
        var detail = StationDetails.For("Testing", GameSceneState.From(live));
        string Value(string label) => detail.Rows.Single(x => x.Label == label).Value;
        Assert.Equal("35", Value("Waiting")); Assert.Equal("3 / 3", Value("Active WIP / limit")); Assert.Equal("0", Value("Testers · configured"));
        Assert.Equal("0 / 0", Value("Tester used / available · last day")); Assert.Equal("Unchanged", Value("Queue change · last day"));
        Assert.Equal("No testers", Value("Observed condition"));
    }
    [Fact] public void SelectionMappingUpdatesAfterInterventionAndRestorationWithoutStaleMetrics()
    {
        var live = Start(); for (var i = 0; i < 30; i++) live.Step(); var checkpoint = live.Capture();
        var initial = StationDetails.For("Testing", GameSceneState.From(live));
        live.Session.ApplyChanges(live.Session.Configuration with { Team = live.Session.Configuration.Team with { TesterCount = 5 } }, "Recovery");
        for (var i = 0; i < 10; i++) live.Step();
        var after = StationDetails.For("Testing", GameSceneState.From(live)); Assert.NotEqual(initial.Rows, after.Rows);
        var restored = StationDetails.For("Testing", GameSceneState.From(LiveSimulation.Restore(checkpoint)));
        Assert.Equal(initial.Rows, restored.Rows); Assert.Equal(initial.Explanation, restored.Explanation);
    }
    [Fact] public void ReviewDoesNotInventStageSpecificAvailableCapacityAndDayZeroOmitsObservations()
    {
        var live = Start(2); var zero = StationDetails.For("Code Review", GameSceneState.From(live));
        Assert.DoesNotContain(zero.Rows, r => r.Label.Contains("used")); Assert.Contains(zero.Rows, r => r.Value == "No completed day yet");
        for (var i = 0; i < 8; i++) live.Step(); var state = GameSceneState.From(live); var review = StationDetails.For("Code Review", state);
        Assert.Contains(review.Rows, r => r.Label == "Shared Dev used / available · last day");
        Assert.DoesNotContain(review.Rows, r => r.Label.Contains("Review") && r.Label.Contains("available"));
        Assert.Equal(live.CurrentSnapshot.AvailableDeveloperCapacity, state.AvailableDeveloperCapacity);
    }
    [Fact] public void ScheduledBatchAndTrendAreSeparateFacts()
    {
        var p = QueuePressure.Classify(35, 5, "", [20, 25, 30, 35], [true, true, true], "", true);
        Assert.Equal("Growing", p.Trend); Assert.Equal(QueuePressureKind.Batching, p.Kind);
        var state = GameSceneState.From(Start()) with { QueuePressures = new Dictionary<string, QueuePressure> { ["ready"] = p } };
        var details = StationDetails.For("Ready for Release", state);
        Assert.Contains(details.Rows, r => r.Value == "Scheduled batch"); Assert.Contains(details.Rows, r => r.Value == "Growing");
        Assert.Contains("Scheduled batch", StationDetails.Summary(state)); Assert.DoesNotContain("bottleneck", StationDetails.Summary(state));
    }
    [Fact] public void TrendComparesTheFirstTwoEffectiveDaysAfterAnIntervention()
    {
        var live = Start(2); for (var i = 0; i < 10; i++) live.Step();
        live.Session.ApplyChanges(live.Session.Configuration with { Team = live.Session.Configuration.Team with { TesterCount = 0 } }, "Stop testing");
        live.Step(); Assert.Equal("No recent comparison", GameSceneState.From(live).QueuePressures["testing-wait"].Trend);
        live.Step(); Assert.NotEqual("No recent comparison", GameSceneState.From(live).QueuePressures["testing-wait"].Trend);
    }
    [Theory] [InlineData(7)] [InlineData(12345)] [InlineData(9876)]
    public void InspectingEveryStationPreservesCompleteSeededStateHistoryAndResults(int seed)
    {
        var a = LiveSimulation.Start(BaselineScenario.DefectsAndReworkExample() with { RandomSeed = seed }, WorkArrivalMode.AlwaysAvailable);
        a.CreateCheckpoint("Shared"); var b = LiveSimulation.Restore(a.Capture());
        for (var day = 0; day < 100; day++)
        {
            a.Step(); b.Step(); var state = GameSceneState.From(a);
            foreach (var station in StationMap.Names) { _ = StationDetails.For(station, state); _ = StationDetails.Summary(state); }
            Assert.Equal(JsonSerializer.Serialize(b.Capture()), JsonSerializer.Serialize(a.Capture()));
        }
        Assert.Equal(JsonSerializer.Serialize(b.Session.GetResult()), JsonSerializer.Serialize(a.Session.GetResult()));
        Assert.Equal(StationDetails.Empty, StationDetails.For("Testing", GameSceneState.From(null)));
    }
}
