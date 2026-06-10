using Ashcroft.Internal;

namespace Ashcroft;

/// <summary>
/// Builds a vertical stack of elements. Created for you inside <c>CardBuilder.At(...)</c> and
/// (for horizontal groups) inside <c>Row(...)</c>. Every method returns the same builder for chaining.
/// </summary>
public sealed class StackBuilder
{
    private readonly List<StackElement> _elements = new();
    private readonly bool _isRow;

    internal StackBuilder(bool isRow = false) => _isRow = isRow;

    // --- Internal state, read by the layout engine / tests ---
    internal IReadOnlyList<StackElement> Elements => _elements;
    internal int GapValue { get; private set; } = 12;
    internal int? MaxWidthValue { get; private set; }
    internal HorizontalAlign? AlignValue { get; private set; }
    internal bool IsRow => _isRow;

    /// <summary>A heading. 64px bold by default; wraps up to 3 lines, then shrinks, then ellipsizes.</summary>
    public StackBuilder Title(string text, string? color = null, float? size = null)
        => AddText(text, Roles.Title(), Roles.TitleOpacity, color, size);

    /// <summary>A one- or two-line description. 30px regular at 85% opacity by default.</summary>
    public StackBuilder Subtitle(string text, string? color = null, float? size = null)
        => AddText(text, Roles.Subtitle(), Roles.SubtitleOpacity, color, size);

    /// <summary>A single line of supporting text (author, date). 22px medium at 65% opacity, ellipsized.</summary>
    public StackBuilder Meta(string text, string? color = null, float? size = null)
        => AddText(text, Roles.Meta(), Roles.MetaOpacity, color, size);

    /// <summary>Fully custom text for anything the three roles don't cover.</summary>
    public StackBuilder Text(string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        // For custom text the style's own Color (incl. its alpha) is authoritative; no role opacity ramp.
        _elements.Add(new TextElement { Text = text, Style = style, ColorOverride = style.Color, Opacity = 1f });
        return this;
    }

    /// <summary>An image, scaled aspect-preserving. <see cref="ImageShape.Circle"/> makes avatars.</summary>
    public StackBuilder Image(string path, int? width = null, int? height = null,
                              ImageShape shape = ImageShape.Rect, float cornerRadius = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _elements.Add(new ImageElement
        {
            Path = path, Width = width, Height = height, Shape = shape, CornerRadius = cornerRadius,
        });
        return this;
    }

    /// <summary>A horizontal group, vertically centered (e.g. avatar + name). Rows cannot nest.</summary>
    public StackBuilder Row(Action<StackBuilder> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (_isRow)
            throw new InvalidOperationException("Row cannot be nested inside another Row (v1).");

        var row = new StackBuilder(isRow: true);
        content(row);
        _elements.Add(new RowElement { Children = row._elements, Gap = row.GapValue });
        return this;
    }

    /// <summary>Extra vertical space beyond the stack's default gap.</summary>
    public StackBuilder Spacer(int pixels)
    {
        _elements.Add(new SpacerElement { Pixels = pixels });
        return this;
    }

    /// <summary>Vertical gap between elements (default 12). In a row, the horizontal gap.</summary>
    public StackBuilder Gap(int pixels)
    {
        GapValue = pixels;
        return this;
    }

    /// <summary>Maximum stack width before text wraps. Defaults to card width minus padding.</summary>
    public StackBuilder MaxWidth(int pixels)
    {
        MaxWidthValue = pixels;
        return this;
    }

    /// <summary>Override the alignment otherwise inherited from the anchor.</summary>
    public StackBuilder Align(HorizontalAlign align)
    {
        AlignValue = align;
        return this;
    }

    private StackBuilder AddText(string text, TextStyle roleStyle, float opacity, string? colorOverride, float? size)
    {
        var style = size is { } s ? roleStyle with { Size = s } : roleStyle;
        _elements.Add(new TextElement { Text = text, Style = style, ColorOverride = colorOverride, Opacity = opacity });
        return this;
    }
}
