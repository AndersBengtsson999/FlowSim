using System.ComponentModel;
using System.Globalization;
using Simulation.Core;

namespace Simulation.UI.ViewModels;

public enum EffortKind { Fixed, Triangular }

/// <summary>Input editing only; distribution validation and sampling belong to Core.</summary>
public sealed class EffortEditorViewModel(string stage) : INotifyPropertyChanged
{
    private EffortKind kind;
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Stage { get; } = stage;
    public IReadOnlyList<EffortKind> Kinds { get; } = Array.AsReadOnly(Enum.GetValues<EffortKind>());
    public EffortKind Kind
    {
        get => kind;
        set { kind = value; PropertyChanged?.Invoke(this, new(string.Empty)); }
    }
    public bool IsFixed => Kind == EffortKind.Fixed;
    public bool IsTriangular => Kind == EffortKind.Triangular;
    public string FixedValue { get; set; } = "";
    public string Minimum { get; set; } = "";
    public string MostLikely { get; set; } = "";
    public string Maximum { get; set; } = "";

    public IEffortDistribution Read()
    {
        try
        {
            return Kind switch
            {
                EffortKind.Fixed => new FixedEffort(Number(FixedValue, "Effort")),
                EffortKind.Triangular => new TriangularEffort(Number(Minimum, "Minimum"), Number(MostLikely, "Most Likely"), Number(Maximum, "Maximum")),
                _ => throw new ArgumentException("Select Fixed or Triangular.")
            };
        }
        catch (ArgumentException ex) { throw new ArgumentException($"{Stage}: {ex.Message}", ex); }
    }

    public void Load(IEffortDistribution distribution)
    {
        switch (distribution)
        {
            case FixedEffort f:
                FixedValue = f.Effort.ToString(CultureInfo.InvariantCulture); kind = EffortKind.Fixed; break;
            case TriangularEffort t:
                Minimum = t.Minimum.ToString(CultureInfo.InvariantCulture);
                MostLikely = t.MostLikely.ToString(CultureInfo.InvariantCulture);
                Maximum = t.Maximum.ToString(CultureInfo.InvariantCulture); kind = EffortKind.Triangular; break;
            default: throw new ArgumentException("Unsupported effort distribution.", nameof(distribution));
        }
        PropertyChanged?.Invoke(this, new(string.Empty));
    }

    private static double Number(string text, string label) =>
        double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        && double.IsFinite(value) ? value : throw new ArgumentException($"{label}: enter a finite number.");
}
