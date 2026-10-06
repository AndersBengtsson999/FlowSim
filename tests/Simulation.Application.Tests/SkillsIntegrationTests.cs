using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelValidation;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
namespace Simulation.Application.Tests;

public sealed class SkillsIntegrationTests
{
    static LiveSimulation Start(double rate=.4) => LiveSimulation.Start(Scenarios.BaselineRequest with {Skills=new(1,rate)},WorkArrivalMode.AlwaysAvailable);
    static void Until(LiveSimulation live,int day) => Scenarios.Until(live,day);
    [Fact]
    public void DefaultSkillsMatchAllArchivedPreFeatureSimulationStatesExactly()
    {
        var oldCulture=CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
            var runs=Scenarios.Run();
            var expected=new[]{"A0E697E4F754642560F2EB0399AD500B07C0FCB9F8C04805078F69E29EABC915","7D81EB43736CC14DF1E31912FDAF52AC2A03F3B2C94E43A8EF762FA0CADC33E1","FC7372AE19023A459B7B890C8748D21CAA0F2FAE362932B3E34AC8089B52E2D6"};
            var actual=new[]{runs.Baseline,runs.Productivity,runs.DebtTimeline}.Select(l=>{
                Assert.All(l.Session.WorkItems,w=>Assert.False(w.RequiresSpecialist));
                var node=JsonNode.Parse(JsonSerializer.Serialize(l.Session.Capture()))!;
                SkillsCompatibility.RemoveNewFields(node);
                return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(node.ToJsonString())));
            }).ToArray();
            Assert.Equal(expected,actual);
        } finally {CultureInfo.CurrentCulture=oldCulture;}
    }
    [Fact]
    public void RatesAffectOnlyFutureArrivalsAndCheckpointSaveLoadRepeatExactly()
    {
        var live=Start(.2);Until(live,100);var old=live.Session.WorkItems.ToDictionary(w=>w.Id,w=>w.RequiresSpecialist);
        live.Session.ApplyChanges(live.Session.Configuration with{Skills=new(2,.6)},"Skills assumption");
        var checkpoint=live.CreateCheckpoint("Skills");var loaded=LiveSessionJson.Load(LiveSessionJson.Save(live));
        Until(live,200);Until(loaded,200);
        Assert.All(live.Session.WorkItems.Where(w=>old.ContainsKey(w.Id)),w=>Assert.Equal(old[w.Id],w.RequiresSpecialist));
        var later=live.Session.WorkItems.Where(w=>!old.ContainsKey(w.Id)).ToArray();Assert.Contains(later,w=>w.RequiresSpecialist);Assert.Contains(later,w=>!w.RequiresSpecialist);
        var expected=JsonSerializer.Serialize(live.Session.Capture());Assert.Equal(expected,JsonSerializer.Serialize(loaded.Session.Capture()));
        var repeat=Start(.2);Until(repeat,100);repeat.Session.ApplyChanges(repeat.Session.Configuration with{Skills=new(2,.6)},"Skills assumption");Until(repeat,200);
        Assert.Equal(expected,JsonSerializer.Serialize(repeat.Session.Capture()));
        var series=LivePerformanceTrend.Project(live.Session,LiveTrendMetric.SpecialistWorkWaiting,50,null);
        Assert.All(series.Points,p=>Assert.Equal(live.Session.Days[p.Day-1].SpecialistWorkWaiting,p.Value));
        live.RestoreCheckpoint(checkpoint.Id);Until(live,200);Assert.Equal(expected,JsonSerializer.Serialize(live.Session.Capture()));
    }
    [Fact]
    public void LegacyLoadDefaultsGeneralAndNewClassificationsUseEffectiveRate()
    {
        var live=Start(0);Until(live,25);
        var node=JsonNode.Parse(LiveSessionJson.Save(live))!;SkillsCompatibility.RemoveNewFields(node);node["SimulationModelVersion"]="0.5";
        var loaded=LiveSessionJson.Load(node.ToJsonString());
        Assert.Equal(new SkillSettings(),loaded.Session.Configuration.Skills);
        Assert.All(loaded.Session.WorkItems,w=>Assert.False(w.RequiresSpecialist));
        var ids=loaded.Session.WorkItems.Select(w=>w.Id).ToHashSet();
        loaded.Session.ApplyChanges(loaded.Session.Configuration with{Skills=new(2,1)});Until(loaded,50);
        Assert.All(loaded.Session.WorkItems.Where(w=>!ids.Contains(w.Id)),w=>Assert.True(w.RequiresSpecialist));
        Assert.All(loaded.Session.WorkItems.Where(w=>ids.Contains(w.Id)),w=>Assert.False(w.RequiresSpecialist));
        Assert.All(loaded.Session.Days.Take(25),d=>Assert.Equal(0,d.SpecialistWorkWaiting));
    }
    [Fact]
    public void MixedSkillsConserveCapacityCostsAndPreserveNonDevelopmentMechanics()
    {
        var live=LiveSimulation.Start(Scenarios.BaselineRequest with {Skills=new(2,.4),DeveloperAvailability=.8,Productivity=new(1.5,1.2,1.3),
            Debt=new(){ShortcutRate=.3,Repayment=.25},Quality=new(){Enabled=true,TestingDefectProbability=.3}},WorkArrivalMode.AlwaysAvailable);
        Until(live,200);var ds=live.Session.Days;
        Assert.Contains(ds,d=>d.UsedReworkDeveloperCapacity>0);Assert.Contains(ds,d=>d.UsedDebtRepaymentCapacity>0);
        foreach(var d in ds) {
            Assert.InRange(d.UsedDeveloperCapacity,0,d.AvailableDeveloperCapacity+1e-9);
            var remaining=d.AvailableDeveloperCapacity-d.UsedReviewCapacity-d.UsedReworkDeveloperCapacity-d.UsedDebtRepaymentCapacity;
            var specialistUse=d.Items.Where(w=>w.RequiresSpecialist).Sum(w=>w.UsedDevelopmentCapacity);
            Assert.InRange(specialistUse,0,Math.Max(0,remaining)*2/5+1e-9);
            Assert.Equal(d.Items.Count(w=>w.RequiresSpecialist && w.State==WorkItemStatus.Development && w.RemainingDevelopmentEffort>0 && w.UsedDevelopmentCapacity==0),d.SpecialistWorkWaiting);
        }
        var total=ds.Sum(d=>d.UsedDeveloperCapacity+d.UsedTesterCapacity);
        var attributed=live.Session.WorkItems.Sum(w=>w.DeliveryCost.Total);
        Assert.InRange(Math.Abs(total-attributed-ds.Sum(d=>d.UsedDebtRepaymentCapacity)),0,1e-8);
        var p=LivePerformance.Rolling(live.Session,50);
        Assert.InRange(Math.Abs(p.SystemCostPerDoneItem!.Value*p.Completed-ds.TakeLast(50).Sum(d=>d.UsedDeveloperCapacity+d.UsedTesterCapacity)),0,1e-8);
        var before=JsonSerializer.Serialize(live.Session.Capture());
        foreach(var metric in LivePerformanceTrend.Metrics)LivePerformanceTrend.Project(live.Session,metric.Metric,50,null);
        Assert.Equal(before,JsonSerializer.Serialize(live.Session.Capture()));
    }
}
