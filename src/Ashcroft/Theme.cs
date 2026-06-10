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

    /// <summary>Default text color as a hex string.</summary>
    public string TextColor { get; init; } = "#ffffff";

    /// <summary>Multiplies the entire type scale.</summary>
    public float Scale { get; init; } = 1.0f;
}
