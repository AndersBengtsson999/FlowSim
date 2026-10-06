using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public sealed class SkillsPresentationTests
{
    [Fact]
    public void ConfigurationAndAtomicChangeValidateSubsetAndRetainLabel()
    {
        using var vm=new LiveViewModel();vm.Setup.Specialists="3";vm.Setup.SpecialistWorkRate="40";vm.Start();vm.Pause();vm.Step();
        Assert.Equal(new SkillSettings(3,.4),vm.Live!.Session.Configuration.Skills);
        vm.BeginChange();vm.Draft.NumberOfDevelopers="2";
        Assert.Throws<ScenarioValidationException>(()=>vm.ApplyChanges());Assert.Equal(5,vm.Live.Session.Configuration.Team.DeveloperCount);
        vm.Draft.Specialists="1";vm.Draft.SpecialistWorkRate="60";vm.ChangeLabel="Skill composition";vm.ApplyChanges();
        Assert.Equal(new SkillSettings(1,.6),vm.Live.Session.Configuration.Skills);
        var c=vm.Live.Session.Changes.Single();Assert.Equal("Skill composition",c.Label);
        Assert.Contains("Specialists",LiveViewModel.DescribeParameters(c));Assert.Contains("Specialist Work Rate",LiveViewModel.DescribeParameters(c));
    }
    [Fact]
    public void WaitingIsVisibleInExistingFlowAndTrendWithoutNewHeadline()
    {
        using var vm=new LiveViewModel();vm.WorkSupply="Always available";vm.Setup.Specialists="0";vm.Setup.SpecialistWorkRate="100";vm.Start();vm.Pause();vm.Step();
        var row=vm.Flow.Single(r=>r.State==WorkItemStatus.Development);Assert.True(row.ShowSkills);Assert.Equal(5,row.SpecialistWorkWaiting);
        Assert.All(row.Items,w=>Assert.Equal("Specialist Development",w.SkillMarker));
        vm.TrendMetric=vm.TrendMetrics.Single(m=>m.Metric==LiveTrendMetric.SpecialistWorkWaiting);Assert.Equal("items",vm.TrendMetric.Unit);
        Assert.DoesNotContain(vm.StatusPrimaryGroups,r=>r.Label.Contains("Specialist"));
    }
}
