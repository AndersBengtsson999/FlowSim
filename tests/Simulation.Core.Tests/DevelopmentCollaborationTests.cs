using System.Text.Json;
using Simulation.Core;
using Xunit;

namespace Simulation.Core.Tests;

public sealed class DevelopmentCollaborationTests
{
    private static SimulationResult Run(double[] efforts, int developers = 5, int wip = 10, double capacity = 1, int days = 1) =>
        new SimulationEngine().Run(new("Collaboration", days, new(developers, 10, capacity), wip, 10, 10,
            efforts.Select((effort, i) => new WorkItem($"W{i}", "Work", effort, 3, 2)).ToArray()));

    [Theory]
    [InlineData(1, 2, 1.5)] [InlineData(2, 4, 3)] [InlineData(3, 5, 4)]
    [InlineData(5, 5, 5)] [InlineData(10, 5, 5)]
    public void WipCapsItemsWhileTwoContributionsConsumeCapacity(int wip, double consumed, double effective)
    {
        var result = Run(Enumerable.Repeat(10d, 20).ToArray(), wip: wip);
        var day = result.Days.Single();
        Assert.Equal(wip, day.DevelopmentWip); Assert.Equal(wip, day.DevelopmentCount);
        Assert.Equal(consumed, day.UsedDevelopmentCapacity); Assert.Equal(effective, day.DevelopmentWork);
        Assert.Equal(consumed / 5, result.DeveloperUtilization);
        Assert.Equal(consumed / 5, result.DevelopmentUtilization);
        Assert.Equal(consumed, day.Items.Sum(w => w.UsedDevelopmentCapacity));
        Assert.Equal(effective, day.Items.Sum(w => w.DevelopmentWork));
        Assert.Equal(day.CollaborationDevelopmentCapacity, day.Items.Sum(w => w.CollaborationDevelopmentCapacity));
        Assert.All(day.Items, w => { Assert.InRange(w.DevelopmentWork, 0, 1.5); Assert.InRange(w.UsedDevelopmentCapacity, 0, 2); });
    }

    [Fact]
    public void AllPrimaryOpportunitiesPrecedeCollaboration()
    {
        var items = Run([10, 10, 10], developers: 4).Days.Single().Items;
        Assert.Equal(new[] { 1d, 1d, 1d }, items.Select(w => w.PrimaryDevelopmentCapacity));
        Assert.Equal(new[] { 1d, 0d, 0d }, items.Select(w => w.CollaborationDevelopmentCapacity));
        var starved = Run([10, 10, 10], developers: 2).Days.Single().Items;
        Assert.Equal(new[] { 1d, 1d, 0d }, starved.Select(w => w.PrimaryDevelopmentCapacity));
        Assert.All(starved, w => Assert.Equal(0, w.CollaborationDevelopmentCapacity));
    }

    [Fact]
    public void ClosestRemainingEffortGetsCollaborationBeforeOlderLongerWork()
    {
        var day = Run([9, 2, 5], developers: 4).Days.Single();
        Assert.Equal(new[] { 0d, 1d, 0d }, day.Items.Select(w => w.CollaborationDevelopmentCapacity));
        Assert.Equal(new[] { 1d, 1.5, 1d }, day.Items.Select(w => w.DevelopmentWork));
    }

    [Fact]
    public void TiesUseFifoAdmissionThenOriginalInputOrder()
    {
        var items = new[] { new WorkItem("older", "Older", 4, 1, 1), new WorkItem("newer", "Newer", 3, 1, 1, createdDay: 1) };
        var scenario = new SimulationScenario("FIFO", 2, new(1, 1), 2, 3, 3, items);
        var session = new SimulationSession(scenario); session.AdvanceOneDay();
        session.ApplyChanges(session.Configuration with { Team = new(3, 1) });
        var day = session.AdvanceOneDay(); // both have two remaining after primary; older wins.
        Assert.Equal(1, day.Items[0].CollaborationDevelopmentCapacity); Assert.Equal(0, day.Items[1].CollaborationDevelopmentCapacity);
        var tied = Run([10, 10, 10], developers: 5);
        Assert.Equal(new[] { 1d, 1d, 0d }, tied.Days[0].Items.Select(w => w.CollaborationDevelopmentCapacity));
        Assert.Equal(JsonSerializer.Serialize(tied), JsonSerializer.Serialize(Run([10, 10, 10], developers: 5)));
    }

