using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
namespace SpritePrototype;
internal static class PrototypeVerification
{
    private static readonly string Output = Environment.GetEnvironmentVariable("SPRITE_VERIFICATION_OUTPUT") ?? "docs/verification/game-view-b1-2/screenshots";
    internal static async Task Run(PreviewWindow window)
    {
        Directory.CreateDirectory(Output);
        var scene = OfficeScene.Create();
        Require(OfficeScene.Workers.Count == 4, "Four logical workers");
        Require(scene.Count(o => o.AssetId == "desk") == 3, "Three desks");
        Require(scene.Count(o => o.AssetId == "computer") == 3, "Three separate computers");
        Require(scene.Count(o => o.AssetId == "chair") == 3, "Three separate chairs");
        Require(scene.Count(o => o.AssetId == "floor") == 1, "One shared floor");
        Require(OfficeScene.Workers.Count(w => w.State == SpritePose.Working) == 1 && OfficeScene.Workers.Count(w => w.State == SpritePose.Idle) == 1 && OfficeScene.Workers.Count(w => w.State == SpritePose.Collaborating) == 2, "Illustrative states");
        var ordered = SpritePlacement.InDrawOrder(scene.Reverse());
        Require(ordered.Select(o => o.Id).SequenceEqual(window.World.Objects.Select(o => o.Id)), "Stable draw order independent of insertion");
        foreach (var station in OfficeScene.Stations)
            Require(Array.FindIndex(ordered, o => o.Id == station.Id + "/desk") < Array.FindIndex(ordered, o => o.Id == station.Id + "/computer"), "Desktop overlap " + station.Id);
        foreach (var worker in OfficeScene.Workers.Where(w => w.Presentation.Seated))
        {
            Require(Array.FindIndex(ordered, o => o.Id == worker.Id) < Array.FindIndex(ordered, o => o.Id == worker.StationId + "/desk"), "Lower body occlusion");
            Require(Array.FindIndex(ordered, o => o.Id == worker.Id + "/upper-body") > Array.FindIndex(ordered, o => o.Id == worker.StationId + "/computer"), "Hands above keyboard");
            var moved = worker with { Position = OfficeScene.Offset(worker.Position, 1, 0) };
            var parts = moved.Compose(OfficeScene.Stations.Single(s => s.Id == worker.StationId).Position).ToArray();
            Require(parts.Length == 2 && parts.All(p => p.Position == moved.Position), "Worker layers move together");
        }
        var fullBody = SpriteCatalog.Assets["developer-seated"];
        Require(ReferenceEquals(window.Assets.Get(fullBody), window.Assets.Get(SpriteCatalog.Assets["developer-seated-upper"])), "Layers share one decoded image");
        var testObject = scene.Single(o => o.Id == "B/desk");
        var rect = SpritePlacement.Destination(testObject, SpriteCatalog.Assets["desk"], window.World.Projection);
        var twice = SpritePlacement.Destination(testObject with { Scale = testObject.Scale * 2 }, SpriteCatalog.Assets["desk"], window.World.Projection);
        Require(twice.Size == rect.Size * 2, "Per-instance consistent scaling");
        foreach (var asset in SpriteCatalog.Assets.Values)
        {
            Require(asset.Resource.Scheme == "avares", "Embedded resource");
            Require(ReferenceEquals(window.Assets.Get(asset), window.Assets.Get(asset)), "Cached " + asset.Id);
        }
        Require(window.Assets.DecodeCount == SpriteCatalog.Assets.Values.Select(a => a.Resource).Distinct().Count(), "Shared catalog textures, multiple instances");
        Require(!typeof(SpriteObject).Assembly.GetReferencedAssemblies().Any(a => a.Name!.StartsWith("Simulation.")), "No simulation reference");
        var seated = OfficeScene.Workers[0];
        Require((seated with { State = SpritePose.Idle }).ToSprite().AssetId == "developer", "Reusable appearance selects standing pose");
        try { (seated with { State = SpritePose.Walking }).ToSprite(); throw new Exception("Invented walking artwork"); } catch (InvalidOperationException) { }
        await Task.Delay(200);
        Save(window, "office-1280"); Save(window.World, "scene-1x"); Save(window.World, "scene-2x", 2);
        window.SetLight(true); await Task.Delay(80); Save(window, "light-surround"); window.SetLight(false);
        window.Width = 960; window.Height = 800; await Task.Delay(120); Save(window, "office-960");
        window.Width = 800; window.Height = 640; await Task.Delay(120); Save(window, "office-800");
        foreach (var item in scene)
            Require(new Rect(0, 0, 1280, 880).Contains(SpritePlacement.Destination(item, SpriteCatalog.Assets[item.AssetId], window.World.Projection)), "Unclipped " + item.Id);
        window.SetOffice(false); await Task.Delay(80); Save(window, "single-desk"); Require(window.World.Objects.Count == 6, "B1.1 retained");
        window.SetOffice(true); await Task.Delay(80); Require(window.World.Objects.Count == scene.Count, "Office restored");
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 30; i++) { using var target = new RenderTargetBitmap(new(1280, 880)); target.Render(window.World); }
        watch.Stop(); Require(window.Assets.DecodeCount == SpriteCatalog.Assets.Values.Select(a => a.Resource).Distinct().Count(), "Repaints and scene changes reuse bitmaps");
        Console.WriteLine($"30 offscreen office renders: {watch.ElapsedMilliseconds} ms; {scene.Count} independently positioned objects, {window.Assets.DecodeCount} decoded textures.");
        Console.WriteLine($"PASS multi-instance cache, four workers, states, deterministic depth order, 1280/960/800 layouts, 1x/2x render, single-desk mode, no simulation reference. Backend: {(Program.Headless ? "Headless + Skia" : "native macOS")}.");
    }
    private static void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); }
    private static void Save(Control c, string name, double scale = 1)
    {
        c.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(c.Bounds.Width * scale), (int)Math.Ceiling(c.Bounds.Height * scale)), new(96 * scale, 96 * scale));
        bitmap.Render(c); bitmap.Save(Path.Combine(Output, name + ".png"));
    }
}
