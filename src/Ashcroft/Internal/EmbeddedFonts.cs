using SkiaSharp;

namespace Ashcroft.Internal;

/// <summary>
/// Typefaces bundled into the assembly (Noto Sans, Noto Color Emoji, Noto Sans JP — all OFL) so
/// default rendering is identical on every machine, including bare CI runners and containers with
/// no system fonts. Each face loads once per process and the instances are shared across all
/// resolvers — they must never be disposed (<see cref="FontResolver.Dispose"/> checks
/// <see cref="Owns"/> before disposing cache entries).
/// </summary>
internal static class EmbeddedFonts
{
    private static readonly Lazy<SKTypeface> SansRegular = Load("NotoSans-Regular.ttf");
    private static readonly Lazy<SKTypeface> SansMedium = Load("NotoSans-Medium.ttf");
    private static readonly Lazy<SKTypeface> SansBold = Load("NotoSans-Bold.ttf");
    private static readonly Lazy<SKTypeface> EmojiFace = Load("NotoColorEmoji.ttf");
    private static readonly Lazy<SKTypeface> JpRegular = Load("NotoSansJP-Regular.otf");
    private static readonly Lazy<SKTypeface> JpBold = Load("NotoSansJP-Bold.otf");

    private static readonly Lazy<SKTypeface>[] All = { SansRegular, SansMedium, SansBold, EmojiFace, JpRegular, JpBold };

    public static SKTypeface Emoji => EmojiFace.Value;

    public static SKTypeface SansForWeight(int weight) =>
        weight < 450 ? SansRegular.Value : weight < 625 ? SansMedium.Value : SansBold.Value;

    public static SKTypeface JpForWeight(int weight) =>
        weight < 600 ? JpRegular.Value : JpBold.Value;

    /// <summary>True when the typeface is one of the shared embedded instances.</summary>
    public static bool Owns(SKTypeface typeface)
    {
        foreach (var lazy in All)
            if (lazy.IsValueCreated && ReferenceEquals(typeface, lazy.Value))
                return true;
        return false;
    }

    private static Lazy<SKTypeface> Load(string file) => new(() =>
    {
        using var stream = typeof(EmbeddedFonts).Assembly.GetManifestResourceStream($"Ashcroft.Fonts.{file}")
            ?? throw new InvalidOperationException($"Embedded font resource 'Ashcroft.Fonts.{file}' is missing from the assembly.");
        using var data = SKData.Create(stream);
        return SKTypeface.FromData(data)
            ?? throw new InvalidOperationException($"Embedded font '{file}' could not be parsed.");
    });
}