    [Theory]
    [InlineData(.2, .2, 0, .2)]
    [InlineData(1.2, 1, .4, 1.2)]
    [InlineData(10, 1, 1, 1.5)]
    public void FractionalCapacityIsNotOverconsumed(double effort, double primary, double collaboration, double work)
    {
        var result = Run([effort]); var item = result.Days[0].Items.Single();
        Assert.Equal(primary, item.PrimaryDevelopmentCapacity, 12);
        Assert.Equal(collaboration, item.CollaborationDevelopmentCapacity, 12);
        Assert.Equal(primary + collaboration, item.UsedDevelopmentCapacity, 12);
        Assert.Equal(work, item.DevelopmentWork, 12);
        Assert.Equal(effort - work, item.RemainingDevelopmentEffort, 12);
        var events = result.WorkItems[0].Events.Where(e => e.EventType == WorkItemEventType.CapacityApplied).ToArray();
        Assert.Equal(item.UsedDevelopmentCapacity, events.Sum(e => e.CapacityConsumed), 12);
        Assert.Equal(item.DevelopmentWork, events.Sum(e => e.EffortApplied), 12);
    }

    [Fact]
    public void FractionsAreReusedButCompletionsDoNotAdmitNewItemsMidday()
    {
        var day = Run([.2, 1.2, 10, 10], wip: 3).Days[0];
        Assert.Equal(3.6, day.UsedDevelopmentCapacity, 12); // .2 + 1 + 1 primary; .4 + 1 collaboration.
        Assert.Equal(2.9, day.DevelopmentWork, 12);
        Assert.Equal(.4, day.Items[1].CollaborationDevelopmentCapacity, 12);
        Assert.Equal(1, day.Items[2].CollaborationDevelopmentCapacity);
        Assert.Equal(WorkItemStatus.Backlog, day.Items[3].State);
        Assert.Equal(0, day.Items[3].UsedDevelopmentCapacity);
    }

    [Fact]
    public void ExhaustedFractionalPoolCanPartlyCollaborate()
    {
        var item = Run([10], developers: 2, capacity: .8).Days[0].Items[0];
        Assert.Equal(.8, item.PrimaryDevelopmentCapacity, 12);
        Assert.Equal(.8, item.CollaborationDevelopmentCapacity, 12);
        Assert.Equal(1.6, item.UsedDevelopmentCapacity, 12);
        Assert.Equal(1.2, item.DevelopmentWork, 12);
        var partial = Run([10, 10], developers: 3, capacity: .5).Days[0].Items;
        Assert.Equal(.5, partial[0].CollaborationDevelopmentCapacity); Assert.Equal(0, partial[1].CollaborationDevelopmentCapacity);
    }

    [Fact]
    public void OneDeveloperCannotSupplyASecondContributorEvenWithCapacityAboveOne()
    {
        var day = Run([10], developers: 1, capacity: 2).Days[0];
        Assert.Equal(1, day.DevelopmentWork); Assert.Equal(1, day.UsedDevelopmentCapacity);
        Assert.Equal(0, day.CollaborationDevelopmentCapacity);
    }

    [Fact]
    public void ReviewAndReworkHavePriorityAndKeepTheirPerItemCaps()
    {
        var scenario = new SimulationScenario("Priority", 10, new(5, 1), 1, 1, 1,
            [new("A", "A", 1, 2, 1), new("B", "B", 10, 2, 1)])
        { Quality = new() { Enabled = true, CodeReviewDefectProbability = 1, CodeReviewReworkEffortDistribution = new FixedEffort(2) } };
        var result = new SimulationEngine().Run(scenario);
        Assert.Equal(1, result.Days[1].ReviewWork); Assert.Equal(2, result.Days[1].UsedDevelopmentCapacity);
        Assert.Equal(1, result.Days[3].UsedReworkDeveloperCapacity);
        Assert.All(result.Days, d => Assert.All(d.Items, w =>
        {
            Assert.InRange(w.CodeReviewWork, 0, 1); Assert.InRange(w.ReworkWork, 0, 1);
            Assert.InRange(new[] { w.DevelopmentWork, w.CodeReviewWork, w.ReworkWork, w.TestingWork }.Count(v => v > 0), 0, 1);
        }));
        var session = new SimulationSession(scenario); session.AdvanceOneDay();
        session.ApplyChanges(session.Configuration with { Team = new(1, 1) });
        var review = session.AdvanceOneDay(); Assert.Equal(1, review.ReviewWork); Assert.Equal(0, review.UsedDevelopmentCapacity);
        session.AdvanceOneDay(); var rework = session.AdvanceOneDay();
        Assert.Equal(1, rework.UsedReworkDeveloperCapacity); Assert.Equal(0, rework.UsedDevelopmentCapacity);
    }
}
