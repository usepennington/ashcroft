using SkiaSharp;

namespace Ashcroft.Internal;

/// <summary>
/// The render pass: build a (possibly scaled) surface, paint the background, lay out and draw each
/// anchored group — scrim first when warranted — and snapshot. Everything stays in logical units
/// because the canvas is pre-scaled by the pixel-density factor.
/// </summary>
internal static class CardRenderer
{
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public static SKImage Render(CardBuilder card)
    {
        var size = card.Size;
        var scale = card.ScaleFactor;
        var pxW = Math.Max(1, (int)MathF.Round(size.Width * scale));
        var pxH = Math.Max(1, (int)MathF.Round(size.Height * scale));

        using var surface = SKSurface.Create(new SKImageInfo(pxW, pxH, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale); // draw everything below in logical units

        using var resolver = new FontResolver(card.ThemeValue);
        using var shaper = new TextShaper(resolver);
        using var images = new ImageLoader();

        DrawBackground(canvas, size, card.BackgroundSource, images);

        var engine = new LayoutEngine(shaper, images, card.ThemeValue, size, card.PaddingValue);
        var laidOut = engine.Layout(card.Groups);

        var backgroundWarrantsScrim = card.BackgroundSource?.WarrantsScrim ?? false;
        foreach (var group in laidOut.Groups)
        {
            if (group.HasText && backgroundWarrantsScrim && !card.ScrimDisabled)
                ScrimPainter.Paint(canvas, size, group, card.ScrimOpacity);
            DrawGroup(canvas, group, images);
        }

        return surface.Snapshot();
    }

    // --- Background ---

    private static void DrawBackground(SKCanvas canvas, SKSizeI size, BackgroundSource? bg, ImageLoader images)
    {
        switch (bg)
        {
            case null:
                canvas.Clear(new SKColor(0x11, 0x18, 0x27)); // #111827 — text-on-dark still looks intentional
                break;
            case ColorBackground c:
                canvas.Clear(c.Color);
                break;
            case FillBackground f:
                DrawFill(canvas, size, f.Fill);
                break;
            case ImagePathBackground p:
                DrawCover(canvas, size, images.FromPath(p.Path));
                break;
            case ImageBytesBackground b:
                DrawCover(canvas, size, images.FromBytes(b.Data));
                break;
            case LambdaBackground l:
                l.Draw(canvas, size);
                break;
        }
    }

    private static void DrawCover(SKCanvas canvas, SKSizeI size, SKImage image)
    {
        // Cover-fit + center-crop: scale to fill, overflow centered off both edges.
        var scale = MathF.Max((float)size.Width / image.Width, (float)size.Height / image.Height);
        var w = image.Width * scale;
        var h = image.Height * scale;
        var dx = (size.Width - w) / 2f;
        var dy = (size.Height - h) / 2f;
        canvas.DrawImage(image, SKRect.Create(dx, dy, w, h), Sampling);
    }

    private static void DrawFill(SKCanvas canvas, SKSizeI size, BackgroundFill fill)
    {
        float w = size.Width, h = size.Height;
        switch (fill.Kind)
        {
            case BackgroundFill.FillKind.Solid:
                canvas.Clear(Color.Parse(fill.From));
                return;
            case BackgroundFill.FillKind.Linear:
            {
                var rad = fill.AngleDegrees * MathF.PI / 180f;
                var dx = MathF.Cos(rad);
                var dy = MathF.Sin(rad);
                var p0 = new SKPoint(w / 2 - dx * w / 2, h / 2 - dy * h / 2);
                var p1 = new SKPoint(w / 2 + dx * w / 2, h / 2 + dy * h / 2);
                using var shader = SKShader.CreateLinearGradient(p0, p1,
                    new[] { Color.Parse(fill.From), Color.Parse(fill.To) }, SKShaderTileMode.Clamp);
                using var paint = new SKPaint { Shader = shader };
                canvas.DrawRect(0, 0, w, h, paint);
                return;
            }
            case BackgroundFill.FillKind.Radial:
            {
                using var shader = SKShader.CreateRadialGradient(new SKPoint(w / 2, h / 2), MathF.Max(w, h) / 2f,
                    new[] { Color.Parse(fill.From), Color.Parse(fill.To) }, SKShaderTileMode.Clamp);
                using var paint = new SKPaint { Shader = shader };
                canvas.DrawRect(0, 0, w, h, paint);
                return;
            }
        }
    }

    // --- Foreground ---

    private static void DrawGroup(SKCanvas canvas, PlacedGroup group, ImageLoader images)
    {
        foreach (var element in group.Elements)
            DrawElement(canvas, element, images);
    }

    private static void DrawElement(SKCanvas canvas, PlacedElement element, ImageLoader images)
    {
        switch (element)
        {
            case PlacedText t:
                DrawText(canvas, t);
                break;
            case PlacedImage img:
                DrawImage(canvas, img, images);
                break;
            case PlacedRow row:
                foreach (var child in row.Children)
                    DrawElement(canvas, child, images);
                break;
        }
    }

    private static void DrawText(SKCanvas canvas, PlacedText text)
    {
        using var paint = new SKPaint { Color = text.Color, IsAntialias = true };
        var shaped = text.Shaped;

        for (var i = 0; i < shaped.Lines.Count; i++)
        {
            var line = shaped.Lines[i];
            if (line.Runs.Count == 0)
                continue;

            var boxTop = text.BlockTop + i * shaped.LineHeightPx;
            // Center the metrics within the line box, then sit the baseline below the ascent.
            var leading = shaped.LineHeightPx - (line.Ascent + line.Descent);
            var baseline = boxTop + line.Ascent + leading / 2f;
            var lineX = text.BlockX + LayoutEngine.AlignOffset(text.BlockWidth, line.Width, text.Align);

            using var builder = new SKTextBlobBuilder();
            foreach (var run in line.Runs)
            {
                if (run.Glyphs.Length == 0)
                    continue;
                using var font = new SKFont(run.Typeface, shaped.Size);
                builder.AddPositionedRun(run.Glyphs, font, run.Positions);
            }

            using var blob = builder.Build();
            if (blob is not null)
            {
                canvas.Save();
                canvas.Translate(lineX, baseline);
                canvas.DrawText(blob, 0, 0, paint);
                canvas.Restore();
            }
        }
    }

    private static void DrawImage(SKCanvas canvas, PlacedImage placed, ImageLoader images)
    {
        var image = images.FromPath(placed.Source.Path);
        var dest = placed.Dest;

        canvas.Save();
        switch (placed.Source.Shape)
        {
            case ImageShape.Circle:
            {
                var radius = MathF.Min(dest.Width, dest.Height) / 2f;
                canvas.ClipPath(CirclePath(dest.MidX, dest.MidY, radius), antialias: true);
                break;
            }
            case ImageShape.Rounded:
            {
                using var rounded = new SKRoundRect(dest, placed.Source.CornerRadius);
                canvas.ClipRoundRect(rounded, antialias: true);
                break;
            }
        }

        canvas.DrawImage(image, dest, Sampling);
        canvas.Restore();
    }

    private static SKPath CirclePath(float cx, float cy, float radius)
    {
        var path = new SKPath();
        path.AddCircle(cx, cy, radius);
        return path;
    }

    // --- Encoding ---

    public static SKData Encode(SKImage image, ImageFormat format, int quality)
    {
        var skFormat = format switch
        {
            ImageFormat.Png => SKEncodedImageFormat.Png,
            ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
            ImageFormat.Webp => SKEncodedImageFormat.Webp,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image format."),
        };
        return image.Encode(skFormat, Math.Clamp(quality, 1, 100))
               ?? throw new InvalidOperationException($"Failed to encode image as {format}.");
    }

    public static ImageFormat FormatFromExtension(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => ImageFormat.Png,
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".webp" => ImageFormat.Webp,
            _ => throw new ArgumentException($"Cannot infer image format from extension '{ext}'. Use .png, .jpg, .jpeg, or .webp.", nameof(path)),
        };
    }
}
