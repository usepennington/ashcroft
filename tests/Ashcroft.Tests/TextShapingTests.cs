using Ashcroft.Internal;

namespace Ashcroft.Tests;

public class TextShapingTests
{
    private static (FontResolver Resolver, TextShaper Shaper) NewShaper(Theme? theme = null)
    {
        var resolver = new FontResolver(theme ?? new Theme());
        return (resolver, new TextShaper(resolver));
    }

    [Fact]
    public void Wide_width_keeps_text_on_one_line()
    {
        var (r, shaper) = NewShaper();
        using (r)
        using (shaper)
        {
            var result = shaper.Shape("Shaping Text the Hard Way", new TextStyle { Size = 40, MaxLines = 5 }, 1f, maxWidth: 10_000);
            Assert.Single(result.Lines);
            Assert.Equal("Shaping Text the Hard Way", result.Lines[0].Text);
            Assert.False(result.Ellipsized);
        }
    }

    [Fact]
    public void Narrow_width_wraps_one_word_per_line()
    {
        var (r, shaper) = NewShaper();
        using (r)
        using (shaper)
        {
            // Identical words at a width that fits one comfortably but never two — robust across fonts.
            var result = shaper.Shape("wrapping wrapping wrapping wrapping", new TextStyle { Size = 20, MaxLines = 99 }, 1f, maxWidth: 150);
            Assert.Equal(4, result.Lines.Count);
            Assert.All(result.Lines, l => Assert.Equal("wrapping", l.Text));
        }
    }

    [Fact]
    public void Explicit_newline_forces_a_break()
    {
        var (r, shaper) = NewShaper();
        using (r)
        using (shaper)
        {
            var result = shaper.Shape("line one\nline two", new TextStyle { Size = 24, MaxLines = 9 }, 1f, maxWidth: 10_000);
            Assert.Equal(2, result.Lines.Count);
            Assert.Equal("line one", result.Lines[0].Text);
            Assert.Equal("line two", result.Lines[1].Text);
        }
    }

    [Fact]
    public void Shrink_to_fit_reduces_size_before_ellipsizing()
    {
        var (r, shaper) = NewShaper();
        using (r)
        using (shaper)
        {
            // Long title, single line allowed, shrink enabled -> size should drop toward the 70% floor.
            var style = new TextStyle { Size = 64, MaxLines = 1, ShrinkToFit = true };
            var result = shaper.Shape("A reasonably long headline that must shrink", style, 1f, maxWidth: 500);
            Assert.True(result.Size < 64f, $"expected shrink below 64, got {result.Size}");
            Assert.True(result.Size >= 64f * 0.70f - 0.01f, $"must not shrink past the 70% floor, got {result.Size}");
        }
    }

    [Fact]
    public void Overflowing_text_ellipsizes_the_last_line()
    {
        var (r, shaper) = NewShaper();
        using (r)
        using (shaper)
        {
            var style = new TextStyle { Size = 40, MaxLines = 1, ShrinkToFit = false };
            var result = shaper.Shape("This is far too much text to ever fit on a single narrow line", style, 1f, maxWidth: 200);
            Assert.True(result.Ellipsized);
            Assert.Single(result.Lines);
            Assert.EndsWith("…", result.Lines[0].Text);
        }
    }

    [Fact]
    public void Theme_scale_multiplies_size()
    {
        var (r, shaper) = NewShaper();
        using (r)
        using (shaper)
        {
            var result = shaper.Shape("Hi", new TextStyle { Size = 30, MaxLines = 1 }, themeScale: 2f, maxWidth: 10_000);
            Assert.Equal(60f, result.Size, 3);
        }
    }

    [Fact]
    public void Pathologically_narrow_width_never_throws()
    {
        var (r, shaper) = NewShaper();
        using (r)
        using (shaper)
        {
            var result = shaper.Shape("impossible", new TextStyle { Size = 40, MaxLines = 1 }, 1f, maxWidth: 1);
            Assert.NotEmpty(result.Lines); // first line ellipsized, but produced
        }
    }

    [Fact]
    public void Mixed_script_splits_into_fallback_runs()
    {
        var (r, shaper) = NewShaper();
        using (r)
        using (shaper)
        {
            const string text = "Shipping 🚀 to 東京";
            var primary = r.Resolve(null, 400);

            // Only assert a split when this machine actually has fallback faces for the exotic runes.
            var hasRocketFallback = primary.GetGlyph(0x1F680) == 0 && r.ResolveFallback(0x1F680, 400) is not null;
            var hasCjkFallback = primary.GetGlyph('東') == 0 && r.ResolveFallback('東', 400) is not null;

            var runs = shaper.SplitTypefaceRuns(text, null, 400);
            Assert.Equal(text, string.Concat(runs.Select(x => x.Text))); // lossless

            if (hasRocketFallback || hasCjkFallback)
                Assert.True(runs.Count >= 2, "expected at least one fallback run for non-Latin glyphs");
        }
    }

    [Fact]
    public void Lines_carry_positive_metrics()
    {
        var (r, shaper) = NewShaper();
        using (r)
        using (shaper)
        {
            var line = shaper.ShapeLine("Metrics", new TextStyle { Size = 50 }, 50);
            Assert.True(line.Ascent > 0);
            Assert.True(line.Descent > 0);
            Assert.True(line.Width > 0);
        }
    }
}
