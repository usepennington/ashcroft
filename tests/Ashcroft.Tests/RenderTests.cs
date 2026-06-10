using SkiaSharp;

namespace Ashcroft.Tests;

public class RenderTests : IDisposable
{
    private readonly List<string> _temp = new();

    private string TempPath(string ext)
    {
        var p = Path.Combine(Path.GetTempPath(), $"ashcroft_render_{Guid.NewGuid():N}{ext}");
        _temp.Add(p);
        return p;
    }

    private string TempImage(int w, int h, SKColor color)
    {
        var path = TempPath(".png");
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        surface.Canvas.Clear(color);
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.OpenWrite(path);
        data.SaveTo(fs);
        return path;
    }

    public void Dispose()
    {
        foreach (var p in _temp)
            if (File.Exists(p)) File.Delete(p);
        AshcroftDiagnostics.Log = null;
    }

    private static (int Width, int Height) Dimensions(byte[] bytes)
    {
        using var bmp = SKBitmap.Decode(bytes);
        Assert.NotNull(bmp); // decodes => valid encoded image
        return (bmp.Width, bmp.Height);
    }

    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.Jpeg)]
    [InlineData(ImageFormat.Webp)]
    public void ToBytes_produces_valid_image_of_card_size(ImageFormat format)
    {
        var bytes = SocialCard.Create()
            .Background("#1e293b")
            .At(Anchor.Center, s => s.Title("Hello"))
            .ToBytes(format);

        var (w, h) = Dimensions(bytes);
        Assert.Equal(1200, w);
        Assert.Equal(630, h);
    }

    [Fact]
    public void Scale_multiplies_pixel_dimensions()
    {
        using var image = SocialCard.Create().Scale(2).Background("#000").ToImage();
        Assert.Equal(2400, image.Width);
        Assert.Equal(1260, image.Height);
    }

    [Fact]
    public void Save_infers_format_from_extension()
    {
        var path = TempPath(".jpg");
        SocialCard.Create().Background("#222").At(Anchor.BottomLeft, s => s.Title("Saved")).Save(path);

        Assert.True(File.Exists(path));
        var (w, h) = Dimensions(File.ReadAllBytes(path));
        Assert.Equal(1200, w);
        Assert.Equal(630, h);
    }

    [Fact]
    public void Save_with_unknown_extension_throws()
    {
        var path = TempPath(".bmp");
        Assert.Throws<ArgumentException>(() => SocialCard.Create().Background("#222").Save(path));
    }

    [Fact]
    public void Missing_background_image_throws_file_not_found_with_path()
    {
        const string missing = "no/such/background.jpg";
        var ex = Assert.Throws<FileNotFoundException>(() =>
            SocialCard.Create().Background(missing).ToBytes());
        Assert.Contains(missing, ex.Message);
    }

    [Fact]
    public void Empty_card_renders_background_only()
    {
        var bytes = SocialCard.Create().Background("#0f172a").ToBytes();
        var (w, h) = Dimensions(bytes);
        Assert.Equal(1200, w);
        Assert.Equal(630, h);
    }

    [Fact]
    public void Default_background_is_near_black()
    {
        using var image = SocialCard.Create().ToImage();
        using var bmp = SKBitmap.FromImage(image);
        var c = bmp.GetPixel(10, 10);
        Assert.Equal(0x11, c.Red);
        Assert.Equal(0x18, c.Green);
        Assert.Equal(0x27, c.Blue);
    }

    [Fact]
    public void Lambda_background_is_invoked_with_logical_size()
    {
        SKSizeI seen = default;
        SocialCard.Create(800, 400)
            .Background((canvas, size) => { seen = size; canvas.Clear(SKColors.Purple); })
            .ToBytes();

        Assert.Equal(800, seen.Width);
        Assert.Equal(400, seen.Height);
    }

    [Fact]
    public void Full_blog_card_renders_without_error()
    {
        var hero = TempImage(1600, 900, SKColors.DarkSlateGray);
        var avatar = TempImage(120, 120, SKColors.Goldenrod);
        var logo = TempImage(200, 80, SKColors.White);

        var bytes = SocialCard.Create()
            .Background(hero)
            .At(Anchor.TopRight, s => s.Image(logo, height: 48))
            .At(Anchor.BottomLeft, s => s
                .Title("Why Your OG Images Look Like Everyone Else's")
                .Subtitle("A one-line description that sits under the title")
                .Spacer(8)
                .Row(r => r
                    .Image(avatar, height: 44, shape: ImageShape.Circle)
                    .Meta("Phil Scott · 9 min read")))
            .ToBytes();

        var (w, h) = Dimensions(bytes);
        Assert.Equal(1200, w);
        Assert.Equal(630, h);
    }

    [Fact]
    public void Unresolvable_font_falls_back_and_reports_via_diagnostics()
    {
        var messages = new List<string>();
        AshcroftDiagnostics.Log = messages.Add;

        SocialCard.Create()
            .Theme(new Theme { FontFamily = "DefinitelyNotARealFontXYZ123" })
            .Background("#111")
            .At(Anchor.Center, s => s.Title("Fallback"))
            .ToBytes();

        Assert.Contains(messages, m => m.Contains("DefinitelyNotARealFontXYZ123"));
    }

    [Fact]
    public void Solid_color_background_skips_scrim_but_still_renders_text()
    {
        // Pure smoke: a color background means no scrim path, text still drawn.
        var bytes = SocialCard.Create()
            .Background(Backgrounds.LinearGradient("#0f172a", "#3b0764"))
            .At(Anchor.BottomLeft, s => s.Title("Gradient").Meta("no scrim here"))
            .ToBytes();
        Assert.NotEmpty(bytes);
    }
}
