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
    public void Balanced_wrap_evens_out_a_greedy_orphan()
    {
        var (r, shaper) = NewShaper();
        using (r)
        using (shaper)
        {
            var style = new TextStyle { Size = 24, MaxLines = 9 };
            float Width(int words) =>
                shaper.ShapeLine(string.Join(' ', Enumerable.Repeat("ashcroft", words)), style, 24).Width;

            // A width that fits exactly four identical words but not five.
            var maxWidth = (Width(4) + Width(5)) / 2f;

            // Six words: greedy packs 4 + 2 (a lopsided last line); balance evens it to 3 + 3.
            var result = shaper.Shape(string.Join(' ', Enumerable.Repeat("ashcroft", 6)), style, 1f, maxWidth);

            Assert.Equal(2, result.Lines.Count);
            Assert.Equal(3, result.Lines[0].Text.Split(' ').Length);
            Assert.Equal(3, result.Lines[1].Text.Split(' ').Length);
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
    public void Default_typeface_is_embedded_noto_sans_on_every_platform()
    {
        var (r, shaper) = NewShaper();
        using (r)
        using (shaper)
        {
            // Medium reports "Noto Sans Medium" (non-RIBBI naming), hence StartsWith.
            Assert.StartsWith("Noto Sans", r.Resolve(null, 400).FamilyName);
            Assert.StartsWith("Noto Sans", r.Resolve(null, 500).FamilyName);
            Assert.StartsWith("Noto Sans", r.Resolve(null, 700).FamilyName);
        }
    }

    [Fact]
    public void Theme_FontFiles_registers_face_by_family_name_before_system_lookup()
    {
        // Extract the embedded Noto Sans to a real file so we can register it without depending on
        // any system-installed or doc-site font. It reports family "Noto Sans".
        var path = Path.Combine(Path.GetTempPath(), $"ashcroft-fontfiles-{Guid.NewGuid():N}.ttf");
        using (var src = typeof(Theme).Assembly.GetManifestResourceStream("Ashcroft.Fonts.NotoSans-VariableFont_wght.ttf")!)
        using (var dst = File.Create(path))
            src.CopyTo(dst);
        try
        {
            using var r = new FontResolver(new Theme { FontFiles = [path] });

            var byName = r.Resolve("Noto Sans", 400);
            Assert.Equal("Noto Sans", byName.FamilyName);
            // The registered file face short-circuits before the embedded singleton (and any system font).
            Assert.False(EmbeddedFonts.Owns(byName));

            // No override still yields the embedded default, untouched by the registration.
            Assert.True(EmbeddedFonts.Owns(r.Resolve(null, 400)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Embedded_default_varies_glyph_weight_across_the_range()
    {
        var (r, shaper) = NewShaper();
        using (r)
        using (shaper)
        {
            float Width(int weight) =>
                shaper.ShapeLine("Weight", new TextStyle { Size = 80, Weight = weight }, 80).Width;

            // Heavier weights shape wider on the embedded variable Noto Sans. Before the fix the
            // default font had only a few static buckets, so most weights collapsed to one face
            // and these advances were identical.
            Assert.True(Width(900) > Width(100), "weight 900 should shape wider than weight 100");
            Assert.True(Width(700) > Width(300), "weight 700 should shape wider than weight 300");

            // Semi-light (300) must resolve to its own instance, distinct from regular (400) —
            // the reported bug was that every weight rendered the same.
            Assert.False(ReferenceEquals(r.Resolve(null, 300), r.Resolve(null, 400)));
        }
    }

    [Fact]
    public void Emoji_and_japanese_fall_back_to_embedded_faces()
    {
        var (r, shaper) = NewShaper();
        using (r)
        using (shaper)
        {
            var rocket = r.ResolveFallback(0x1F680, 400);
            Assert.NotNull(rocket);
            Assert.True(rocket!.GetGlyph(0x1F680) != 0, "embedded emoji face should cover U+1F680");

            var kanji = r.ResolveFallback('東', 700);
            Assert.NotNull(kanji);
            Assert.True(kanji!.GetGlyph('東') != 0, "embedded JP face should cover 東");
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
