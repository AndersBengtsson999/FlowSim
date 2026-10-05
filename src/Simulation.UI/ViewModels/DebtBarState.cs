using Simulation.Core;
namespace Simulation.UI.ViewModels;

/// <summary>Presentation only. Above tolerance is red; equality has zero overhead.</summary>
public sealed record DebtBarState(double Ratio, double Tolerance, double Overhead, double ScaleMaximum)
{
    public double GreenEnd => .7 * Tolerance / ScaleMaximum;
    public double YellowEnd => Tolerance / ScaleMaximum;
    public double Marker => Ratio / ScaleMaximum;
    public string Zone => Ratio > Tolerance ? "Above tolerance" : Ratio >= .7 * Tolerance && Tolerance > 0 ? "Upper tolerance range" : "Within tolerance";
    public static DebtBarState From(TechnicalDebtState state, TechnicalDebtSettings settings) =>
        new(state.Ratio, settings.Tolerance, state.Overhead(settings), Math.Max(.01, Math.Max(2 * settings.Tolerance, state.Ratio <= double.MaxValue / 1.2 ? 1.2 * state.Ratio : double.MaxValue)));
}
