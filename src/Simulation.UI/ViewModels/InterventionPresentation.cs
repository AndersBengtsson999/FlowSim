using System.Globalization;
using Avalonia.Data.Converters;
using Simulation.Core;

namespace Simulation.UI.ViewModels;

/// <summary>Presentation of existing intervention metadata; never changes the stored intervention.</summary>
public sealed class InterventionPresentation : IValueConverter
{
    private static string? Label(ConfigurationChange change) => string.IsNullOrWhiteSpace(change.Label) ? null : change.Label.Trim();
    public static string Latest(ConfigurationChange change) => Label(change) is { } label
        ? $"Last change · Day {change.Day} · {label}\n{LiveViewModel.DescribeParameters(change)} · effective Day {change.Day + 1}"
        : $"Last change · Day {change.Day} · {LiveViewModel.DescribeParameters(change)} · effective Day {change.Day + 1}";
    public static string Selection(ConfigurationChange change) =>
        (Label(change) is { } label ? label + "\n" : "") + $"Day {change.Day} · {LiveViewModel.DescribeParameters(change)}";
    public static string Marker(ConfigurationChange change) => $"Day {change.Day}\n"
        + (Label(change) is { } label ? label + "\n" : "")
        + $"{LiveViewModel.DescribeParameters(change)}\nEffective Day {change.Day + 1}; timing does not establish causality.";
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ConfigurationChange change ? Selection(change) : "";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
