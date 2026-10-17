using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public sealed class TeamEconomicsViewModelTests
{
    [Fact]
    public void SettingsRoundTripAndLiveInterventionPreserveEarlierCosts()
    {
        var setup = new MainWindowViewModel();
        var rates = new RoleCostSettings(91000.25m,81000m,120000m,22);
        setup.LoadConfiguration(new(){RoleCosts=rates}); Assert.Equal(rates,setup.CaptureSetup().RoleCosts);
        using var vm = new LiveViewModel(); vm.Start(); vm.Tick(); vm.Pause();
        var previous=vm.Live!.Session.Days[0].Economics;
        vm.BeginChange();vm.ChangeFields.Single(f=>f.Label=="Tester monthly cost · SEK").Value="80000";
        vm.ApplyChanges(); Assert.Contains("Tester monthly cost",vm.Interventions.Last().Label ?? LiveViewModel.Describe(vm.Interventions.Last()));
        vm.Step();Assert.Equal(previous,vm.Live.Session.Days[0].Economics);Assert.Equal(80000m,vm.Live.Session.Days[1].Economics!.Rates.TesterMonthly);
        Assert.Equal(4,vm.EconomicRows.Count);Assert.Equal(2,vm.EconomicTrend.Points.Count);Assert.Contains("SEK",vm.EconomicHeader);
        Assert.Equal("N/A",vm.EconomicRows.Single(r=>r.Label=="Cost / delivered item").Value);
    }
    [Fact]
    public async Task GuidedSummaryShowsEconomicMetricsWithoutReplacingCapacityCost()
    {
        using var vm = new LiveViewModel(); vm.Start();for(int i=0;i<100;i++)vm.Tick();
        vm.BeginGuidedExperiment();vm.Draft.NumberOfTesters="3";vm.ApplyChanges();await vm.BaselineTask;vm.Step();
        Assert.Contains(vm.GuidedRows,r=>r.Metric=="Cost · capacity");Assert.Contains(vm.GuidedRows,r=>r.Metric=="Team cost · SEK");
        Assert.Contains("Incremental cost",vm.GuidedIncrementalCost);Assert.Equal(2,vm.GuidedTeamCost.Count);
        vm.RollingWindow=10;Assert.Contains("Rolling 10",vm.EconomicWindow);
        vm.Reset();Assert.Empty(vm.EconomicRows);Assert.Empty(vm.EconomicTrend.Points);
    }
    [Theory]
    [InlineData("-1")] [InlineData("NaN")] [InlineData("Infinity")] [InlineData("1000000001")]
    public void InvalidCostsAreRejected(string text)
    { var vm=new MainWindowViewModel{DeveloperMonthlyCost=text};Assert.ThrowsAny<Exception>(()=>vm.CaptureSetup()); }
}
