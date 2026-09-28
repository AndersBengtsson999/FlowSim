using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;
using Simulation.Application;

namespace Simulation.UI.ViewModels;

/// <summary>Presentation only; enum values and persisted identifiers stay unchanged.</summary>
public sealed class PresentationLabels : IValueConverter
{
    public static string Label(object? value) => value?.ToString() switch
    {
        "ThroughputPerFiveDays" => "Throughput / 5 days",
        "DeveloperCount" => "Developers", "TesterCount" => "Testers",
        "AverageWip" => "Average WIP", "TotalWip" => "Total WIP",
        null => "", var text => Regex.Replace(text, "([a-z])([A-Z])", "$1 $2").Replace("Wip", "WIP")
    };
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Label(value);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    public static string Report(string text)
    {
        foreach (var metric in Enum.GetValues<AnalysisMetric>().OrderByDescending(m => m.ToString().Length)) text = text.Replace(metric.ToString(), Label(metric));
        return text;
    }
    public static readonly AnalysisMetric[] Primary = [AnalysisMetric.ThroughputPerFiveDays, AnalysisMetric.AverageLeadTime,
        AnalysisMetric.AverageCycleTime, AnalysisMetric.AverageWip, AnalysisMetric.DeveloperUtilization, AnalysisMetric.TesterUtilization];
}
