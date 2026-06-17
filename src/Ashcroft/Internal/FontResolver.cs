using SkiaSharp;

namespace Ashcroft.Internal;

/// <summary>
/// Resolves typefaces from a <see cref="Theme"/> and per-element overrides. The default face is
/// the embedded Noto Sans (<see cref="EmbeddedFonts"/>), so output is identical on every machine;
/// a requested family that isn't installed silently falls back to it — libraries shouldn't crash
/// a build over a font. The chosen family is reported to <see cref="AshcroftDiagnostics"/> when a
/// fallback happens.
/// </summary>
internal sealed class FontResolver : IDisposable
{
    // The OpenType 'wght' variation axis a variable file font is instanced along.
    private static readonly SKFourByteTag WeightAxis = SKFourByteTag.Parse("wght");

    private readonly SKFontManager _fontManager = SKFontManager.Default;
    private readonly Theme _theme;
    private readonly Dictionary<(string family, int weight), SKTypeface> _cache = new();
    private readonly Dictionary<(int codepoint, int weight), SKTypeface?> _fallbackCache = new();
    private readonly SKTypeface? _fileTypeface;
    // Set when the theme font file is variable along 'wght'; per-weight clones are cut from it.
    private readonly SKFontVariationAxis? _fileWeightAxis;
    private readonly Dictionary<int, SKTypeface> _fileByWeight = new();

    public FontResolver(Theme theme)
    {
        _theme = theme;
        if (!string.IsNullOrEmpty(theme.FontPath))
        {
            _fileTypeface = SKTypeface.FromFile(theme.FontPath);
            if (_fileTypeface is null)
                AshcroftDiagnostics.Report($"Font file '{theme.FontPath}' could not be loaded; falling back to system fonts.");
            else
                _fileWeightAxis = FindWeightAxis(_fileTypeface);
        }
    }

    /// <summary>Resolve the primary typeface for a text run.</summary>
    public SKTypeface Resolve(string? familyOverride, int weight)
    {
        // An explicit theme font file wins unless the element overrode the family by name. A
        // variable file font is instanced at the requested weight; a static one ignores it.
        if (_fileTypeface is not null && familyOverride is null)
            return FileForWeight(weight);

        var requested = familyOverride
                        ?? (_theme.FontFamily.Length > 0 ? _theme.FontFamily : null);

        // Nothing requested: the embedded default, identical on every machine.
        if (requested is null)
            return EmbeddedFonts.SansForWeight(weight);

        var key = (requested, weight);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var style = StyleFor(weight);
        var tf = SKTypeface.FromFamilyName(requested, style.Weight, SKFontStyleWidth.Normal, style.Slant);
        var resolved = tf is not null && tf.FamilyName.Equals(requested, StringComparison.OrdinalIgnoreCase)
            ? tf
            : EmbeddedFonts.SansForWeight(weight);

        if (!ReferenceEquals(resolved, tf))
        {
            tf?.Dispose(); // substituted face we won't use
            AshcroftDiagnostics.Report($"Font family '{requested}' was not found; using '{resolved.FamilyName}' instead.");
        }

        _cache[key] = resolved;
        return resolved;
    }

    /// <summary>Find a typeface that can render <paramref name="codepoint"/> when the primary face can't.</summary>
    public SKTypeface? ResolveFallback(int codepoint, int weight)
    {
        var key = (codepoint, weight);
        if (_fallbackCache.TryGetValue(key, out var cached))
            return cached;

        // Embedded faces first (emoji, then Japanese) so the common cases never depend on the
        // host's font inventory; other scripts still resolve from the system when present.
        SKTypeface? tf;
        if (EmbeddedFonts.Emoji.GetGlyph(codepoint) != 0)
        {
            tf = EmbeddedFonts.Emoji;
        }
        else if (EmbeddedFonts.JpForWeight(weight).GetGlyph(codepoint) != 0)
        {
            tf = EmbeddedFonts.JpForWeight(weight);
        }
        else
        {
            var style = StyleFor(weight);
            tf = _fontManager.MatchCharacter(null, style.Weight, SKFontStyleWidth.Normal, style.Slant, null, codepoint)
                 ?? _fontManager.MatchCharacter(codepoint);
        }

        _fallbackCache[key] = tf;
        return tf;
    }

    /// <summary>
    /// The theme font file at <paramref name="weight"/>. When the file is a variable font with a
    /// 'wght' axis, clone it along that axis (clamped to the axis range) and cache per weight, just
    /// like <see cref="EmbeddedFonts.SansForWeight"/>; the clone carries its variation position so
    /// <see cref="HarfBuzzShaper"/> shapes — not merely rasterizes — at that weight. A static file
    /// has no axis to vary, so its single face serves every weight.
    /// </summary>
    private SKTypeface FileForWeight(int weight)
    {
        if (_fileWeightAxis is not { } axis)
            return _fileTypeface!;

        var clamped = Math.Clamp(weight, (int)axis.Min, (int)axis.Max);
        if (_fileByWeight.TryGetValue(clamped, out var cached))
            return cached;

        var clone = _fileTypeface!.Clone([new SKFontVariationPositionCoordinate { Axis = WeightAxis, Value = clamped }]);
        _fileByWeight[clamped] = clone;
        return clone;
    }

    private static SKFontVariationAxis? FindWeightAxis(SKTypeface typeface)
    {
        foreach (var axis in typeface.VariationDesignParameters ?? [])
            if (axis.Tag == WeightAxis)
                return axis;
        return null;
    }

    private static (SKFontStyleWeight Weight, SKFontStyleSlant Slant) StyleFor(int weight)
        => ((SKFontStyleWeight)Math.Clamp(weight, 100, 900), SKFontStyleSlant.Upright);

    public void Dispose()
    {
        // Embedded faces are process-wide singletons shared by every resolver — never dispose them.
        foreach (var tf in _cache.Values)
            if (!EmbeddedFonts.Owns(tf))
                tf.Dispose();
        foreach (var tf in _fallbackCache.Values)
            if (tf is not null && !EmbeddedFonts.Owns(tf))
                tf.Dispose();
        foreach (var tf in _fileByWeight.Values)
            tf.Dispose();
        _fileTypeface?.Dispose();
        _cache.Clear();
        _fallbackCache.Clear();
        _fileByWeight.Clear();
    }
}
