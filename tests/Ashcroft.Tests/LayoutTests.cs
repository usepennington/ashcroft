using Ashcroft.Internal;
using SkiaSharp;

namespace Ashcroft.Tests;

public class LayoutTests : IDisposable
{
    private readonly string _imagePath;

    public LayoutTests()
    {
        // A real 100×50 PNG so the image-measurement path (decode + aspect) is exercised.
        _imagePath = Path.Combine(Path.GetTempPath(), $"ashcroft_layout_{Guid.NewGuid():N}.png");
        using var surface = SKSurface.Create(new SKImageInfo(100, 50));
        surface.Canvas.Clear(SKColors.Teal);
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.OpenWrite(_imagePath);
        data.SaveTo(fs);
    }

    public void Dispose()
    {
        if (File.Exists(_imagePath)) File.Delete(_imagePath);
    }

    private static LaidOutCard Layout(CardBuilder card)
    {
        using var resolver = new FontResolver(card.ThemeValue);
        using var shaper = new TextShaper(resolver);
        using var images = new ImageLoader();
        var engine = new LayoutEngine(shaper, images, card.ThemeValue, card.Size, card.PaddingValue);
        return engine.Layout(card.Groups);
    }

    private static PlacedGroup Single(CardBuilder card) => Layout(card).Groups.Single();

    [Fact]
    public void BottomLeft_pins_left_and_bottom_edges()
    {
        var g = Single(SocialCard.Create().At(Anchor.BottomLeft, s => s.Title("Pinned")));
        Assert.Equal(64f, g.Bounds.Left, 1);
        Assert.Equal(630 - 64f, g.Bounds.Bottom, 1);

        var text = Assert.IsType<PlacedText>(g.Elements[0]);
        Assert.Equal(HorizontalAlign.Left, text.Align);
        Assert.Equal(64f, text.BlockX, 1);
    }

    [Fact]
    public void TopRight_pins_right_and_top_edges_and_right_aligns()
    {
        var g = Single(SocialCard.Create().At(Anchor.TopRight, s => s.Image(_imagePath, width: 100, height: 50)));
        Assert.Equal(64f, g.Bounds.Top, 1);
        Assert.Equal(1200 - 64f, g.Bounds.Right, 1);

        var img = Assert.IsType<PlacedImage>(g.Elements[0]);
        Assert.Equal(1200 - 64f, img.Dest.Right, 1);
        Assert.Equal(64f, img.Dest.Top, 1);
        Assert.Equal(100f, img.Dest.Width, 1);
        Assert.Equal(50f, img.Dest.Height, 1);
    }

    [Fact]
    public void Center_centers_on_both_axes()
    {
        var g = Single(SocialCard.Create().At(Anchor.Center, s => s.Title("Mid")));
        Assert.Equal(600f, g.Bounds.MidX, 1);
        Assert.Equal(315f, g.Bounds.MidY, 1);
        Assert.Equal(HorizontalAlign.Center, Assert.IsType<PlacedText>(g.Elements[0]).Align);
    }

    [Fact]
    public void MiddleRight_centers_vertically_and_right_aligns()
    {
        var g = Single(SocialCard.Create().At(Anchor.MiddleRight, s => s.Meta("x")));
        Assert.Equal(315f, g.Bounds.MidY, 1);
        Assert.Equal(1200 - 64f, g.Bounds.Right, 1);
        Assert.Equal(HorizontalAlign.Right, Assert.IsType<PlacedText>(g.Elements[0]).Align);
    }

    [Fact]
    public void Explicit_align_overrides_anchor_inheritance()
    {
        var g = Single(SocialCard.Create().At(Anchor.BottomLeft, s => s.Align(HorizontalAlign.Center).Meta("x")));
        Assert.Equal(HorizontalAlign.Center, Assert.IsType<PlacedText>(g.Elements[0]).Align);
    }

    [Fact]
    public void Stack_height_is_sum_of_elements_plus_gaps()
    {
        var one = Single(SocialCard.Create().At(Anchor.TopLeft, s => s.Meta("line"))).Bounds.Height;
        var two = Single(SocialCard.Create().At(Anchor.TopLeft, s => s.Gap(20).Meta("line").Meta("line"))).Bounds.Height;
        Assert.Equal(2 * one + 20, two, 1);
    }

    [Fact]
    public void Spacer_adds_exactly_its_pixels()
    {
        var noSpacer = Single(SocialCard.Create().At(Anchor.TopLeft, s => s.Meta("a").Meta("b"))).Bounds.Height;
        var withSpacer = Single(SocialCard.Create().At(Anchor.TopLeft, s => s.Meta("a").Spacer(20).Meta("b"))).Bounds.Height;
        Assert.Equal(noSpacer + 20, withSpacer, 1);
    }

    [Fact]
    public void Row_width_is_sum_plus_gaps_and_height_is_max_child()
    {
        var metaWidth = Single(SocialCard.Create().At(Anchor.TopLeft, s => s.Meta("Phil Scott"))).Bounds.Width;

        var g = Single(SocialCard.Create().At(Anchor.BottomLeft, s => s
            .Row(r => r
                .Image(_imagePath, width: 40, height: 40)
                .Meta("Phil Scott"))));

        var row = Assert.IsType<PlacedRow>(g.Elements[0]);
        Assert.Equal(2, row.Children.Count);
        // 40 (image) + 12 (default row gap) + meta width.
        Assert.Equal(40 + 12 + metaWidth, g.Bounds.Width, 1);
        // Image (40) is taller than a single line of Meta, so the row height is 40.
        Assert.Equal(40f, g.Bounds.Height, 1);
    }

    [Fact]
    public void Narrower_max_width_wraps_taller()
    {
        var style = new TextStyle { Size = 28, MaxLines = 20 };
        const string text = "A long paragraph of text that will wrap differently depending on the width available to it";

        var wide = Single(SocialCard.Create().At(Anchor.TopLeft, s => s.MaxWidth(1000).Text(text, style))).Bounds.Height;
        var narrow = Single(SocialCard.Create().At(Anchor.TopLeft, s => s.MaxWidth(250).Text(text, style))).Bounds.Height;

        Assert.True(narrow > wide, $"narrow ({narrow}) should be taller than wide ({wide})");
    }

    [Fact]
    public void Missing_image_throws_file_not_found_with_path()
    {
        var card = SocialCard.Create().At(Anchor.Center, s => s.Image("does/not/exist.png", height: 40));
        var ex = Assert.Throws<FileNotFoundException>(() => Layout(card));
        Assert.Contains("does/not/exist.png", ex.Message);
    }
}
