using SkiaSharp;

namespace Ashcroft.Internal;

// --- Positioned, absolute-coordinate output of the layout pass (logical units; scale applied at draw). ---

internal abstract record PlacedElement;

/// <summary>Text positioned within the block [<see cref="BlockX"/>, BlockX+<see cref="BlockWidth"/>]; each line aligns inside it.</summary>
internal sealed record PlacedText(ShapedText Shaped, SKColor Color, HorizontalAlign Align, float BlockX, float BlockTop, float BlockWidth) : PlacedElement;

internal sealed record PlacedImage(ImageElement Source, SKRect Dest) : PlacedElement;

internal sealed record PlacedRow(IReadOnlyList<PlacedElement> Children) : PlacedElement;

/// <summary>One anchored stack, fully placed. <see cref="Bounds"/> is the measured stack rectangle (used by the scrim).</summary>
internal sealed record PlacedGroup(Anchor Anchor, IReadOnlyList<PlacedElement> Elements, SKRect Bounds, bool HasText);

internal sealed record LaidOutCard(IReadOnlyList<PlacedGroup> Groups);

/// <summary>
/// Measures each stack (shaping text, scaling images, laying out rows) and positions it against its
/// anchor inside the padded content box. No constraint solving and no collision handling — overlap is
/// the user's choice. Works entirely in logical units; the renderer applies the pixel-density scale.
/// </summary>
internal sealed class LayoutEngine
{
    private readonly TextShaper _shaper;
    private readonly ImageLoader _images;
    private readonly Theme _theme;
    private readonly SKSizeI _size;
    private readonly int _padding;

    public LayoutEngine(TextShaper shaper, ImageLoader images, Theme theme, SKSizeI size, int padding)
    {
        _shaper = shaper;
        _images = images;
        _theme = theme;
        _size = size;
        _padding = padding;
    }

    public LaidOutCard Layout(IReadOnlyList<AnchoredGroup> groups)
    {
        var placed = new List<PlacedGroup>(groups.Count);
        foreach (var group in groups)
            placed.Add(LayoutGroup(group));
        return new LaidOutCard(placed);
    }

    private PlacedGroup LayoutGroup(AnchoredGroup group)
    {
        var stack = group.Stack;
        var align = stack.AlignValue ?? AlignFromAnchor(group.Anchor);
        var defaultMax = Math.Max(1, _size.Width - 2 * _padding);
        var maxWidth = stack.MaxWidthValue ?? defaultMax;

        // 1–3. Measure every element top-down; collect heights/widths and shaped/scaled payloads.
        var measured = MeasureElements(stack.Elements, maxWidth);
        var stackWidth = measured.Count == 0 ? 0 : measured.Max(m => m.Width);
        var stackHeight = StackHeight(measured, stack.GapValue);

        // 4. Position the stack rectangle against the anchor inside the padded box.
        var (stackLeft, stackTop) = AnchorOrigin(group.Anchor, stackWidth, stackHeight);

        // 6 (positions only; drawing is the renderer's job). Place each element absolutely.
        var placed = PlaceElements(measured, stack.GapValue, stackLeft, stackTop, stackWidth, align);

        var hasText = measured.Any(m => m.HasText);
        var bounds = SKRect.Create(stackLeft, stackTop, stackWidth, stackHeight);
        return new PlacedGroup(group.Anchor, placed, bounds, hasText);
    }

    // --- Measurement ---

    private sealed record Measured(StackElement Source, float Width, float Height, object Payload, bool HasText, bool IsSpacer);

    private List<Measured> MeasureElements(IReadOnlyList<StackElement> elements, float maxWidth)
    {
        var result = new List<Measured>(elements.Count);
        foreach (var el in elements)
            result.Add(Measure(el, maxWidth));
        return result;
    }

    private Measured Measure(StackElement el, float maxWidth)
    {
        switch (el)
        {
            case TextElement t:
            {
                var shaped = _shaper.Shape(t.Text, t.Style, _theme.Scale, maxWidth);
                var color = ResolveTextColor(t);
                return new Measured(el, shaped.Width, shaped.Height, new TextPayload(shaped, color), HasText: true, IsSpacer: false);
            }
            case ImageElement img:
            {
                var (w, h) = ResolveImageSize(img);
                return new Measured(el, w, h, Payload: null!, HasText: false, IsSpacer: false);
            }
            case SpacerElement s:
                return new Measured(el, 0, s.Pixels, Payload: null!, HasText: false, IsSpacer: true);
            case RowElement row:
            {
                var children = MeasureElements(row.Children, maxWidth);
                var width = children.Sum(c => c.Width) + row.Gap * Math.Max(0, children.Count - 1);
                var height = children.Count == 0 ? 0 : children.Max(c => c.Height);
                var hasText = children.Any(c => c.HasText);
                return new Measured(el, width, height, new RowPayload(children, row.Gap), hasText, IsSpacer: false);
            }
            default:
                throw new InvalidOperationException($"Unknown element type {el.GetType().Name}.");
        }
    }

