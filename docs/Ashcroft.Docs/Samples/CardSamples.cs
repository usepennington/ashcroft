using Ashcroft;
using SkiaSharp;

namespace Ashcroft.Docs.Samples;

/// <summary>
/// The doc site's worked examples. Each method returns a deferred <see cref="CardBuilder"/>.
/// These are the single source of truth: the page shows this source via a <c>:symbol</c> embed,
/// and the image gallery executes the very same method to render its picture — code shown == code run.
/// </summary>
public static class CardSamples
{
    // One line of content, all defaults, over a built-in gradient.
    public static CardBuilder MinimumViable()
    {
        return SocialCard.Create()
            .Background(Backgrounds.LinearGradient("#0f172a", "#1e3a8a"))
            .At(Anchor.Center, s => s.Title("April Release Notes"));
    }

    // Text over a photo: the scrim switches on automatically so the title stays legible.
    public static CardBuilder ImageBackgroundWithScrim()
    {
        return SocialCard.Create()
            .Background("assets/hero.png")
            .At(Anchor.BottomLeft, s => s
                .Title("Legible over any photograph")
                .Subtitle("The scrim is a load-bearing default — no configuration required"));
    }

    // The same card with the scrim opted out — rendered on the page next to the one above
    // so the reader can see exactly what the default buys them.
    public static CardBuilder ImageBackgroundWithoutScrim()
    {
        return SocialCard.Create()
            .Background("assets/hero.png")
            .NoScrim()
            .At(Anchor.BottomLeft, s => s
                .Title("Legible over any photograph")
                .Subtitle("The scrim is a load-bearing default — no configuration required"));
    }

    // The expected common case: hero image, logo, title, subtitle, and an avatar + byline row.
    public static CardBuilder BlogCard()
    {
        return SocialCard.Create()
            .Background("assets/hero.png")
            .At(Anchor.TopRight, s => s.Image("assets/logo.png", height: 48))
            .At(Anchor.BottomLeft, s => s
                .Title("Why Your OG Images Look Like Everyone Else's")
                .Subtitle("What HarfBuzz actually does, and why you want it")
                .Spacer(8)
                .Row(r => r
                    .Image("assets/avatar.png", height: 44, shape: ImageShape.Circle)
                    .Meta("Phil Scott · June 2026")));
    }

    // A generative background drawn straight onto the raw SKCanvas, with a custom theme color.
    public static CardBuilder Generative()
    {
        return SocialCard.Create(CardSize.Square)
            .Theme(new Theme { TextColor = "#a7f3d0" })
            .Background(DrawIsoGrid)
            .At(Anchor.Center, s => s
                .Title("ashcroft v1.0", size: 88)
                .Meta("social cards for .NET"));
    }

    // Card-wide theming: a mint text color over a deep radial gradient.
    public static CardBuilder Themed()
    {
        return SocialCard.Create()
            .Theme(new Theme { TextColor = "#a7f3d0" })
            .Background(Backgrounds.RadialGradient("#0b1020", "#020617"))
            .At(Anchor.BottomLeft, s => s
                .Title("Designed by default")
                .Subtitle("Describe what the text is, not what it looks like")
                .Meta("dotnet add package Ashcroft"));
    }

    // A title that fits: one line at the full 64px. The pair below shows what happens when it doesn't.
    public static CardBuilder TitleThatFits()
    {
        return SocialCard.Create()
            .Background(Backgrounds.LinearGradient("#312e81", "#0b1020"))
            .At(Anchor.BottomLeft, s => s
                .Title("Shaping text is hard")
                .Meta("ashcroft · the text engine"));
    }

    // A conference-talk title: wraps to three lines, shrinks toward the 70% floor,
    // and finally ellipsizes — but never throws and never overflows the card.
    public static CardBuilder TitleThatShrinks()
    {
        return SocialCard.Create()
            .Background(Backgrounds.LinearGradient("#312e81", "#0b1020"))
            .At(Anchor.BottomLeft, s => s
                .Title("Everything I learned shipping a cross-platform text rendering pipeline " +
                       "built on SkiaSharp, HarfBuzz, and an unreasonable number of glyph metrics, " +
                       "and what I would do differently if I had to start over today")
                .Meta("ashcroft · the text engine"));
    }

    // Mixed scripts in one run: the primary face covers the Latin, and per-run fallback
    // finds an emoji face and a CJK face for the rest. No configuration, no tofu.
    public static CardBuilder EmojiAndCjk()
    {
        return SocialCard.Create()
            .Background(Backgrounds.LinearGradient("#7c2d12", "#0c0a09"))
            .At(Anchor.Center, s => s
                .Title("Shipping 🚀 to 東京")
                .Subtitle("Emoji and CJK fall back per run — they just work"));
    }

    // Per-element overrides: a custom Text() kicker, a tinted subtitle, an accent meta.
    // The title stays on the theme default to show overrides are opt-in, not all-or-nothing.
    public static CardBuilder ElementColors()
    {
        return SocialCard.Create()
            .Background(Backgrounds.RadialGradient("#1e1b4b", "#0b1020"))
            .At(Anchor.BottomLeft, s => s
                .Text("CASE STUDY", new TextStyle { Size = 22, Weight = 600, LetterSpacing = 4, Color = "#fbbf24" })
                .Title("Coloring outside the theme")
                .Subtitle("Every role takes an optional color and size", color: "#93c5fd")
                .Meta("ashcroft.dev", color: "#fbbf24"));
    }

    // A font file shipped with the app: FontPath loads it directly, no installation needed.
    // One file is one face — it carries every weight on the card, so pick a face that can.
    public static CardBuilder CustomFont()
    {
        return SocialCard.Create()
            .Theme(new Theme { FontPath = "assets/SpaceGrotesk-Bold.ttf" })
            .Background(Backgrounds.LinearGradient("#134e4a", "#0f172a"))
            .At(Anchor.Center, s => s
                .Title("Bring your own typeface")
                .Meta("Theme.FontPath · any TTF or OTF"));
    }

    // The override knobs in one card: wider padding, a capped text column that forces the
    // wrap, a looser gap, and a rounded-rect image pinned to the opposite edge.
    public static CardBuilder FineTuning()
    {
        return SocialCard.Create()
            .Padding(80)
            .Background("#0b1020")
            .At(Anchor.MiddleLeft, s => s
                .MaxWidth(560)
                .Gap(20)
                .Title("Every default has an override")
                .Subtitle("Padding, MaxWidth, Gap, Align — when the defaults aren't enough"))
            .At(Anchor.MiddleRight, s => s
                .Image("assets/hero.png", width: 360, shape: ImageShape.Rounded, cornerRadius: 24));
    }

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
}
