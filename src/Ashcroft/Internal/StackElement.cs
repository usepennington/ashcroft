namespace Ashcroft.Internal;

/// <summary>Base type for everything that can live in a stack. Internal — users see only the fluent API.</summary>
internal abstract class StackElement;

/// <summary>
/// A run of text. <see cref="Style"/> carries size/weight/wrapping; <see cref="ColorOverride"/> is the
/// user's explicit color (null means "use the theme color"), and <see cref="Opacity"/> is the role
/// opacity ramp (Title 1.0, Subtitle 0.85, Meta 0.65) applied on top of whichever color wins.
/// </summary>
internal sealed class TextElement : StackElement
{
    public required string Text { get; init; }
    public required TextStyle Style { get; init; }
    public string? ColorOverride { get; init; }
    public float Opacity { get; init; } = 1f;
}

/// <summary>An image element, scaled aspect-preserving and optionally clipped to a shape.</summary>
internal sealed class ImageElement : StackElement
{
    public required string Path { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public ImageShape Shape { get; init; } = ImageShape.Rect;
    public float CornerRadius { get; init; }
}

/// <summary>Extra vertical gap beyond the stack default.</summary>
internal sealed class SpacerElement : StackElement
{
    public int Pixels { get; init; }
}

/// <summary>A horizontal group of elements, vertically centered. Cannot contain another row.</summary>
internal sealed class RowElement : StackElement
{
    public required IReadOnlyList<StackElement> Children { get; init; }
    public int Gap { get; init; } = 12;
}

/// <summary>The pre-tuned type scale and opacity ramp behind Title/Subtitle/Meta.</summary>
internal static class Roles
{
    public const float TitleOpacity = 1.0f;
    public const float SubtitleOpacity = 0.85f;
    public const float MetaOpacity = 0.65f;

    public static TextStyle Title() => new()
    {
        Size = 64, Weight = 700, LineHeight = 1.15f, MaxLines = 3, ShrinkToFit = true,
    };

    public static TextStyle Subtitle() => new()
    {
        Size = 30, Weight = 400, LineHeight = 1.35f, MaxLines = 2, ShrinkToFit = false,
    };

    public static TextStyle Meta() => new()
    {
        Size = 22, Weight = 500, LineHeight = 1.2f, MaxLines = 1, ShrinkToFit = false,
    };
}
