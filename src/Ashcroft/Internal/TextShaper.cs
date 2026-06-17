using System.Text;
using SkiaSharp;

namespace Ashcroft.Internal;

/// <summary>A shaped run of text in a single typeface: glyph ids and their positions, baseline-relative.</summary>
internal sealed record ShapedRun(SKTypeface Typeface, ushort[] Glyphs, SKPoint[] Positions, float Width);

/// <summary>One visual line. <see cref="Ascent"/>/<see cref="Descent"/> are the max font metrics across its runs.</summary>
internal sealed record ShapedLine(string Text, IReadOnlyList<ShapedRun> Runs, float Width, float Ascent, float Descent);

/// <summary>
/// The result of shaping + wrapping a paragraph. <see cref="Size"/> is the size actually used
/// (after shrink-to-fit), <see cref="LineHeightPx"/> is the per-line box height, and
/// <see cref="Ellipsized"/> indicates the last line was truncated.
/// </summary>
internal sealed record ShapedText(IReadOnlyList<ShapedLine> Lines, float Size, float LineHeightPx, bool Ellipsized)
{
    public float Width => Lines.Count == 0 ? 0 : Lines.Max(l => l.Width);
    public float Height => Lines.Count * LineHeightPx;
}

/// <summary>
/// HarfBuzz-backed shaping with honest (shaped-advance) measurement, balanced word wrap on cluster
/// boundaries, shrink-to-fit, ellipsis, and per-typeface fallback runs. Measurement and drawing
/// share the same shaped glyphs, so what was measured is exactly what gets drawn.
/// </summary>
internal sealed class TextShaper : IDisposable
{
    private const string Ellipsis = "…";
    private const float ShrinkStep = 0.05f; // 5% steps down to the 70% floor

    private readonly FontResolver _resolver;
    private readonly Dictionary<SKTypeface, HarfBuzzShaper> _shapers = new();

    public TextShaper(FontResolver resolver) => _resolver = resolver;

    /// <summary>Shape and wrap <paramref name="text"/> within <paramref name="maxWidth"/> using <paramref name="style"/>.</summary>
    public ShapedText Shape(string text, TextStyle style, float themeScale, float maxWidth)
    {
        text ??= string.Empty;
        var baseSize = MathF.Max(1f, style.Size * themeScale);
        var maxLines = Math.Max(1, style.MaxLines);
        var floor = style.ShrinkToFit ? baseSize * 0.70f : baseSize;

        foreach (var size in SizeSteps(baseSize, floor))
        {
            var lines = Wrap(text, style, size, maxWidth);
            if (lines.Count <= maxLines)
                return new ShapedText(lines, size, size * style.LineHeight, Ellipsized: false);
        }

        // Still too tall at the smallest size: keep MaxLines and ellipsize the last.
        var all = Wrap(text, style, floor, maxWidth);
        var kept = all.Take(maxLines).ToList();
        if (kept.Count > 0)
        {
            var lastIndex = kept.Count - 1;
            var remainder = string.Join(" ", all.Skip(lastIndex).Select(l => l.Text));
            kept[lastIndex] = Ellipsize(remainder, style, floor, maxWidth);
        }

        return new ShapedText(kept, floor, floor * style.LineHeight, Ellipsized: true);
    }

    private static IEnumerable<float> SizeSteps(float baseSize, float floor)
    {
        if (floor >= baseSize)
        {
            yield return baseSize;
            yield break;
        }

        for (var f = 1.0f; f >= 0.70f - 1e-4f; f -= ShrinkStep)
        {
            var size = baseSize * f;
            yield return size < floor ? floor : size;
            if (size <= floor)
                yield break;
        }
    }

    private List<ShapedLine> Wrap(string text, TextStyle style, float size, float maxWidth)
    {
        var lines = new List<ShapedLine>();
        foreach (var paragraph in text.Split('\n'))
            WrapParagraph(paragraph, style, size, maxWidth, lines);

        if (lines.Count == 0)
            lines.Add(ShapeLine(string.Empty, style, size));
        return lines;
    }

    private void WrapParagraph(string paragraph, TextStyle style, float size, float maxWidth, List<ShapedLine> lines)
    {
        var words = paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            lines.Add(ShapeLine(string.Empty, style, size));
            return;
        }

