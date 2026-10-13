namespace Simulation.Sprites;

/// <summary>Presentation-only elapsed time. The caller supplies monotonic timestamps; no simulation dependency.</summary>
public sealed class PresentationClock
{
    private TimeSpan lastTimestamp;
    public bool IsRunning { get; private set; }
    public double Seconds { get; private set; }
    public void SetRunning(bool running, TimeSpan timestamp)
    {
        Sample(timestamp);
        IsRunning = running;
    }
    public void Sample(TimeSpan timestamp)
    {
        if (timestamp < lastTimestamp) return;
        if (IsRunning) Seconds += (timestamp - lastTimestamp).TotalSeconds;
        lastTimestamp = timestamp;
    }
    public void Reset(TimeSpan timestamp)
    {
        Seconds = 0; lastTimestamp = timestamp;
    }
    public static bool ShouldRun(bool attached, bool visible, bool inViewport, bool spriteMode,
        bool animationEnabled, bool simulationRunning, bool hasSession) =>
        attached && visible && inViewport && spriteMode && animationEnabled && simulationRunning && hasSession;
}

/// <summary>Direct time sampling skips missed frames. No random values, tasks, or event queue.</summary>
public static class SpriteFrameSampling
{
    public static int Index(int frameCount, double framesPerSecond, double seconds)
    {
        if (frameCount <= 0) throw new ArgumentOutOfRangeException(nameof(frameCount));
        if (!double.IsFinite(framesPerSecond) || framesPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        return (int)Math.Floor(seconds % (frameCount / framesPerSecond) * framesPerSecond) % frameCount;
    }
}
