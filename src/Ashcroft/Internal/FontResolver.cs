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
    private readonly SKFontManager _fontManager = SKFontManager.Default;
    private readonly Theme _theme;
    private readonly Dictionary<(string family, int weight), SKTypeface> _cache = new();
    private readonly Dictionary<(int codepoint, int weight), SKTypeface?> _fallbackCache = new();
    private readonly SKTypeface? _fileTypeface;

    public FontResolver(Theme theme)
    {
        _theme = theme;
        if (!string.IsNullOrEmpty(theme.FontPath))
        {
            _fileTypeface = SKTypeface.FromFile(theme.FontPath);
            if (_fileTypeface is null)
                AshcroftDiagnostics.Report($"Font file '{theme.FontPath}' could not be loaded; falling back to system fonts.");
        }
    }

    /// <summary>Resolve the primary typeface for a text run.</summary>
    public SKTypeface Resolve(string? familyOverride, int weight)
    {
        // An explicit theme font file wins unless the element overrode the family by name.
        if (_fileTypeface is not null && familyOverride is null)
            return _fileTypeface;

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
        _fileTypeface?.Dispose();
        _cache.Clear();
        _fallbackCache.Clear();
    }
}
