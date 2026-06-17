using SkiaSharp;

namespace Ashcroft.Internal;

/// <summary>
/// Resolves typefaces from a <see cref="Theme"/> and per-element overrides. The default face is
/// the embedded Noto Sans (<see cref="EmbeddedFonts"/>), so output is identical on every machine;
/// a requested family that isn't installed (and isn't a registered file) silently falls back to it —
/// libraries shouldn't crash a build over a font. The chosen family is reported to
/// <see cref="AshcroftDiagnostics"/> when a fallback happens.
/// </summary>
internal sealed class FontResolver : IDisposable
{
    private readonly SKFontManager _fontManager = SKFontManager.Default;
    private readonly Theme _theme;
    private readonly Dictionary<(string family, int weight), SKTypeface> _cache = new();
    private readonly Dictionary<(int codepoint, int weight), SKTypeface?> _fallbackCache = new();

    // A single card-wide face from Theme.FontPath (wins for the default, unless an element overrides the family).
    private readonly FileFace? _themeFile;
    // Theme.FontFiles registered by the family name each file reports; resolved before any system lookup.
    private readonly Dictionary<string, FileFace> _registered = new(StringComparer.OrdinalIgnoreCase);

    public FontResolver(Theme theme)
    {
        _theme = theme;

        if (!string.IsNullOrEmpty(theme.FontPath))
        {
            var tf = SKTypeface.FromFile(theme.FontPath);
            if (tf is null)
                AshcroftDiagnostics.Report($"Font file '{theme.FontPath}' could not be loaded; falling back to system fonts.");
            else
                _themeFile = new FileFace(tf);
        }

        foreach (var path in theme.FontFiles)
        {
            if (string.IsNullOrEmpty(path))
                continue;
            var tf = SKTypeface.FromFile(path);
            if (tf is null)
            {
                AshcroftDiagnostics.Report($"Font file '{path}' could not be loaded; it was not registered.");
                continue;
            }
            // Last registration of a family name wins; dispose the face it shadows.
            if (_registered.Remove(tf.FamilyName, out var shadowed))
                shadowed.Dispose();
            _registered[tf.FamilyName] = new FileFace(tf);
        }
    }

    /// <summary>Resolve the primary typeface for a text run.</summary>
    public SKTypeface Resolve(string? familyOverride, int weight)
    {
        // An explicit theme font file wins unless the element overrode the family by name. A
        // variable file font is instanced at the requested weight; a static one ignores it.
        if (_themeFile is not null && familyOverride is null)
            return _themeFile.ForWeight(weight);

        var requested = familyOverride
                        ?? (_theme.FontFamily.Length > 0 ? _theme.FontFamily : null);

        // Nothing requested: the embedded default, identical on every machine.
        if (requested is null)
            return EmbeddedFonts.SansForWeight(weight);

        // A bundled file registered under this name resolves deterministically, before any system font.
        if (_registered.TryGetValue(requested, out var registered))
            return registered.ForWeight(weight);

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
        _themeFile?.Dispose();
        foreach (var face in _registered.Values)
            face.Dispose();
        _cache.Clear();
        _fallbackCache.Clear();
        _registered.Clear();
    }

    /// <summary>
    /// A typeface loaded from a file. When the file is a variable font with a 'wght' axis, faces are
    /// cloned along that axis (clamped to its range) and cached per weight, just like
    /// <see cref="EmbeddedFonts.SansForWeight"/>; the clone carries its variation position so
    /// <see cref="HarfBuzzShaper"/> shapes — not merely rasterizes — at that weight. A static file has
    /// no axis to vary, so its single face serves every weight. Owns its faces and disposes them.
    /// </summary>
    private sealed class FileFace : IDisposable
    {
        // The OpenType 'wght' variation axis a variable file font is instanced along.
        private static readonly SKFourByteTag WeightAxis = SKFourByteTag.Parse("wght");

        private readonly SKTypeface _base;
        private readonly SKFontVariationAxis? _weightAxis;
        private readonly Dictionary<int, SKTypeface> _byWeight = new();

        public FileFace(SKTypeface baseTypeface)
        {
            _base = baseTypeface;
            _weightAxis = FindWeightAxis(baseTypeface);
        }

        public SKTypeface ForWeight(int weight)
        {
            if (_weightAxis is not { } axis)
                return _base;

            var clamped = Math.Clamp(weight, (int)axis.Min, (int)axis.Max);
            if (_byWeight.TryGetValue(clamped, out var cached))
                return cached;

            var clone = _base.Clone([new SKFontVariationPositionCoordinate { Axis = WeightAxis, Value = clamped }]);
            _byWeight[clamped] = clone;
            return clone;
        }

        private static SKFontVariationAxis? FindWeightAxis(SKTypeface typeface)
        {
            foreach (var axis in typeface.VariationDesignParameters ?? [])
                if (axis.Tag == WeightAxis)
                    return axis;
            return null;
        }

        public void Dispose()
        {
            foreach (var tf in _byWeight.Values)
                tf.Dispose();
            _byWeight.Clear();
            _base.Dispose();
        }
    }
}
