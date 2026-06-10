namespace Ashcroft;

/// <summary>
/// Fully describes how a run of text is drawn. The role elements
/// (<c>Title</c>/<c>Subtitle</c>/<c>Meta</c>) are just pre-tuned <see cref="TextStyle"/> values;
/// use this directly via <c>Text(...)</c> for anything they don't cover.
/// </summary>
public sealed record TextStyle
{
    /// <summary>Font family; <see langword="null"/> falls back to the theme font.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Font size in logical pixels.</summary>
    public float Size { get; init; } = 30;

    /// <summary>Weight 100–900, mapped to <c>SKFontStyleWeight</c>.</summary>
    public int Weight { get; init; } = 400;

    /// <summary>Hex color string (<c>#rgb</c>/<c>#rrggbb</c>/<c>#aarrggbb</c>).</summary>
    public string Color { get; init; } = "#ffffff";

    /// <summary>Line height as a multiple of the font size.</summary>
    public float LineHeight { get; init; } = 1.35f;

    /// <summary>Maximum number of lines before ellipsizing (or shrinking, if enabled).</summary>
    public int MaxLines { get; init; } = 2;

    /// <summary>When true, step the size down (to 70% floor) before ellipsizing.</summary>
    public bool ShrinkToFit { get; init; } = false;

    /// <summary>Extra tracking added between glyph clusters, in logical pixels.</summary>
    public float LetterSpacing { get; init; } = 0;
}
