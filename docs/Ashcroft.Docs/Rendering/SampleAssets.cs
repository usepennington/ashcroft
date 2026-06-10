using SkiaSharp;

namespace Ashcroft.Docs.Rendering;

/// <summary>
/// Generates the small input images the samples reference (<c>assets/hero.png</c> etc.) so the doc
/// site is self-contained — no binary blobs committed to the repo. Runs once at startup; deterministic,
/// so dev and build produce identical inputs.
/// </summary>
public static class SampleAssets
{
    public static void EnsureInputs(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "assets");
        Directory.CreateDirectory(dir);

        Write(Path.Combine(dir, "hero.png"), 1600, 900, Hero);
        Write(Path.Combine(dir, "avatar.png"), 256, 256, Avatar);
        Write(Path.Combine(dir, "logo.png"), 240, 80, Logo);
    }

    private static void Write(string path, int w, int h, Action<SKCanvas, int, int> draw)
    {
        if (File.Exists(path)) return;
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        draw(surface.Canvas, w, h);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.OpenWrite(path);
        data.SaveTo(fs);
    }

    private static void Hero(SKCanvas canvas, int w, int h)
    {
        using var bg = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(w, h),
                new[] { new SKColor(0x10, 0x2a, 0x43), new SKColor(0x1e, 0x29, 0x3b), new SKColor(0x3b, 0x10, 0x4a) },
                null, SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(0, 0, w, h, bg);

        using var glow = new SKPaint { Color = SKColors.SteelBlue.WithAlpha(40), IsAntialias = true };
        for (var i = 0; i < 9; i++)
            canvas.DrawCircle(w * i / 9f, h * 0.75f, 220, glow);
    }

    private static void Avatar(SKCanvas canvas, int w, int h)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(w * 0.35f, h * 0.3f), w * 0.9f,
                new[] { new SKColor(0xfb, 0xbf, 0x24), new SKColor(0xb4, 0x53, 0x09) },
                null, SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(0, 0, w, h, paint);
    }

    private static void Logo(SKCanvas canvas, int w, int h)
    {
        canvas.Clear(SKColors.Transparent);
        using var pill = new SKPaint { Color = SKColors.White.WithAlpha(235), IsAntialias = true };
        using var rrect = new SKRoundRect(SKRect.Create(0, 0, w, h), 16);
        canvas.DrawRoundRect(rrect, pill);
        using var bar = new SKPaint { Color = new SKColor(0x1e, 0x29, 0x3b), IsAntialias = true };
        canvas.DrawRect(20, h / 2f - 6, w - 40, 12, bar);
    }
}
