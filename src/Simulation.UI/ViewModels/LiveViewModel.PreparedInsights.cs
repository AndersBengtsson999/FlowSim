using Simulation.Application;
using Simulation.Core;
namespace Simulation.UI.ViewModels;

public sealed partial class LiveViewModel
{
    private bool isolatedInsightComparison;
    private string GuidedAlternativeName=>isolatedInsightComparison?"Prepared alternative":"LIVE intervention";
    /// <summary>Adapter to the existing guided comparison, on a dedicated view model only.
    /// Both branches restore the same checkpoint; ordinary LIVE never calls this method.</summary>
    public async Task RunPreparedInsightComparisonAsync(SimulationSessionState baseline,SessionConfiguration alternative,int window,CancellationToken token)
    {
        if(Live is not null)throw new InvalidOperationException("Prepared comparisons require an unused, isolated view model.");
        isolatedInsightComparison=true;
        Load(new LiveSimulation(SimulationSession.Restore(baseline)));
        Live!.Session.ApplyChanges(alternative,"Prepared Insights comparison");
        var experiment=new GuidedInterventionExperiment(new(Guid.NewGuid(),"Insights baseline",baseline),Live.Session,window);
        guidedExperiment=experiment;
        await experiment.PrepareBaselineAsync(token);
        await Task.Run(()=>{
            for(var n=0;n<window;n++) {token.ThrowIfCancellationRequested();Live.Session.AdvanceOneDay(LaunchDecisionPolicy.Automatic);}
        },token);
        token.ThrowIfCancellationRequested();
        RebuildHistory();Refresh();
    }
}
