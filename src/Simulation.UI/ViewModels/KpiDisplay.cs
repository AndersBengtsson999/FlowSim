using System.Globalization;
using Simulation.Application;
namespace Simulation.UI.ViewModels;

/// <summary>Display-only conversions. Observations and persisted ratios keep their original units.</summary>
public static class KpiDisplay
{
    public static double? Scale(TeamKpi id, double? value) => id == TeamKpi.WorkVolumePerSek ? value * 1000 : value;
    public static string Unit(TeamKpiDefinition definition) => definition.Id == TeamKpi.WorkVolumePerSek ? "effort units / 1,000 SEK" : definition.Unit;
    public static string NumberFormat(TeamKpi id) => id == TeamKpi.WorkVolumePerSek ? "N3" : id == TeamKpi.UnlaunchedDevelopmentCost ? "N0" :
        id is TeamKpi.CostPerDelivery or TeamKpi.TotalTeamCost or TeamKpi.ReworkCost or TeamKpi.CostPerWorkSize ? "N2" : "0.##";
    public static string Format(TeamKpi id, double? value) => Scale(id, value)?.ToString(NumberFormat(id), CultureInfo.CurrentCulture) ?? "N/A";
    public static LiveTrendSeries Trend(TeamKpi id, LiveTrendSeries series) => id != TeamKpi.WorkVolumePerSek ? series :
        new(series.Points.Select(p => p with { Value = Scale(id, p.Value) }).ToArray(), series.Interventions);
    public static string Percentage(double? value, double? baseline)
    {
        if (value is null || baseline is null) return "N/A · previous period unavailable";
        if (baseline == 0) return "N/A · previous period is zero";
        var percent = (value.Value - baseline.Value) / Math.Abs(baseline.Value) * 100;
        return $"{(percent > 0 ? "+" : "")}{percent.ToString("N1", CultureInfo.CurrentCulture)}% versus previous period";
    }
}
