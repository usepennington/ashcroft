using SkiaSharp;

namespace Ashcroft.Tests;

public class SmokeTests
{
    [Fact]
    public void SkiaSharp_native_assets_load()
    {
        // Proves the native runtime is wired up before we lean on it everywhere.
        using var surface = SKSurface.Create(new SKImageInfo(8, 8));
        Assert.NotNull(surface);
        surface.Canvas.Clear(SKColors.Black);
    }

    [Fact]
    public void Diagnostics_hook_is_reachable()
    {
        string? seen = null;
        AshcroftDiagnostics.Log = m => seen = m;
        AshcroftDiagnostics.Report("hello");
        Assert.Equal("hello", seen);
        AshcroftDiagnostics.Log = null;
    }
}
