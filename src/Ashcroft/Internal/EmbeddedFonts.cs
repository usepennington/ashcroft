using System.Collections.Concurrent;
using SkiaSharp;

namespace Ashcroft.Internal;

/// <summary>
/// Typefaces bundled into the assembly (variable Noto Sans, Noto Color Emoji, Noto Sans JP — all
/// OFL) so default rendering is identical on every machine, including bare CI runners and
/// containers with no system fonts. Each face loads once per process and the instances are shared
/// across all resolvers — they must never be disposed (<see cref="FontResolver.Dispose"/> checks
/// <see cref="Owns"/> before disposing cache entries).
/// </summary>
internal static class EmbeddedFonts
{
    // The OpenType 'wght' variation axis the default face is instanced along.
    private static readonly SKFourByteTag WeightAxis = SKFourByteTag.Parse("wght");

    // The variable Noto Sans master (wght 100–900); the per-weight faces are cloned from it.
    private static readonly Lazy<SKTypeface> SansVariable = LoadFace("NotoSans-VariableFont_wght.ttf");
    private static readonly Lazy<SKTypeface> EmojiFace = LoadFace("NotoColorEmoji.ttf");
    private static readonly Lazy<SKTypeface> JpRegular = LoadFace("NotoSansJP-Regular.otf");
    private static readonly Lazy<SKTypeface> JpBold = LoadFace("NotoSansJP-Bold.otf");

    private static readonly Lazy<SKTypeface>[] FixedFaces = { SansVariable, EmojiFace, JpRegular, JpBold };

    // One Noto Sans instance per requested weight, cut from the variable master's 'wght' axis.
    private static readonly ConcurrentDictionary<int, SKTypeface> SansByWeight = new();

    public static SKTypeface Emoji => EmojiFace.Value;

    /// <summary>The embedded Noto Sans instanced at <paramref name="weight"/> (100–900) along its variable 'wght' axis.</summary>
    public static SKTypeface SansForWeight(int weight) =>
        SansByWeight.GetOrAdd(Math.Clamp(weight, 100, 900), static w =>
            SansVariable.Value.Clone([new SKFontVariationPositionCoordinate { Axis = WeightAxis, Value = w }]));

    public static SKTypeface JpForWeight(int weight) =>
        weight < 600 ? JpRegular.Value : JpBold.Value;

    /// <summary>True when the typeface is one of the shared embedded instances.</summary>
    public static bool Owns(SKTypeface typeface)
    {
        foreach (var lazy in FixedFaces)
            if (lazy.IsValueCreated && ReferenceEquals(typeface, lazy.Value))
                return true;
        foreach (var face in SansByWeight.Values)
            if (ReferenceEquals(typeface, face))
                return true;
        return false;
    }

    private static Lazy<SKTypeface> LoadFace(string file) => new(() =>
    {
        using var stream = typeof(EmbeddedFonts).Assembly.GetManifestResourceStream($"Ashcroft.Fonts.{file}")
            ?? throw new InvalidOperationException($"Embedded font resource 'Ashcroft.Fonts.{file}' is missing from the assembly.");
        using var data = SKData.Create(stream);
        return SKTypeface.FromData(data)
            ?? throw new InvalidOperationException($"Embedded font '{file}' could not be parsed.");
    });
}
