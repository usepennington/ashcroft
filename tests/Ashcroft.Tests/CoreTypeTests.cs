using Ashcroft.Internal;
using SkiaSharp;

namespace Ashcroft.Tests;

public class CoreTypeTests
{
    [Theory]
    [InlineData("#fff", 255, 255, 255, 255)]
    [InlineData("#f00", 255, 0, 0, 255)]
    [InlineData("fff", 255, 255, 255, 255)]              // leading '#' optional
    [InlineData("#1e293b", 0x1e, 0x29, 0x3b, 255)]
    [InlineData("#FB923C", 0xFB, 0x92, 0x3C, 255)]       // upper-case
    [InlineData("#80ffffff", 255, 255, 255, 0x80)]       // aarrggbb (alpha first)
    public void Parses_valid_hex_colors(string hex, int r, int g, int b, int a)
    {
        var c = Color.Parse(hex);
        Assert.Equal((byte)r, c.Red);
        Assert.Equal((byte)g, c.Green);
        Assert.Equal((byte)b, c.Blue);
        Assert.Equal((byte)a, c.Alpha);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#ff")]       // 2 nibbles
    [InlineData("#fffff")]    // 5 nibbles
    [InlineData("#gggggg")]   // non-hex
    [InlineData("blue")]      // named colors not supported
    public void Rejects_invalid_hex_colors(string hex)
    {
        Assert.False(Color.TryParse(hex, out _));
        Assert.Throws<FormatException>(() => Color.Parse(hex));
    }

    [Fact]
    public void WithOpacity_scales_alpha()
    {
        var c = Color.WithOpacity(new SKColor(255, 255, 255, 255), 0.65f);
        Assert.Equal((byte)Math.Round(255 * 0.65), c.Alpha);
        Assert.Equal((byte)255, c.Red);
    }

    [Theory]
    [InlineData(CardSize.OpenGraph, 1200, 630)]
    [InlineData(CardSize.Square, 1080, 1080)]
    [InlineData(CardSize.Wide, 1920, 1080)]
    [InlineData(CardSize.Story, 1080, 1920)]
    public void Resolves_card_size_presets(CardSize size, int w, int h)
    {
        var s = CardSizes.Resolve(size);
        Assert.Equal(w, s.Width);
        Assert.Equal(h, s.Height);
    }

    [Fact]
    public void TextStyle_has_spec_defaults()
    {
        var s = new TextStyle();
        Assert.Equal(30, s.Size);
        Assert.Equal(400, s.Weight);
        Assert.Equal("#ffffff", s.Color);
        Assert.Equal(1.35f, s.LineHeight);
        Assert.Equal(2, s.MaxLines);
        Assert.False(s.ShrinkToFit);
        Assert.Equal(0, s.LetterSpacing);
        Assert.Null(s.FontFamily);
    }

    [Fact]
    public void Theme_has_spec_defaults()
    {
        var t = new Theme();
        Assert.Equal("", t.FontFamily);
        Assert.Null(t.FontPath);
        Assert.Equal("#ffffff", t.TextColor);
        Assert.Equal(1.0f, t.Scale);
    }
}
