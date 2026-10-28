using System.Reflection;

namespace Simulation.UI;

/// <summary>Identity embedded at build time, never read from the user's checkout at runtime.</summary>
public static class ProductIdentity
{
    public const string Name = "FLOW";
    private static string Metadata(string key) => typeof(ProductIdentity).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == key).Value!;
    public static string Version => Metadata("FlowPublicVersion");
    public static string Commit => Metadata("FlowCommit");
    public static string SourceState => Metadata("FlowSourceState");
    public static string Build => $"Commit: {Commit}\nSource: {SourceState}";
    public static string Description => $"FLOW {Version}\nSoftware Development Simulation\n\n{Build}\n\nThe application version is separate from simulation model and saved-data format versions.";
}
