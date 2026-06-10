using SkiaSharp;

namespace Ashcroft.Internal;

/// <summary>
/// Draws the legibility scrim: a dark gradient fading from the card edge nearest the anchor toward the
/// center. This single default is most of why zero-config text over a photo reads as deliberate.
/// </summary>
internal static class ScrimPainter
{
    private const float DefaultOpacity = 0.55f;

    public static void Paint(SKCanvas canvas, SKSizeI size, PlacedGroup group, float? opacityOverride)
    {
        var opacity = Math.Clamp(opacityOverride ?? DefaultOpacity, 0f, 1f);
        if (opacity <= 0f)
            return;

        var dark = new SKColor(0, 0, 0, (byte)Math.Round(opacity * 255));
        var clear = new SKColor(0, 0, 0, 0);
        float w = size.Width, h = size.Height;

        using var shader = ShaderForAnchor(group.Anchor, w, h, dark, clear);
        using var paint = new SKPaint { Shader = shader, IsAntialias = false };
        canvas.DrawRect(0, 0, w, h, paint);
    }

    private static SKShader ShaderForAnchor(Anchor anchor, float w, float h, SKColor dark, SKColor clear)
    {
        var rowBand = (int)anchor / 3;   // 0 top, 1 middle, 2 bottom
        var col = (int)anchor % 3;       // 0 left, 1 center, 2 right
        var colors = new[] { dark, clear };

        // Top / bottom rows fade vertically from their edge; the middle row fades horizontally,
        // and dead-center darkens radially behind the centered text.
        if (rowBand == 2)
            return SKShader.CreateLinearGradient(new SKPoint(0, h), new SKPoint(0, h * 0.40f), colors, SKShaderTileMode.Clamp);
        if (rowBand == 0)
            return SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, h * 0.60f), colors, SKShaderTileMode.Clamp);
        if (col == 0)
            return SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(w * 0.60f, 0), colors, SKShaderTileMode.Clamp);
        if (col == 2)
            return SKShader.CreateLinearGradient(new SKPoint(w, 0), new SKPoint(w * 0.40f, 0), colors, SKShaderTileMode.Clamp);

        return SKShader.CreateRadialGradient(new SKPoint(w / 2, h / 2), MathF.Max(w, h) * 0.5f, colors, SKShaderTileMode.Clamp);
    }
}
