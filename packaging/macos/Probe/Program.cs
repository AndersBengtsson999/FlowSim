using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// Inspect the shipped assemblies, not a project reference or the developer build output.
var bundle = Path.GetFullPath(args[0]);
var runtime = Path.Combine(bundle, "Contents/Resources/runtime");
AssemblyLoadContext.Default.Resolving += (_, name) => File.Exists(Path.Combine(runtime, name.Name + ".dll"))
    ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime, name.Name + ".dll")) : null;
var ui = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime, "Simulation.UI.dll"));
var metadata = ui.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(a => a.Key, a => a.Value);
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(bundle, "Contents/Resources/release.json")));
void Equal(string key, string manifestKey)
{ if (metadata[key] != manifest.RootElement.GetProperty(manifestKey).GetString()) throw new Exception(key + " differs from manifest"); }
Equal("FlowPublicVersion", "version"); Equal("FlowCommit", "revision"); Equal("FlowReleaseDesignation", "releaseDesignation");
Equal("FlowBuildUtc", "buildUtc"); Equal("FlowArchitecture", "rid"); Equal("FlowSourceState", "sourceState");
if (metadata["FlowSourceState"] != "clean archive") throw new Exception("Archive identity absent");
var identity = ui.GetType("Simulation.UI.ProductIdentity", true)!;
var about = (string)identity.GetProperty("Description")!.GetValue(null)!;
var catalogType = ui.GetType("Simulation.UI.Help.GuideCatalog", true)!;
var catalog = Activator.CreateInstance(catalogType)!;
catalogType.GetMethod("Validate")!.Invoke(catalog, [true]);
var resources = ui.GetManifestResourceNames().Where(n => n.StartsWith("Guide/")).Order().ToArray();
if (!resources.Contains("Guide/assets/images/live-session-controls.png")) throw new Exception("Current guide image missing");
var resourceHashes = new Dictionary<string, string>();
foreach (var name in resources)
{ using var stream = ui.GetManifestResourceStream(name)!; resourceHashes[name] = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
var sprites = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime, "Simulation.Sprites.dll"));
if (!sprites.GetManifestResourceNames().Contains("!AvaloniaResources")) throw new Exception("Embedded sprites absent");
// Avalonia's managed resource provider reads all atlas PNGs without starting native graphics.
var loader = Assembly.Load("Avalonia.Base").GetType("Avalonia.Platform.StandardAssetLoader", true)!;
var assetLoader = Activator.CreateInstance(loader, [sprites])!;
var open = loader.GetMethods().Single(m => m.Name == "Open" && m.GetParameters().Length == 2);
var spriteCatalog = sprites.GetType("Simulation.Sprites.SpriteCatalog", true)!;
var definitions = (System.Collections.IEnumerable)spriteCatalog.GetProperty("Assets")!.GetValue(null)!;
var spriteHashes = new Dictionary<string, string>();
foreach (var entry in definitions)
{
    var definition = entry.GetType().GetProperty("Value")!.GetValue(entry)!;
    var uri = (Uri)definition.GetType().GetProperty("Resource")!.GetValue(definition)!;
    if (spriteHashes.ContainsKey(uri.AbsoluteUri)) continue;
    using var stream = (Stream)open.Invoke(assetLoader, [uri, null])!;
    using var bytes = new MemoryStream(); stream.CopyTo(bytes); var png = bytes.ToArray();
    if (png.Length < 8 || !png.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})) throw new Exception("Invalid sprite PNG: "+uri);
    spriteHashes[uri.AbsoluteUri] = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
}
Console.WriteLine(JsonSerializer.Serialize(new { about, metadata, guideResourceHashes=resourceHashes, spriteHashes, guideLinksAndImagesValid=true },new JsonSerializerOptions{WriteIndented=true}));
