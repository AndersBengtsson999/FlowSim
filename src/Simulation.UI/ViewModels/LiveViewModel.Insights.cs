using Simulation.Core;
using Simulation.UI.Insights;
namespace Simulation.UI.ViewModels;

public sealed partial class LiveViewModel
{
    private SimulationSession? insightsSession;
    public InsightTracker Insights { get; } = new();
    public InsightReport? InsightsReport { get; private set; }
    public string InsightsIndicator => Insights.Indicator;
    private void RefreshInsights()
    {
        if(!ReferenceEquals(insightsSession,Live?.Session)) { Insights.Reset();insightsSession=Live?.Session; }
        InsightsReport=Live is null?null:SimulationInsights.Analyze(Live);
        if(InsightsReport is not null)Insights.Observe(InsightsReport);
        PropertyChanged?.Invoke(this,new(nameof(InsightsReport)));
        PropertyChanged?.Invoke(this,new(nameof(InsightsIndicator)));
    }
    public void ReviewInsight(string id)
    {
        Insights.Review(id);
        PropertyChanged?.Invoke(this,new(nameof(InsightsIndicator)));
    }
}
