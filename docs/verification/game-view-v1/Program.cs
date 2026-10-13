using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Views;
using Simulation.UI.ViewModels;
using Simulation.UI.Controls;
using ModelValidationV2;
internal static class Program
{
 [STAThread] public static int Main()=>AppBuilder.Configure<PreviewApp>().UsePlatformDetect().With(new AvaloniaNativePlatformOptions{RenderingMode=[AvaloniaNativeRenderingMode.Software]}).WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class PreviewApp:Avalonia.Application
{
 public override void Initialize(){Styles.Add(new FluentTheme());RequestedThemeVariant=ThemeVariant.Light;}
 public override void OnFrameworkInitializationCompleted(){
 var desktop=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;var main=new MainWindowViewModel();var vm=main.Simple.Live;var w=new MainWindow{DataContext=main,Width=1280,Height=1000};desktop.MainWindow=w;
 w.Opened+=async(_,_)=>{try{
 var live=LiveSimulation.Start(Validation.Complex with {Skills=new(2,.3)},WorkArrivalMode.AlwaysAvailable);Validation.Until(live,150);vm.Load(live);await Task.Delay(200);
 var view=w.GetVisualDescendants().OfType<LiveView>().Single();var selector=view.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="LiveVisualization");var scroll=view.GetVisualDescendants().OfType<ScrollViewer>().Single(c=>c.Name=="LiveScroll");
 var before=Validation.Hash(live.Capture());selector.SelectedItem="Game View";await Task.Delay(200);
 Validation.Require(vm.GameViewSelected,"Selector binding");Validation.Require(before==Validation.Hash(live.Capture()),"Switch mutated session");
 var game=view.GetVisualDescendants().OfType<GameView>().Single();Validation.Require(game.IsEffectivelyVisible,"Game visible");
 Validation.Require(game.TranslatePoint(new Point(0,game.Bounds.Height),w)!.Value.Y<=w.Bounds.Height,"Whole scene fits 1280 viewport");
 Save(w,"game-1280");SaveControl(game,"scene-1280");
 w.Width=960;w.Height=820;await Task.Delay(200);Validation.Require(game.TranslatePoint(new Point(0,game.Bounds.Height),w)!.Value.Y<=w.Bounds.Height,"Whole scene fits 960 viewport");Save(w,"game-960");SaveControl(game,"scene-960");
 vm.BeginChange();vm.ChangeLabel="Pending label";vm.Draft.NumberOfTesters="4";selector.SelectedItem="Flow Board";selector.SelectedItem="Game View";await Task.Delay(100);Validation.Require(vm.IsEditing&&vm.ChangeLabel=="Pending label"&&vm.Draft.NumberOfTesters=="4","Draft survives selector");Validation.Require(before==Validation.Hash(live.Capture()),"Draft/switch changed model");vm.CancelChangeCommand.Execute(null);
 selector.SelectedItem="Flow Board";await Task.Delay(100);Validation.Require(!game.IsEffectivelyVisible,"Game hidden");Save(w,"flow-board-960");
 selector.SelectedItem="Game View";vm.Speed=10;vm.TargetDay="250";var elapsed=System.Diagnostics.Stopwatch.StartNew();await vm.RunToDayAsync();elapsed.Stop();await Task.Delay(100);Validation.Require(vm.Day==250&&vm.GameScene.Day==250,"Fast advance refresh");Console.WriteLine($"Fast advance 150→250: {elapsed.ElapsedMilliseconds} ms");
 var large=LiveSimulation.Start(Validation.Baseline with{NumberOfWorkItems=2000,DeveloperCount=200,TesterCount=100,Skills=new(40,.3)},WorkArrivalMode.FixedBacklog);vm.Load(large);await Task.Delay(100);SaveControl(game,"large-queues");Validation.Require(vm.GameScene.Areas[0].VisiblePackages==8,"Bounded packages");
 Console.WriteLine("PASS native selector binding, read-only view switching, pending draft, responsive 1280/960 layouts, existing Flow Board, fast advance and large queue grouping.");vm.Dispose();desktop.Shutdown(0);
 }catch(Exception e){Console.Error.WriteLine(e);vm.Dispose();desktop.Shutdown(1);}};base.OnFrameworkInitializationCompleted();}
 static void Save(Window w,string name)=>SaveControl(w,name);
 static void SaveControl(Control c,string name){c.UpdateLayout();using var bitmap=new RenderTargetBitmap(new PixelSize((int)c.Bounds.Width,(int)c.Bounds.Height));bitmap.Render(c);bitmap.Save(Path.Combine("docs/verification/game-view-v1/screenshots",name+".png"));}
}
