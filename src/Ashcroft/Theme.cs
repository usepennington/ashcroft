namespace Ashcroft;

/// <summary>
/// Card-wide styling. A value you construct, not a file format. Per-element overrides
/// take precedence over these defaults.
/// </summary>
public sealed record Theme
{
    /// <summary>Primary font family. <c>""</c> uses the platform sans-serif fallback chain.</summary>
    public string FontFamily { get; init; } = "";

    /// <summary>Optional path to a font file, loaded directly instead of resolving a family.</summary>
    public string? FontPath { get; init; }

    /// <summary>
    /// Font files to register under the family name each one reports, so a per-element or theme
    /// <see cref="FontFamily"/> resolves to the bundled file <em>before</em> any system lookup —
    /// the deterministic way to mix several bundled faces in one card. <see cref="FontPath"/>, by
    /// contrast, sets a single card-wide face.
    /// </summary>
    public IReadOnlyList<string> FontFiles { get; init; } = [];

    /// <summary>Default text color as a hex string.</summary>
    public string TextColor { get; init; } = "#ffffff";

    /// <summary>Multiplies the entire type scale.</summary>
    public float Scale { get; init; } = 1.0f;
}
