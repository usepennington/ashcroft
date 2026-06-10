using Ashcroft.Internal;

namespace Ashcroft.Tests;

public class BuilderTests
{
    [Fact]
    public void Create_defaults_to_open_graph_size()
    {
        var card = SocialCard.Create();
        Assert.Equal(1200, card.Size.Width);
        Assert.Equal(630, card.Size.Height);
    }

    [Fact]
    public void Create_from_preset_resolves_size()
    {
        var card = SocialCard.Create(CardSize.Story);
        Assert.Equal(1080, card.Size.Width);
        Assert.Equal(1920, card.Size.Height);
    }

    [Fact]
    public void Card_defaults_match_spec()
    {
        var card = SocialCard.Create();
        Assert.Equal(64, card.PaddingValue);
        Assert.Equal(1f, card.ScaleFactor);
        Assert.Null(card.ScrimOpacity);
        Assert.False(card.ScrimDisabled);
        Assert.Null(card.BackgroundSource);
        Assert.Empty(card.Groups);
    }

    [Fact]
    public void Hex_background_becomes_color_no_scrim()
    {
        var card = SocialCard.Create().Background("#1e293b");
        var bg = Assert.IsType<ColorBackground>(card.BackgroundSource);
        Assert.False(bg.WarrantsScrim);
        Assert.Equal((byte)0x1e, bg.Color.Red);
    }

    [Fact]
    public void Non_hex_string_background_becomes_image_path_with_scrim()
    {
        var card = SocialCard.Create().Background("assets/header.jpg");
        var bg = Assert.IsType<ImagePathBackground>(card.BackgroundSource);
        Assert.True(bg.WarrantsScrim);
        Assert.Equal("assets/header.jpg", bg.Path);
    }

    [Fact]
    public void Lambda_and_fill_backgrounds_classify_scrim_correctly()
    {
        var lambda = SocialCard.Create().Background((c, s) => { });
        Assert.True(Assert.IsType<LambdaBackground>(lambda.BackgroundSource).WarrantsScrim);

        var fill = SocialCard.Create().Background(Backgrounds.LinearGradient("#000", "#fff"));
        Assert.False(Assert.IsType<FillBackground>(fill.BackgroundSource).WarrantsScrim);
    }

    [Fact]
    public void Stream_background_is_buffered_to_bytes()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        using var ms = new MemoryStream(bytes);
        var card = SocialCard.Create().Background(ms);
        var bg = Assert.IsType<ImageBytesBackground>(card.BackgroundSource);
        Assert.Equal(bytes, bg.Data.ToArray());
    }

    [Fact]
    public void Roles_capture_their_type_scale_and_opacity()
    {
        var card = SocialCard.Create().At(Anchor.BottomLeft, s => s
            .Title("T")
            .Subtitle("S")
            .Meta("M"));

        var elements = card.Groups.Single().Stack.Elements;
        var title = Assert.IsType<TextElement>(elements[0]);
        var subtitle = Assert.IsType<TextElement>(elements[1]);
        var meta = Assert.IsType<TextElement>(elements[2]);

        Assert.Equal(64, title.Style.Size);
        Assert.Equal(700, title.Style.Weight);
        Assert.Equal(3, title.Style.MaxLines);
        Assert.True(title.Style.ShrinkToFit);
        Assert.Equal(Roles.TitleOpacity, title.Opacity);

        Assert.Equal(30, subtitle.Style.Size);
        Assert.Equal(Roles.SubtitleOpacity, subtitle.Opacity);

        Assert.Equal(22, meta.Style.Size);
        Assert.Equal(1, meta.Style.MaxLines);
        Assert.Equal(Roles.MetaOpacity, meta.Opacity);
    }

    [Fact]
    public void Role_overrides_apply_size_and_color()
    {
        var card = SocialCard.Create().At(Anchor.Center, s => s.Title("Hi", color: "#fbbf24", size: 88));
        var title = Assert.IsType<TextElement>(card.Groups.Single().Stack.Elements[0]);
        Assert.Equal(88, title.Style.Size);
        Assert.Equal("#fbbf24", title.ColorOverride);
    }

    [Fact]
    public void Custom_text_uses_its_style_color_and_no_role_opacity()
    {
        var style = new TextStyle { Size = 40, Color = "#a7f3d0" };
        var card = SocialCard.Create().At(Anchor.Center, s => s.Text("x", style));
        var el = Assert.IsType<TextElement>(card.Groups.Single().Stack.Elements[0]);
        Assert.Equal("#a7f3d0", el.ColorOverride);
        Assert.Equal(1f, el.Opacity);
        Assert.Same(style, el.Style);
    }

    [Fact]
    public void Stack_settings_are_captured()
    {
        var card = SocialCard.Create().At(Anchor.TopRight, s => s
            .Gap(20).MaxWidth(600).Align(HorizontalAlign.Right)
            .Image("logo.png", height: 48));

        var stack = card.Groups.Single().Stack;
        Assert.Equal(20, stack.GapValue);
        Assert.Equal(600, stack.MaxWidthValue);
        Assert.Equal(HorizontalAlign.Right, stack.AlignValue);

        var img = Assert.IsType<ImageElement>(stack.Elements[0]);
        Assert.Equal("logo.png", img.Path);
        Assert.Equal(48, img.Height);
    }

    [Fact]
    public void Row_collects_children_and_forbids_nesting()
    {
        var card = SocialCard.Create().At(Anchor.BottomLeft, s => s
            .Row(r => r
                .Image("phil.jpg", height: 44, shape: ImageShape.Circle)
                .Meta("Phil · 9 min read")));

        var row = Assert.IsType<RowElement>(card.Groups.Single().Stack.Elements[0]);
        Assert.Equal(2, row.Children.Count);
        Assert.Equal(ImageShape.Circle, Assert.IsType<ImageElement>(row.Children[0]).Shape);

        Assert.Throws<InvalidOperationException>(() =>
            SocialCard.Create().At(Anchor.Center, s => s.Row(r => r.Row(_ => { }))));
    }

    [Fact]
    public void Scrim_and_noscrim_toggle_state()
    {
        Assert.Equal(0.7f, SocialCard.Create().Scrim(0.7f).ScrimOpacity);
        Assert.True(SocialCard.Create().NoScrim().ScrimDisabled);
    }

    [Fact]
    public void Groups_preserve_insertion_order()
    {
        var card = SocialCard.Create()
            .At(Anchor.TopRight, s => s.Meta("first"))
            .At(Anchor.BottomLeft, s => s.Meta("second"));

        Assert.Equal(Anchor.TopRight, card.Groups[0].Anchor);
        Assert.Equal(Anchor.BottomLeft, card.Groups[1].Anchor);
    }
}
