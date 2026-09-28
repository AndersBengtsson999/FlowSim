using System.Text.Json;
using System.Text.Json.Serialization;
using Simulation.Application;
using Simulation.Core;

namespace Simulation.Infrastructure;

public sealed record ScenarioDocument(int SchemaVersion, string SimulationModelVersion, string DocumentKind, ScenarioDefinition Scenario);
public sealed record ExperimentDocument(int SchemaVersion, string SimulationModelVersion, string DocumentKind, Experiment Experiment);

/// <summary>Explicit JSON schema for configuration, not execution state or UI objects.</summary>
public static class ExperimentJson
{
    public const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, IgnoreReadOnlyProperties = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new JsonStringEnumConverter()); options.Converters.Add(new EffortConverter()); return options;
    }
    public static string SaveScenario(ScenarioDefinition scenario)
    {
        new Experiment(Guid.NewGuid(), "Validate", "", new[] { scenario }, scenario.Id, new()).Validate();
        return JsonSerializer.Serialize(new ScenarioDocument(SchemaVersion, SimulationModel.Version, "Scenario", scenario), Options);
    }
    public static string SaveExperiment(Experiment experiment)
    { experiment.Validate(); return JsonSerializer.Serialize(new ExperimentDocument(SchemaVersion, SimulationModel.Version, "Experiment", experiment), Options); }
    public static ScenarioDefinition LoadScenario(string json)
    {
        var document = JsonSerializer.Deserialize<ScenarioDocument>(json, Options) ?? throw new JsonException("Empty scenario document.");
        ValidateHeader(document.SchemaVersion, document.SimulationModelVersion, document.DocumentKind, "Scenario");
        if (document.Scenario is null) throw new JsonException("Scenario is required.");
        SaveScenario(document.Scenario); return document.Scenario;
    }
    public static Experiment LoadExperiment(string json)
    {
        var document = JsonSerializer.Deserialize<ExperimentDocument>(json, Options) ?? throw new JsonException("Empty experiment document.");
        ValidateHeader(document.SchemaVersion, document.SimulationModelVersion, document.DocumentKind, "Experiment");
        if (document.Experiment is null) throw new JsonException("Experiment is required.");
        document.Experiment.Validate();
        return document.Experiment with { Scenarios = Array.AsReadOnly(document.Experiment.Scenarios.ToArray()) };
    }
    public static string ConfigurationSnapshot(SimulationRequest request) => JsonSerializer.Serialize(request, Options);
    private static void ValidateHeader(int schema, string model, string kind, string expected)
    {
        if (schema != SchemaVersion) throw new JsonException($"Unsupported SchemaVersion {schema}; expected {SchemaVersion}.");
        if (model != SimulationModel.Version) throw new JsonException($"Unsupported SimulationModelVersion '{model}'; expected {SimulationModel.Version}.");
        if (kind != expected) throw new JsonException($"Expected a {expected} document.");
    }
    public static async Task<string> ReadAsync(string path)
    {
        if (new FileInfo(path).Length > 5_000_000) throw new IOException("Configuration files must be smaller than 5 MB.");
        return await File.ReadAllTextAsync(path);
    }
    public static async Task WriteAsync(string path, string text)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllTextAsync(temporary, text); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private sealed class EffortConverter : JsonConverter<IEffortDistribution>
    {
        public override IEffortDistribution Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader); var root = document.RootElement;
            if (!root.TryGetProperty("Kind", out var kind)) throw new JsonException("Effort distribution requires Kind.");
            double Value(string name) => root.TryGetProperty(name, out var v) && v.TryGetDouble(out var d) ? d : throw new JsonException($"Distribution requires numeric {name}.");
            var allowed = kind.GetString() == "Fixed" ? new[] { "Kind", "Effort" } : new[] { "Kind", "Minimum", "MostLikely", "Maximum" };
            if (root.EnumerateObject().Any(p => !allowed.Contains(p.Name))) throw new JsonException("Unknown effort distribution field.");
            return kind.GetString() switch
            {
                "Fixed" => new FixedEffort(Value("Effort")),
                "Triangular" => new TriangularEffort(Value("Minimum"), Value("MostLikely"), Value("Maximum")),
                _ => throw new JsonException("Effort Kind must be Fixed or Triangular.")
            };
        }
        public override void Write(Utf8JsonWriter writer, IEffortDistribution value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            if (value is FixedEffort f) { writer.WriteString("Kind", "Fixed"); writer.WriteNumber("Effort", f.Effort); }
            else if (value is TriangularEffort t)
            { writer.WriteString("Kind", "Triangular"); writer.WriteNumber("Minimum", t.Minimum); writer.WriteNumber("MostLikely", t.MostLikely); writer.WriteNumber("Maximum", t.Maximum); }
            else throw new JsonException("Only Fixed and Triangular distributions can be saved.");
            writer.WriteEndObject();
        }
    }
}
