using System.Globalization;
using Avalonia.Data.Converters;
using Simulation.Core;
namespace Simulation.UI.ViewModels;

public sealed class DeliveryCostDetails : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DeliveryCost { IsComplete: true } cost
            ? $"Observed item capacity so far\nDevelopment {cost.Development:0.##}\nCode Review {cost.CodeReview:0.##}\nRework {cost.Rework:0.##}\nTesting {cost.Testing:0.##}\nTotal {cost.Total:0.##}\nDebt repayment excluded."
            : "Full lifecycle delivery cost unavailable: historical consumed capacity is incomplete.";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
