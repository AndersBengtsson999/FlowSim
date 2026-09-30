using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;

public sealed class SimpleWorkflowTests
{
    [Fact]
    public async Task SimpleRunUsesInputsAndFourActualResultCards()
    {
        var main = new MainWindowViewModel(); var simple = main.Simple;
        Assert.True(simple.LiveVisible); main.NumberOfDevelopers = "4"; main.NumberOfTesters = "3";
        await simple.RunAsync(); Assert.True(simple.ResultsVisible);
        Assert.Equal(4, main.LastRunRequest!.DeveloperCount); Assert.Equal(3, main.LastRunRequest.TesterCount);
        Assert.Equal(4, simple.RunCards.Count);
        Assert.Equal(SimpleResultPresentation.Number(main.Result!.AverageCycleTime) + " days", simple.RunCards[1].Value);
        Assert.Equal("Cycle Time", simple.RunCards[1].Label);
        Assert.Equal(main.Result!.Days.SelectMany(d => new[] { d.WaitingForCodeReviewCount,d.WaitingForTestingCount,d.WaitingForReworkCount }).Max(), QueueObservation.From(main.Result).Count);
        simple.FlowCommand.Execute(null); Assert.True(simple.FlowVisible);
        simple.ResultsCommand.Execute(null); Assert.True(simple.ResultsVisible);
    }
    [Fact]
    public async Task TestersChangeNeedsNoScenarioManagementAndPreservesReference()
    {
        var main = new MainWindowViewModel(); var simple = main.Simple;
        simple.OpenChangesCommand.Execute(null); var original = simple.StartingConfiguration;
        simple.TeamChanges.Single(f => f.Label == "Testers").Value = "3";
        await simple.RunComparisonAsync();
        Assert.True(simple.ComparisonVisible); Assert.Equal(2, simple.Pair.Scenarios.Count);
        Assert.Equal(2, original.TesterCount); Assert.Equal(3, simple.AlternativeConfiguration().TesterCount);
        Assert.Single(simple.Pair.ChangedParameters); Assert.Equal("Testers", simple.Pair.ChangedParameters[0].Name);
        Assert.Equal("2 → 3", simple.Pair.ChangedParameters[0].Change); Assert.Equal(4, simple.CompareCards.Count);
        Assert.Contains(simple.MoreComparisonResults, r => r.Label == "Average Lead Time");
        Assert.Contains(simple.MoreComparisonResults, r => r.Label == "Maximum Waiting For Testing Queue");
        Assert.Single(main.Compare.Scenarios); // expert experiment is preserved
    }
    [Fact]
    public async Task LastRunUsesCapturedConfigurationAndPreservesHiddenSettings()
    {
        var main = new MainWindowViewModel(); var request = BaselineScenario.DefectsAndReworkExample() with {
            SimulationDays = 75, CodeReviewWipLimit = 2, RandomSeed = 78, TesterCapacityPerDay = .8
        };
        main.LoadConfiguration(request); await main.Simple.RunAsync();
        main.NumberOfDevelopers = "99"; main.Simple.OpenChangesCommand.Execute(null);
        var simple = main.Simple; var alternative = simple.AlternativeConfiguration();
        Assert.Equal("Last simulation", simple.StartingPoint); Assert.Equal(5, alternative.DeveloperCount);
        Assert.Equal(75, alternative.SimulationDays); Assert.Equal(2, alternative.CodeReviewWipLimit);
        Assert.Equal(78, alternative.RandomSeed); Assert.Equal(.8, alternative.TesterCapacityPerDay);
        Assert.Equal(request.Quality, alternative.Quality); Assert.Equal(request.CodeReviewDistribution, alternative.CodeReviewDistribution);
        Assert.Empty(ChangedParameter.Between(simple.StartingConfiguration, alternative));
    }
    [Fact]
    public async Task MultipleChangesAndNoChangesArePresentedFaithfully()
    {
        var s = new MainWindowViewModel().Simple;
        s.Try.NumberOfDevelopers = "7"; s.Try.TestingWipLimit = "5"; s.Try.DeveloperCapacity = "1.4";
        await s.RunComparisonAsync(); Assert.Equal(3, s.Pair.ChangedParameters.Count);
        Assert.Contains(s.Pair.ChangedParameters,c => c.Name == "Developer Capacity / Day");
        s.PrepareChanges(); await s.RunComparisonAsync(); Assert.Empty(s.Pair.ChangedParameters);
        Assert.Equal("You changed nothing", s.ChangesTitle);
    }
    [Fact]
    public async Task ExploreUsesExistingAnalysisRunnerAndSelectedMetric()
    {
        var s = new MainWindowViewModel().Simple; s.Explore.Values = "1, 2, 3";
        await s.ExploreAsync(); var result = Assert.IsType<SensitivityAnalysisResult>(s.Explore.Result);
        var expected = new SensitivityAnalysisRunner().Run(s.Explore.ReadRequest());
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(result));
        Assert.Equal(3, s.Explore.Points.Count); Assert.Equal(AnalysisMetric.ThroughputPerFiveDays, s.Explore.Metric);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SimplePresentationDoesNotChangeNumericalResults(bool defects)
    {
        var request = defects ? BaselineScenario.DefectsAndReworkExample() : BaselineScenario.CreateRequest();
        var main = new MainWindowViewModel(); main.LoadConfiguration(request); await main.Simple.RunAsync();
        var expected = new SimulationRunner().Run(request);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(main.Result));
        var before = JsonSerializer.Serialize(main.Result); _ = main.Simple.RunCards; main.Simple.AdvancedCommand.Execute(null);
        Assert.Equal(before, JsonSerializer.Serialize(main.Result)); Assert.True(main.Simple.AdvancedVisible);
        main.Compare.DuplicateCommand.Execute(null); await main.Compare.RunAsync(true); Assert.NotNull(main.Compare.Comparison);
    }
    [Fact]
    public async Task InvalidChangesShowErrorWithoutPublishingAResult()
    {
        var simple = new MainWindowViewModel().Simple; simple.Try.NumberOfTesters = "-1";
        await simple.RunComparisonAsync(); Assert.False(simple.ComparisonVisible); Assert.Null(simple.Pair.Comparison);
        Assert.NotEmpty(simple.Status); Assert.False(simple.IsBusy);
    }
}
