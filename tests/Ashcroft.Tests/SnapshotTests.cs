using System.Runtime.CompilerServices;
using SkiaSharp;

namespace Ashcroft.Tests;

/// <summary>
/// Renders the spec's worked examples and compares them against approved PNGs with a pixel tolerance.
/// Baselines are self-seeding: the first run on a machine writes the approved image and passes; later
/// runs diff against it. Because Ashcroft resolves system fonts, baselines are platform-specific — delete
/// the <c>Approved/</c> folder to re-seed after an intentional visual change or on a new OS.
/// </summary>
public class SnapshotTests : IDisposable
{
    private const double Tolerance = 2.0; // mean per-channel difference allowed (0–255)
    private readonly List<string> _temp = new();

    private static string ApprovedDir([CallerFilePath] string file = "")
        => Path.Combine(Path.GetDirectoryName(file)!, "Approved");

    public void Dispose()
    {
        foreach (var p in _temp)
            if (File.Exists(p)) File.Delete(p);
    }

    [Fact]
    public void Minimum_viable_card()
    {
        var bytes = SocialCard.Create()
            .Background(Backgrounds.LinearGradient("#0f172a", "#1e3a8a"))
            .At(Anchor.Center, s => s.Title("April Release Notes"))
            .ToBytes();

        Approve("minimum_viable", bytes);
    }

    [Fact]
    public void Blog_post_card()
    {
        var hero = TempImage(1600, 900, SKColors.DarkSlateGray);
        var logo = TempImage(200, 80, SKColors.White);
        var avatar = TempImage(120, 120, SKColors.Goldenrod);

        var bytes = SocialCard.Create()
            .Background(hero)
            .At(Anchor.TopRight, s => s.Image(logo, height: 48))
            .At(Anchor.BottomLeft, s => s
                .Title("Why Your OG Images Look Like Everyone Else's")
                .Subtitle("What HarfBuzz actually does, and why you want it")
                .Spacer(8)
                .Row(r => r
                    .Image(avatar, height: 44, shape: ImageShape.Circle)
                    .Meta("Phil Scott · June 2026")))
            .ToBytes();

        Approve("blog_post", bytes);
    }

    [Fact]
    public void Generative_background_custom_theme()
    {
        var bytes = SocialCard.Create(CardSize.Square)
            .Theme(new Theme { FontFamily = "JetBrains Mono", TextColor = "#a7f3d0" })
            .Background(DrawIsoGrid)
            .At(Anchor.Center, s => s
                .Title("ashcroft v1.0", size: 88)
                .Meta("dotnet add package Ashcroft"))
            .ToBytes();

        Approve("generative", bytes);
    }

    // A deterministic geometric background (no randomness, so snapshots are stable).
    private static void DrawIsoGrid(SKCanvas canvas, SKSizeI size)
    {
        canvas.Clear(new SKColor(0x0b, 0x10, 0x20));
        using var paint = new SKPaint
        {
            Color = SKColors.SlateBlue.WithAlpha(70),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
        };
        const int step = 64;
        for (var x = -size.Height; x < size.Width; x += step)
        {
            canvas.DrawLine(x, 0, x + size.Height, size.Height, paint);
            canvas.DrawLine(x + size.Height, 0, x, size.Height, paint);
        }
    }

    // --- approval plumbing ---

    private void Approve(string name, byte[] received)
    {
        Directory.CreateDirectory(ApprovedDir());
        var approvedPath = Path.Combine(ApprovedDir(), name + ".png");

        if (!File.Exists(approvedPath))
        {
            File.WriteAllBytes(approvedPath, received);
            return; // seeded
        }

        var diff = MeanChannelDifference(File.ReadAllBytes(approvedPath), received, name);
        Assert.True(diff <= Tolerance, $"Snapshot '{name}' differs from approved by {diff:F2} (> {Tolerance}).");
    }

    private static double MeanChannelDifference(byte[] approved, byte[] received, string name)
    {
        using var a = SKBitmap.Decode(approved);
        using var b = SKBitmap.Decode(received);
        Assert.True(a.Width == b.Width && a.Height == b.Height,
            $"Snapshot '{name}' size {b.Width}x{b.Height} != approved {a.Width}x{a.Height}.");

        var pa = a.Pixels;
        var pb = b.Pixels;
        long total = 0;
        for (var i = 0; i < pa.Length; i++)
        {
            total += Math.Abs(pa[i].Red - pb[i].Red);
            total += Math.Abs(pa[i].Green - pb[i].Green);
            total += Math.Abs(pa[i].Blue - pb[i].Blue);
        }
        return (double)total / (pa.Length * 3);
    }

    private string TempImage(int w, int h, SKColor color)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ashcroft_snap_{Guid.NewGuid():N}.png");
        _temp.Add(path);
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        surface.Canvas.Clear(color);
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.OpenWrite(path);
        data.SaveTo(fs);
        return path;
    }
}
