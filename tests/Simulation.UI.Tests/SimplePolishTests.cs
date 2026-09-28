using System.Globalization;
using System.Text.Json;
using Simulation.Application;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class SimplePolishTests
{
    [Theory]
    [InlineData(1.500, "1.5")] [InlineData(9.667, "9.7")] [InlineData(2.60, "2.6")]
    [InlineData(8.967, "9.0")] [InlineData(-0.001, "0.0")]
    public void DecimalFormatIsOnePlaceAndIndependentOfSwedishCulture(double value, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = new CultureInfo("sv-SE"); Assert.Equal(expected, SimpleResultPresentation.Number(value)); }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [Fact]
    public void DifferencesRoundRawDeltaNotRoundedEndpoints()
    {
        Assert.Equal("-0.7", SimpleResultPresentation.Change(8.967 - 9.667));
        Assert.Equal("+0.1", SimpleResultPresentation.Change(1.149 - 1.051));
        Assert.Equal("0.0", SimpleResultPresentation.Change(-.001));
        Assert.Equal("94%", SimpleResultPresentation.Percent(.94));
    }
    [Theory]
    [InlineData("WaitingForCodeReview", "Code Review")]
    [InlineData("Waiting for Testing", "Testing")]
    [InlineData("WaitingForRework", "Rework")]
    public void SummaryQueueNamesAreCompact(string input, string expected) => Assert.Equal(expected, SimpleResultPresentation.QueueName(input));

    private static FlowObservationValues V(string queue = "Code Review", int count = 5, double throughput = 1.5, double cycle = 9.667, double wip = 2.6) => new(new(queue, count), throughput, cycle, wip);
    [Fact]
    public void ObservationPriorityAndThresholdsAreExplicit()
    {
        Assert.Contains("moved from Testing to Code Review", SimpleFlowObservation.Describe(V("Testing", 20), V(count: 1, throughput: 10)));
        Assert.Contains("changed from 5 to 7 items", SimpleFlowObservation.Describe(V(), V(count: 7, throughput: 10)));
        Assert.StartsWith("Throughput changed", SimpleFlowObservation.Describe(V(), V(count: 6, throughput: 1.6)));
        Assert.Contains("remained Code Review at 5 items", SimpleFlowObservation.Describe(V(), V(cycle: 8.967)));
        Assert.Contains("average Cycle Time changed", SimpleFlowObservation.Describe(V(), V(count: 6, cycle: 9.567)));
        Assert.Contains("Work in Progress changed", SimpleFlowObservation.Describe(V(), V(count: 6, wip: 2.7)));
        Assert.Equal("No notable change in the primary results.", SimpleFlowObservation.Describe(V(), V(count: 6, cycle: 9.60)));
        Assert.Equal("The largest observed queue changed from 0 to 12 items.", SimpleFlowObservation.Describe(V(count: 0), V("Testing", 12)));
        Assert.Equal("No waiting queue was observed in either run.", SimpleFlowObservation.Describe(V(count: 0), V(count: 0)));
    }
    [Fact]
    public void EveryObservationBranchUsesNeutralLanguageAndIsDeterministic()
    {
        foreach (var after in new[] { V("Testing", 12), V(count: 9), V(throughput: 2), V(), V(count: 6,cycle: 8), V(count: 6,wip: 3), V(count: 6) })
        {
            var text = SimpleFlowObservation.Describe(V(), after);
            Assert.Equal(text, SimpleFlowObservation.Describe(V(), after));
            foreach (var word in new[] { "better", "worse", "good", "bad", "optimal", "problem", "bottleneck", "should", "recommend" })
                Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase);
        }
    }
    [Fact]
    public async Task ActualComparisonFormatsCardsAndObservationWithoutChangingResults()
    {
        var main = new MainWindowViewModel(); await main.Simple.RunAsync();
        Assert.Equal("1.5 items / 5 days", main.Simple.RunCards[0].Value);
        Assert.Equal("9.7 days", main.Simple.RunCards[1].Value);
        Assert.Equal("2.6 items", main.Simple.RunCards[2].Value);
        main.Simple.OpenChangesCommand.Execute(null); main.Simple.Try.NumberOfTesters = "3"; await main.Simple.RunComparisonAsync();
        var c = main.Simple.Pair.Comparison!; var before = JsonSerializer.Serialize(c);
        var cycle = main.Simple.CompareCards.Single(m => m.Name == "Cycle Time");
        Assert.Equal("9.7 days", cycle.Before); Assert.Equal("9.0 days", cycle.After); Assert.Equal("-0.7 days", cycle.Difference);
        var queue = main.Simple.CompareCards.Single(m => m.Name == "Largest Queue");
        Assert.Equal("Code Review\n5 items", queue.Before); Assert.Equal(queue.Before, queue.After);
        Assert.False(queue.HasDifference); Assert.Equal("", queue.Difference);
        Assert.Equal("The largest observed queue remained Code Review at 5 items.", main.Simple.FlowObservation);
        Assert.Equal(before, JsonSerializer.Serialize(c));
        Assert.Equal(JsonSerializer.Serialize(new SimulationRunner().Run(BaselineScenario.CreateRequest())), JsonSerializer.Serialize(main.Result));
    }
    [Fact]
    public async Task DifferentLargestQueuesUseCorrectBeforeAndAfterNames()
    {
        var s = new MainWindowViewModel().Simple; s.Try.NumberOfTesters = "0"; await s.RunComparisonAsync();
        var c = s.Pair.Comparison!;
        var before = c.Runs.Single(r => r.ScenarioSnapshot.Id == c.ExperimentSnapshot.BaselineId).SingleRun!;
        var after = c.Runs.Single(r => r.ScenarioSnapshot.Id != c.ExperimentSnapshot.BaselineId).SingleRun!;
        Assert.NotEqual(QueueObservation.From(before).Name, QueueObservation.From(after).Name);
        var row = s.CompareCards.Single(r => r.Name == "Largest Queue");
        Assert.StartsWith("Code Review\n", row.Before); Assert.StartsWith("Testing\n", row.After);
        Assert.Equal("The largest observed queue moved from Code Review to Testing.", s.FlowObservation);
    }
}