    private sealed record TextPayload(ShapedText Shaped, SKColor Color);
    private sealed record RowPayload(IReadOnlyList<Measured> Children, int Gap);

    private (float Width, float Height) ResolveImageSize(ImageElement img)
    {
        if (img.Width is { } w0 && img.Height is { } h0)
            return (w0, h0);

        var image = _images.FromPath(img.Path);
        var aspect = image.Height == 0 ? 1f : (float)image.Width / image.Height;
        if (img.Width is { } w)
            return (w, w / aspect);
        if (img.Height is { } h)
            return (h * aspect, h);
        return (image.Width, image.Height);
    }

    private static float StackHeight(IReadOnlyList<Measured> measured, int gap)
    {
        float y = 0;
        var firstReal = true;
        foreach (var m in measured)
        {
            if (m.IsSpacer) { y += m.Height; continue; }
            if (!firstReal) y += gap;
            y += m.Height;
            firstReal = false;
        }
        return y;
    }

    // --- Positioning ---

    private List<PlacedElement> PlaceElements(IReadOnlyList<Measured> measured, int gap,
        float stackLeft, float stackTop, float stackWidth, HorizontalAlign align)
    {
        var placed = new List<PlacedElement>();
        var y = stackTop;
        var firstReal = true;

        foreach (var m in measured)
        {
            if (m.IsSpacer) { y += m.Height; continue; }
            if (!firstReal) y += gap;
            firstReal = false;

            placed.Add(Place(m, stackLeft, y, stackWidth, align));
            y += m.Height;
        }

        return placed;
    }

    private PlacedElement Place(Measured m, float stackLeft, float y, float stackWidth, HorizontalAlign align)
    {
        switch (m.Source)
        {
            case TextElement:
            {
                var p = (TextPayload)m.Payload;
                // The text block spans the full stack width; individual lines align within it at draw time.
                return new PlacedText(p.Shaped, p.Color, align, stackLeft, y, stackWidth);
            }
            case ImageElement img:
            {
                var x = stackLeft + AlignOffset(stackWidth, m.Width, align);
                return new PlacedImage(img, SKRect.Create(x, y, m.Width, m.Height));
            }
            case RowElement:
            {
                var p = (RowPayload)m.Payload;
                var rowX = stackLeft + AlignOffset(stackWidth, m.Width, align);
                return PlaceRow(p, rowX, y, m.Height);
            }
            default:
                throw new InvalidOperationException($"Cannot place {m.Source.GetType().Name}.");
        }
    }

    private PlacedRow PlaceRow(RowPayload row, float rowX, float rowTop, float rowHeight)
    {
        var children = new List<PlacedElement>();
        var x = rowX;
        foreach (var child in row.Children)
        {
            // Row children are vertically centered within the row's height.
            var childTop = rowTop + (rowHeight - child.Height) / 2f;
            switch (child.Source)
            {
                case TextElement:
                {
                    var p = (TextPayload)child.Payload;
                    children.Add(new PlacedText(p.Shaped, p.Color, HorizontalAlign.Left, x, childTop, child.Width));
                    break;
                }
                case ImageElement img:
                    children.Add(new PlacedImage(img, SKRect.Create(x, childTop, child.Width, child.Height)));
                    break;
                default:
                    throw new InvalidOperationException("Rows may only contain text and images (v1).");
            }
            x += child.Width + row.Gap;
        }
        return new PlacedRow(children);
    }

    // --- Helpers ---

    private SKColor ResolveTextColor(TextElement t)
    {
        var hex = t.ColorOverride ?? (_theme.TextColor.Length > 0 ? _theme.TextColor : "#ffffff");
        var baseColor = Color.TryParse(hex, out var c) ? c : SKColors.White;
        return Color.WithOpacity(baseColor, t.Opacity);
    }

    private (float Left, float Top) AnchorOrigin(Anchor anchor, float stackWidth, float stackHeight)
    {
        var col = (int)anchor % 3;   // 0 left, 1 center, 2 right
        var rowBand = (int)anchor / 3; // 0 top, 1 middle, 2 bottom

        var left = col switch
        {
            0 => (float)_padding,
            1 => (_size.Width - stackWidth) / 2f,
            _ => _size.Width - _padding - stackWidth,
        };

        var top = rowBand switch
        {
            0 => (float)_padding,
            1 => (_size.Height - stackHeight) / 2f,
            _ => _size.Height - _padding - stackHeight,
        };

        return (left, top);
    }

    internal static float AlignOffset(float containerWidth, float contentWidth, HorizontalAlign align) => align switch
    {
        HorizontalAlign.Left => 0,
        HorizontalAlign.Center => (containerWidth - contentWidth) / 2f,
        HorizontalAlign.Right => containerWidth - contentWidth,
        _ => 0,
    };

    internal static HorizontalAlign AlignFromAnchor(Anchor anchor) => ((int)anchor % 3) switch
    {
        0 => HorizontalAlign.Left,
        1 => HorizontalAlign.Center,
        _ => HorizontalAlign.Right,
    };
}
