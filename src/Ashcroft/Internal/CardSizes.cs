using SkiaSharp;

namespace Ashcroft.Internal;

internal static class CardSizes
{
    /// <summary>Maps a <see cref="CardSize"/> preset to its pixel dimensions.</summary>
    public static SKSizeI Resolve(CardSize size) => size switch
    {
        CardSize.OpenGraph => new SKSizeI(1200, 630),
        CardSize.Square    => new SKSizeI(1080, 1080),
        CardSize.Wide      => new SKSizeI(1920, 1080),
        CardSize.Story     => new SKSizeI(1080, 1920),
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown card size preset."),
    };
}
