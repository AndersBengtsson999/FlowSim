using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace SpritePrototype;

internal static class Program
{
    internal static bool Verify { get; private set; }
    internal static bool Headless { get; private set; }
    [STAThread]
    public static int Main(string[] args)
    {
        Verify = args.Contains("--verify"); Headless = args.Contains("--headless");
        if (Headless && !Verify) throw new ArgumentException("--headless requires --verify.");
        var builder = AppBuilder.Configure<PreviewApplication>();
        builder = Headless
            ? builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia()
            : builder.UsePlatformDetect().With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] });
        return builder.WithInterFont().StartWithClassicDesktopLifetime([]);
    }
}

public sealed class PreviewApplication : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Dark; }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var window = new PreviewWindow(); desktop.MainWindow = window;
        if (Program.Verify) window.Opened += async (_, _) =>
        {
            try { await PrototypeVerification.Run(window); desktop.Shutdown(0); }
            catch (Exception ex) { Console.Error.WriteLine(ex); desktop.Shutdown(1); }
        };
        base.OnFrameworkInitializationCompleted();
    }
}
