using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Help;
using Simulation.UI.Insights;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;

public sealed class InsightEvidenceAndProposalTests
{
    private static LiveSimulation Live()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=80,DeveloperCount=6,TesterCount=1,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=5,DevelopmentWipLimit=20,CodeReviewWipLimit=20,TestingWipLimit=6},WorkArrivalMode.ContinuousArrival,3);
        for(var n=0;n<35;n++)live.Step();return live;
    }
    private static SimulationInsight Source(LiveSimulation live)=>SimulationInsights.Analyze(live).Insights.Single(i=>i.Id=="imbalance.testing-wait");
    private static string Json(object obj)=>JsonSerializer.Serialize(obj);
    [Theory][InlineData(InsightStatus.New,"New")][InlineData(InsightStatus.Updated,"Updated")][InlineData(InsightStatus.Reviewed,"Reviewed")][InlineData(InsightStatus.Informational,"Informational")]
    public void CompactRowsKeepStatusSeparateFromMeaningfulShortTitle(InsightStatus state,string label)
    {
        var item=new TrackedInsight(Source(Live()),state);
        Assert.Equal("Review → Testing",item.ShortTitle);Assert.Equal(label,item.StatusLabel);Assert.Contains("Days 16–35",item.Secondary);
        Assert.DoesNotContain(label,item.ShortTitle);
    }
    [Fact] public void QueueAndCapacityUseActualIndependentFourAndTwentyDayRanges()
    {
        var live=Live();var report=SimulationInsights.Analyze(live);
        var queue=report.Insights.Single(i=>i.Id=="queue.testing-wait");var imbalance=Source(live);
        Assert.Equal(Enumerable.Range(32,4),queue.Charts[0].Primary.Points.Select(p=>p.Day));
        Assert.Equal(Enumerable.Range(16,20),imbalance.Charts[0].Primary.Points.Select(p=>p.Day));
        Assert.Equal(live.Session.Days.TakeLast(20).Select(d=>(double?)d.WaitingForTestingCount),imbalance.Charts[0].Primary.Points.Select(p=>p.Value));
        Assert.Equal(live.Session.Days.TakeLast(20).Select(d=>(double?)d.UsedTesterCapacity),imbalance.Charts[1].Primary.Points.Select(p=>p.Value));
        Assert.Equal(live.Session.Days.TakeLast(20).Select(d=>(double?)d.AvailableTesterCapacity),imbalance.Charts[1].Secondary!.Points.Select(p=>p.Value));
        Assert.Equal("items",queue.Charts[0].Unit);Assert.Equal("raw units",queue.Charts[1].Unit);
    }
    [Fact] public void MissingDaysAreNotInventedOrFilledWithZero()
    {
        var live=Live();var source=Source(live);var days=live.Session.Days.Where(d=>d.Day is 16 or 19 or 30).ToArray();
        var charts=InsightEvidence.Build(source,days);
        Assert.Equal(new[]{17,20,31},charts[0].Primary.Points.Select(p=>p.Day));
        Assert.Equal(new InsightPeriod(17,31,3,20),charts[0].Period);
        Assert.Empty(InsightEvidence.Build(source,[]));
        var missing=charts[0] with { Primary=new([new(17,null)],[]) };
        Assert.Contains("N/A",Assert.Single(missing.Measurements));
    }
    [Fact] public void SharedDeveloperChartIsNotAnIndependentReviewerPool()
    {
        var live=Live();var chart=SimulationInsights.Analyze(live).Insights.Single(i=>i.Id=="queue.review-wait").Charts[1];
        Assert.Contains("Shared developer",chart.Title);
        Assert.Equal(live.Session.Days.TakeLast(4).Select(d=>(double?)d.UsedDeveloperCapacity),chart.Primary.Points.Select(p=>p.Value));
    }
    [Fact] public void PreparingEditingAndCancellingPreserveLiveSeedBaselineAndCheckpoints()
    {
        var live=Live();var before=Json(live.Capture());using var proposal=InsightExperimentProposal.Prepare(Source(live),live)!;
        Assert.False(proposal.Confirmed);Assert.Null(proposal.Result);Assert.Equal(live.Session.RandomSeed,proposal.Seed);
        Assert.Equal(35,proposal.BaselineDay);Assert.Equal(live.Session.Configuration,proposal.BaselineConfiguration);
        Assert.Equal(2,proposal.ProposedTesters);
        Assert.Equal(proposal.BaselineConfiguration,proposal.ProposedConfiguration with {Team=proposal.ProposedConfiguration.Team with {TesterCount=1}});
        proposal.ProposedTesters=3;Assert.Equal(3,proposal.ProposedConfiguration.Team.TesterCount);
        proposal.Cancel();Assert.True(proposal.Cancelled);Assert.Null(proposal.Result);Assert.Equal(before,Json(live.Capture()));
    }
    [Fact] public async Task CancelAndInvalidDraftCannotExecute()
    {
        var live=Live();using var cancelled=InsightExperimentProposal.Prepare(Source(live),live)!;cancelled.Cancel();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>cancelled.ConfirmAndRunAsync());
        using var invalid=InsightExperimentProposal.Prepare(Source(live),live)!;invalid.ProposedTesters=1;
        await Assert.ThrowsAsync<ArgumentException>(()=>invalid.ConfirmAndRunAsync());Assert.False(invalid.Confirmed);Assert.Null(invalid.Result);
    }
    [Fact] public async Task ConfirmReusesGuidedComparisonFromFrozenStateWithoutChangingLive()
    {
        var live=Live();var state=live.Session.Capture();using var proposal=InsightExperimentProposal.Prepare(Source(live),live)!;
        // LIVE may continue after preparation; the comparison still starts at captured Day35.
        live.Step();var before=Json(live.Capture());proposal.Window=10;await proposal.ConfirmAndRunAsync();
        Assert.True(proposal.Confirmed);var result=proposal.Result!;Assert.Equal(45,result.Day);Assert.Equal(before,Json(live.Capture()));
        Assert.Contains("Complete",result.GuidedProgress);Assert.NotEmpty(result.GuidedRows);
        Assert.Equal(new[]{"Unchanged baseline","Prepared alternative"},result.GuidedQueueSeries.Select(s=>s.Name));
        var expected=SimulationSession.Restore(state);expected.ApplyChanges(proposal.ProposedConfiguration,"Prepared Insights comparison");
        for(var n=0;n<10;n++)expected.AdvanceOneDay(LaunchDecisionPolicy.Automatic);
        Assert.Equal(Json(expected.Capture()),Json(result.Live!.Session.Capture()));
        Assert.Equal(Json(KpiDashboard.Project(expected,new())),Json(KpiDashboard.Project(result.Live.Session,new())));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>proposal.ConfirmAndRunAsync());
    }
    [Fact] public void UnsupportedAndInactiveSuggestionsHaveNoAction()
    {
        var live=Live();var report=SimulationInsights.Analyze(live);
        Assert.False(InsightExperimentProposal.Supports(report.Insights.Single(i=>i.Id=="queue.review-wait"),live));
        Assert.Null(InsightExperimentProposal.Prepare(Source(live) with {Relevant=false},live));
        Assert.Null(InsightExperimentProposal.Prepare(Source(live),null));
    }
    [Fact] public void NewEvidenceDoesNotChangeAcknowledgementIdentity()
    {
        var live=Live();var tracker=new InsightTracker();tracker.Observe(SimulationInsights.Analyze(live));tracker.Review("imbalance.testing-wait");
        var first=Source(live);var report=SimulationInsights.Analyze(live);
        tracker.Observe(report with {Insights=[first with {Charts=[]}]});
        Assert.Equal(InsightStatus.Reviewed,tracker.Items.Single(i=>i.Insight.Id==first.Id).Status);
    }
    [Fact] public void EvidenceAndPreparationDocumentationIsPackaged()
    {
        var guide=new GuideCatalog();guide.Validate(requireImages:true);
        Assert.Equal("experiments.insight-evidence",guide.Resolve("experiments.insight-evidence").Id);
        Assert.Contains(guide.Search("prepared comparison"),r=>r.Id=="experiments.insight-evidence");
    }
}
