using HarfBuzzSharp;
using SkiaSharp;
using SkiaSharp.HarfBuzz; // ToHarfBuzzBlob

namespace Ashcroft.Internal;

/// <summary>
/// A minimal HarfBuzz shaper for a single typeface. It mirrors SkiaSharp's <c>SKShaper</c> (shape at
/// a fixed 512-unit em, then scale to the target size) with one critical addition: it pushes the
/// typeface's variation design position (e.g. the <c>wght</c> axis of a variable font) onto the
/// HarfBuzz font before shaping. <c>SKShaper</c> reads only the raw font blob, so a variable font
/// instanced via <see cref="SKTypeface.Clone(System.ReadOnlySpan{SKFontVariationPositionCoordinate})"/>
/// would shape at its default weight and only the rasterized outline would change — advances and
/// wrapping would not. Shaping here, measurement, and drawing therefore all agree on the weight.
/// </summary>
/// <remarks>
/// If a future SkiaSharp.HarfBuzz applies a typeface's variations inside <c>SKShaper</c> itself, this
/// whole class can be deleted in favour of it. See <c>CLAUDE.md</c>.
/// </remarks>
internal sealed class HarfBuzzShaper : IDisposable
{
    // SKShaper shapes at this em size, then scales results by (targetSize / FontSizeScale).
    private const int FontSizeScale = 512;

    private readonly Font _font;

    public HarfBuzzShaper(SKTypeface typeface)
    {
        using var blob = typeface.OpenStream(out var index).ToHarfBuzzBlob();
        using var face = new Face(blob, index) { Index = index, UnitsPerEm = typeface.UnitsPerEm };

        _font = new Font(face);
        _font.SetScale(FontSizeScale, FontSizeScale);
        _font.SetFunctionsOpenType();

        // A variable typeface (our embedded default, cloned per weight) carries its axis position
        // here; static faces (emoji, JP, system fonts) report none and shape unchanged.
        var position = typeface.VariationDesignPosition;
        if (position is { Length: > 0 })
        {
            var variations = new Variation[position.Length];
            for (var i = 0; i < position.Length; i++)
                variations[i] = new Variation { Tag = (uint)position[i].Axis, Value = position[i].Value };
            _font.SetVariations(variations);
        }
    }

    /// <summary>Shape <paramref name="text"/> at <paramref name="size"/> into glyph ids and baseline-relative positions.</summary>
    public (ushort[] Glyphs, SKPoint[] Positions, float Width) Shape(string text, float size)
    {
        using var buffer = new HarfBuzzSharp.Buffer();
        buffer.AddUtf8(text);
        buffer.GuessSegmentProperties();
        _font.Shape(buffer);

        var infos = buffer.GlyphInfos;
        var positions = buffer.GlyphPositions;
        var scale = size / FontSizeScale;

        var glyphs = new ushort[infos.Length];
        var points = new SKPoint[infos.Length];
        float x = 0, y = 0;
        for (var i = 0; i < infos.Length; i++)
        {
            glyphs[i] = (ushort)infos[i].Codepoint;
            points[i] = new SKPoint(x + positions[i].XOffset * scale, y - positions[i].YOffset * scale);
            x += positions[i].XAdvance * scale;
            y += positions[i].YAdvance * scale;
        }

        return (glyphs, points, x);
    }

    public void Dispose() => _font.Dispose();
}
