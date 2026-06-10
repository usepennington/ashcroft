using SkiaSharp;

namespace Ashcroft.Internal;

/// <summary>
/// The resolved background of a card. <see cref="WarrantsScrim"/> distinguishes the
/// "photographic / unknown" sources (image, lambda) — which get a legibility scrim behind
/// text — from flat color/gradient fills, which don't.
/// </summary>
internal abstract class BackgroundSource
{
    public abstract bool WarrantsScrim { get; }
}

internal sealed class ColorBackground : BackgroundSource
{
    public required SKColor Color { get; init; }
    public override bool WarrantsScrim => false;
}

internal sealed class FillBackground : BackgroundSource
{
    public required BackgroundFill Fill { get; init; }
    public override bool WarrantsScrim => false;
}

internal sealed class ImagePathBackground : BackgroundSource
{
    public required string Path { get; init; }
    public override bool WarrantsScrim => true;
}

internal sealed class ImageBytesBackground : BackgroundSource
{
    public required ReadOnlyMemory<byte> Data { get; init; }
    public override bool WarrantsScrim => true;
}

internal sealed class LambdaBackground : BackgroundSource
{
    public required Action<SKCanvas, SKSizeI> Draw { get; init; }
    public override bool WarrantsScrim => true;
}
