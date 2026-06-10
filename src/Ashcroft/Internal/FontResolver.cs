using SkiaSharp;

namespace Ashcroft.Internal;

/// <summary>
/// Resolves typefaces from a <see cref="Theme"/> and per-element overrides. A missing family
/// silently falls back down the chain (requested → Segoe UI → Helvetica Neue → platform sans);
/// libraries shouldn't crash a build over a font. The chosen family is reported to
/// <see cref="AshcroftDiagnostics"/> when a fallback happens.
/// </summary>
internal sealed class FontResolver : IDisposable
{
    private static readonly string[] FallbackChain = { "Segoe UI", "Helvetica Neue" };

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

        var key = (requested ?? "", weight);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var style = StyleFor(weight);
        SKTypeface? resolved = null;

        // Walk the explicit chain, accepting only an exact family match.
        foreach (var family in Candidates(requested))
        {
            var tf = SKTypeface.FromFamilyName(family, style.Weight, SKFontStyleWidth.Normal, style.Slant);
            if (tf is not null && tf.FamilyName.Equals(family, StringComparison.OrdinalIgnoreCase))
            {
                resolved = tf;
                break;
            }
        }

        // Nothing matched exactly — take the platform default sans.
        resolved ??= SKTypeface.FromFamilyName(null, style.Weight, SKFontStyleWidth.Normal, style.Slant)
                     ?? SKTypeface.Default;

        if (requested is not null && !resolved.FamilyName.Equals(requested, StringComparison.OrdinalIgnoreCase))
            AshcroftDiagnostics.Report($"Font family '{requested}' was not found; using '{resolved.FamilyName}' instead.");

        _cache[key] = resolved;
        return resolved;
    }

    /// <summary>Find a typeface that can render <paramref name="codepoint"/> when the primary face can't.</summary>
    public SKTypeface? ResolveFallback(int codepoint, int weight)
    {
        var key = (codepoint, weight);
        if (_fallbackCache.TryGetValue(key, out var cached))
            return cached;

        var style = StyleFor(weight);
        var tf = _fontManager.MatchCharacter(null, style.Weight, SKFontStyleWidth.Normal, style.Slant, null, codepoint)
                 ?? _fontManager.MatchCharacter(codepoint);

        _fallbackCache[key] = tf;
        return tf;
    }

    private IEnumerable<string> Candidates(string? requested)
    {
        if (!string.IsNullOrEmpty(requested))
            yield return requested;
        foreach (var f in FallbackChain)
            yield return f;
    }

    private static (SKFontStyleWeight Weight, SKFontStyleSlant Slant) StyleFor(int weight)
        => ((SKFontStyleWeight)Math.Clamp(weight, 100, 900), SKFontStyleSlant.Upright);

    public void Dispose()
    {
        foreach (var tf in _cache.Values)
            tf.Dispose();
        foreach (var tf in _fallbackCache.Values)
            tf?.Dispose();
        _fileTypeface?.Dispose();
        _cache.Clear();
        _fallbackCache.Clear();
    }
}
