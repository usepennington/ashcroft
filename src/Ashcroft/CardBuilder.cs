using Ashcroft.Internal;
using SkiaSharp;

namespace Ashcroft;

/// <summary>An anchored content group: a stack pinned to one of nine positions.</summary>
internal sealed record AnchoredGroup(Anchor Anchor, StackBuilder Stack);

/// <summary>
/// The fluent card. Configuration is captured eagerly but nothing rasterizes until
/// <see cref="Save(string)"/>, <see cref="ToBytes"/>, or <see cref="ToImage"/> is called, so a
/// builder is cheap to construct and reuse.
/// </summary>
public sealed class CardBuilder
{
    private readonly List<AnchoredGroup> _groups = new();

    internal CardBuilder(SKSizeI size) => Size = size;

    // --- Internal state, consumed by the renderer / tests ---
    internal SKSizeI Size { get; }
    internal BackgroundSource? BackgroundSource { get; private set; }
    internal IReadOnlyList<AnchoredGroup> Groups => _groups;
    internal Theme ThemeValue { get; private set; } = new();
    internal int PaddingValue { get; private set; } = 64;
    internal float? ScrimOpacity { get; private set; }
    internal bool ScrimDisabled { get; private set; }
    internal float ScaleFactor { get; private set; } = 1f;

    /// <summary>A hex color (<c>#rgb</c>/<c>#rrggbb</c>/<c>#aarrggbb</c>) or, if not a color, a file path.</summary>
    public CardBuilder Background(string colorOrPath)
    {
        ArgumentNullException.ThrowIfNull(colorOrPath);
        BackgroundSource = Color.TryParse(colorOrPath, out var color)
            ? new ColorBackground { Color = color }
            : new ImagePathBackground { Path = colorOrPath };
        return this;
    }

    /// <summary>A background image read from a stream (buffered now; decoded at render time).</summary>
    public CardBuilder Background(Stream image)
    {
        ArgumentNullException.ThrowIfNull(image);
        using var ms = new MemoryStream();
        image.CopyTo(ms);
        BackgroundSource = new ImageBytesBackground { Data = ms.ToArray() };
        return this;
    }

    /// <summary>A background image from raw bytes.</summary>
    public CardBuilder Background(ReadOnlyMemory<byte> image)
    {
        BackgroundSource = new ImageBytesBackground { Data = image };
        return this;
    }

    /// <summary>Draw the background yourself over the raw canvas (geometric / generative art).</summary>
    public CardBuilder Background(Action<SKCanvas, SKSizeI> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        BackgroundSource = new LambdaBackground { Draw = draw };
        return this;
    }

    /// <summary>A solid color or gradient via the <see cref="Backgrounds"/> factory.</summary>
    public CardBuilder Background(BackgroundFill fill)
    {
        ArgumentNullException.ThrowIfNull(fill);
        BackgroundSource = new FillBackground { Fill = fill };
        return this;
    }

    /// <summary>Pin a stack of content to an anchor.</summary>
    public CardBuilder At(Anchor anchor, Action<StackBuilder> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var stack = new StackBuilder();
        content(stack);
        _groups.Add(new AnchoredGroup(anchor, stack));
        return this;
    }

    /// <summary>Card-wide font/color/scale.</summary>
    public CardBuilder Theme(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ThemeValue = theme;
        return this;
    }

    /// <summary>Inset from the card edges for every anchor (default 64).</summary>
    public CardBuilder Padding(int pixels)
    {
        PaddingValue = pixels;
        return this;
    }

    /// <summary>Force the legibility scrim opacity (0–1) instead of the auto default (~0.55).</summary>
    public CardBuilder Scrim(float opacity)
    {
        ScrimOpacity = opacity;
        ScrimDisabled = false;
        return this;
    }

    /// <summary>Disable the legibility scrim for this card.</summary>
    public CardBuilder NoScrim()
    {
        ScrimDisabled = true;
        return this;
    }

    /// <summary>Render at N× pixel density (all layout values multiplied). Default 1.</summary>
    public CardBuilder Scale(float factor)
    {
        if (factor <= 0)
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "Scale factor must be positive.");
        ScaleFactor = factor;
        return this;
    }

    /// <summary>Render and write to a file. Format is inferred from the extension.</summary>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var format = CardRenderer.FormatFromExtension(path);
        using var fs = File.Create(path);
        Save(fs, format);
    }

    /// <summary>Render and write to a stream in the given format.</summary>
    public void Save(Stream destination, ImageFormat format, int quality = 90)
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var image = CardRenderer.Render(this);
        using var data = CardRenderer.Encode(image, format, quality);
        data.SaveTo(destination);
    }

    /// <summary>Render to an encoded byte array.</summary>
    public byte[] ToBytes(ImageFormat format = ImageFormat.Png, int quality = 90)
    {
        using var image = CardRenderer.Render(this);
        using var data = CardRenderer.Encode(image, format, quality);
        return data.ToArray();
    }

    /// <summary>Render to an <see cref="SKImage"/> for further Skia work. The caller owns the result.</summary>
    public SKImage ToImage() => CardRenderer.Render(this);
}