        // Greedy packing establishes the minimum feasible line count — the vertical budget the
        // shrink-to-fit / MaxLines machinery above is built around. If a single word is wider than
        // the line it must hard-break, which only greedy does, so use its result verbatim; otherwise
        // re-break across that same number of lines for balance.
        var greedy = GreedyWrap(words, style, size, maxWidth, out var hadOverflow);
        var chosen = hadOverflow || greedy.Count <= 1
            ? greedy
            : BalanceLines(words, style, size, maxWidth, greedy.Count);

        foreach (var line in chosen)
            lines.Add(ShapeLine(line, style, size));
    }

    /// <summary>
    /// Classic greedy word wrap: pack each line as full as it goes, hard-breaking any single word
    /// wider than the line. Returns the line strings and reports whether a hard-break happened.
    /// </summary>
    private List<string> GreedyWrap(string[] words, TextStyle style, float size, float maxWidth, out bool hadOverflow)
    {
        hadOverflow = false;
        var lines = new List<string>();
        string? current = null;
        foreach (var word in words)
        {
            var candidate = current is null ? word : current + " " + word;
            if (Measure(candidate, style, size) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current is not null)
            {
                lines.Add(current);
                current = null;
            }

            if (Measure(word, style, size) <= maxWidth)
            {
                current = word;
            }
            else
            {
                // A single word wider than the line: hard-break it on rune boundaries.
                hadOverflow = true;
                var pieces = BreakWord(word, style, size, maxWidth);
                for (var i = 0; i < pieces.Count - 1; i++)
                    lines.Add(pieces[i]);
                current = pieces[^1];
            }
        }

        if (current is not null)
            lines.Add(current);
        return lines;
    }

    /// <summary>
    /// Re-break <paramref name="words"/> into exactly <paramref name="lineCount"/> lines that minimise
    /// total raggedness — the score-based "balance" of CSS <c>text-wrap</c>. Each line is penalised by
    /// the square of its shortfall from the full width, so slack spreads evenly and the lines come out
    /// near-equal (which also kills one-word orphans). Constraining to greedy's own line count keeps
    /// the vertical footprint — and therefore shrink-to-fit and MaxLines — untouched; only the
    /// horizontal break points move. A Knuth–Plass dynamic program; <c>n</c> is tiny for card text.
    /// </summary>
    private List<string> BalanceLines(string[] words, TextStyle style, float size, float maxWidth, int lineCount)
    {
        var n = words.Length;
        var target = maxWidth; // balance penalises every line's shortfall from the full width
        var width = new float?[n, n + 1]; // memoised shaped width of words[i..j)

        float LineWidth(int i, int j)
        {
            if (width[i, j] is { } cached)
                return cached;
            var measured = Measure(string.Join(' ', words[i..j]), style, size);
            width[i, j] = measured;
            return measured;
        }

        // f[k, i] = least total cost to set words[i..n) in exactly k lines; back[k, i] = the chosen
        // first break j. f[lineCount, 0] is finite because greedy is itself a feasible witness.
        const float inf = float.PositiveInfinity;
        var f = new float[lineCount + 1, n + 1];
        var back = new int[lineCount + 1, n + 1];
        for (var i = 0; i <= n; i++)
            f[0, i] = i == n ? 0f : inf;

        for (var k = 1; k <= lineCount; k++)
        {
            f[k, n] = inf; // no words left can't fill a line
            for (var i = n - 1; i >= 0; i--)
            {
                var best = inf;
                var bestJ = i + 1;
                for (var j = i + 1; j <= n; j++)
                {
                    var w = LineWidth(i, j);
                    if (w > maxWidth)
                        break; // appending more words only widens the line, and stays infeasible
                    var rest = f[k - 1, j];
                    if (float.IsPositiveInfinity(rest))
                        continue;
                    var d = target - w;
                    var total = d * d + rest;
                    if (total < best)
                    {
                        best = total;
                        bestJ = j;
                    }
                }
                f[k, i] = best;
                back[k, i] = bestJ;
            }
        }

        var result = new List<string>(lineCount);
        var start = 0;
        for (var k = lineCount; k >= 1; k--)
        {
            var end = back[k, start];
            result.Add(string.Join(' ', words[start..end]));
            start = end;
        }
        return result;
    }

    private List<string> BreakWord(string word, TextStyle style, float size, float maxWidth)
    {
        var pieces = new List<string>();
        var current = new StringBuilder();
        foreach (var rune in word.EnumerateRunes())
        {
            var trial = current.ToString() + rune;
            if (current.Length == 0 || Measure(trial, style, size) <= maxWidth)
            {
                current.Append(rune);
            }
            else
            {
                pieces.Add(current.ToString());
                current.Clear();
                current.Append(rune);
            }
        }
        pieces.Add(current.ToString());
        return pieces;
    }

    private ShapedLine Ellipsize(string text, TextStyle style, float size, float maxWidth)
    {
        // Trim runes off the end until the text + ellipsis fits. Never throws on pathological widths.
        var runes = text.EnumerateRunes().ToList();
        for (var count = runes.Count; count >= 0; count--)
        {
            var head = string.Concat(runes.Take(count).Select(r => r.ToString())).TrimEnd();
            var candidate = head + Ellipsis;
            var line = ShapeLine(candidate, style, size);
            if (count == 0 || line.Width <= maxWidth)
                return line;
        }
        return ShapeLine(Ellipsis, style, size);
    }

    private float Measure(string text, TextStyle style, float size) => ShapeLine(text, style, size).Width;

    /// <summary>Shape a single line into per-typeface runs with absolute, baseline-relative glyph positions.</summary>
    internal ShapedLine ShapeLine(string text, TextStyle style, float size)
    {
        var runs = new List<ShapedRun>();
        float penX = 0, ascent = 0, descent = 0;
        var globalGlyph = 0;

        foreach (var (segment, face) in SplitTypefaceRuns(text, style.FontFamily, style.Weight))
        {
            using var font = new SKFont(face, size); // for vertical metrics; shaping is HarfBuzz's job
            var (glyphs, shaped, runWidth) = GetShaper(face).Shape(segment, size);

            var metrics = font.Metrics;
            ascent = MathF.Max(ascent, -metrics.Ascent);
            descent = MathF.Max(descent, metrics.Descent);

            var positions = new SKPoint[shaped.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                positions[i] = new SKPoint(shaped[i].X + penX + style.LetterSpacing * globalGlyph, shaped[i].Y);
                globalGlyph++;
            }

            runs.Add(new ShapedRun(face, glyphs, positions, runWidth));
            penX += runWidth;
        }

        var width = penX + style.LetterSpacing * Math.Max(0, globalGlyph - 1);
        if (ascent == 0 && descent == 0)
        {
            // Empty line: still reserve metrics so blank lines have stable height.
            using var font = new SKFont(_resolver.Resolve(style.FontFamily, style.Weight), size);
            ascent = -font.Metrics.Ascent;
            descent = font.Metrics.Descent;
        }

        return new ShapedLine(text, runs, width, ascent, descent);
    }

    /// <summary>Split text into maximal runs sharing one typeface, falling back per character as needed.</summary>
    internal List<(string Text, SKTypeface Face)> SplitTypefaceRuns(string text, string? family, int weight)
    {
        var primary = _resolver.Resolve(family, weight);
        var runs = new List<(string, SKTypeface)>();
        if (text.Length == 0)
        {
            runs.Add((string.Empty, primary));
            return runs;
        }

        var sb = new StringBuilder();
        var current = primary;
        foreach (var rune in text.EnumerateRunes())
        {
            // Whitespace always shapes with the primary face — a fallback face's space advance can
            // be wildly different (emoji fonts give a space the width of an emoji). Supported runes
            // use the primary; the rest fall back.
            SKTypeface want;
            if (Rune.IsWhiteSpace(rune))
                want = primary;
            else if (primary.GetGlyph(rune.Value) != 0)
                want = primary;
            else
                want = _resolver.ResolveFallback(rune.Value, weight) ?? primary;

            if (sb.Length == 0)
            {
                current = want;
            }
            else if (!SameFace(want, current))
            {
                runs.Add((sb.ToString(), current));
                sb.Clear();
                current = want;
            }

            sb.Append(rune.ToString());
        }

        runs.Add((sb.ToString(), current));
        return runs;
    }

    private static bool SameFace(SKTypeface a, SKTypeface b)
        => ReferenceEquals(a, b) || a.FamilyName == b.FamilyName;

    private HarfBuzzShaper GetShaper(SKTypeface face)
    {
        if (!_shapers.TryGetValue(face, out var shaper))
        {
            shaper = new HarfBuzzShaper(face);
            _shapers[face] = shaper;
        }
        return shaper;
    }

    public void Dispose()
    {
        foreach (var s in _shapers.Values)
            s.Dispose();
        _shapers.Clear();
    }
}
