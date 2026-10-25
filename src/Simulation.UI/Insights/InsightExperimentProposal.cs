using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
namespace Simulation.UI.Insights;

/// <summary>Frozen provenance and baseline. Preparation and editing perform no simulation steps.</summary>
public sealed class InsightExperimentProposal : IDisposable
{
    private readonly SimulationSessionState baseline;
    private readonly CancellationTokenSource cancellation=new();
    public SimulationInsight Source { get; }
    public int BaselineDay=>baseline.CurrentDay;
    public int Seed=>baseline.RandomSeed;
    public SessionConfiguration BaselineConfiguration=>baseline.Configuration;
    public int ProposedTesters { get; set; }
    public int Window { get; set; }=20;
    public bool Cancelled { get; private set; }
    public bool Confirmed { get; private set; }
    public LiveViewModel? Result { get; private set; }
    public string Question=>"Does additional tester capacity change waiting work and released throughput over the matched period? Improvement is not guaranteed.";
    private InsightExperimentProposal(LiveSimulation live,SimulationInsight source)
    {
        // Restore/capture makes an independent snapshot without changing LIVE checkpoints or randomness.
        baseline=SimulationSession.Restore(live.Session.Capture()).Capture();
        Source=source;ProposedTesters=checked(baseline.Configuration.Team.TesterCount+1);
    }
    public static bool Supports(SimulationInsight source,LiveSimulation? live)=>live is not null && source.Relevant &&
        source.Id is "queue.testing-wait" or "imbalance.testing-wait" && live.Session.Configuration.Team.TesterCount<int.MaxValue && live.Session.CurrentDay<=int.MaxValue-40;
    public static InsightExperimentProposal? Prepare(SimulationInsight source,LiveSimulation? live)=>Supports(source,live)?new(live!,source):null;
    public SessionConfiguration ProposedConfiguration
    {
        get
        {
            if(ProposedTesters<=BaselineConfiguration.Team.TesterCount)throw new ArgumentException("Enter a tester count greater than the baseline count.");
            var configuration=BaselineConfiguration with { Team=BaselineConfiguration.Team with {TesterCount=ProposedTesters} };
            configuration.Validate();return configuration;
        }
    }
    public async Task ConfirmAndRunAsync()
    {
        if(Cancelled || Confirmed)throw new InvalidOperationException("This proposal is cancelled or has already run. Prepare a new comparison.");
        if(Window is not (10 or 20 or 40))throw new ArgumentException("Choose 10, 20 or 40 days.");
        var alternative=ProposedConfiguration;
        Confirmed=true;
        Result=new LiveViewModel();
        await Result.RunPreparedInsightComparisonAsync(baseline,alternative,Window,cancellation.Token);
    }
    public void Cancel() {Cancelled=true;cancellation.Cancel();}
    public void Dispose(){Cancel();Result?.Dispose();cancellation.Dispose();}
}
