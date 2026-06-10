using Ashcroft.Internal;

namespace Ashcroft;

/// <summary>The single entry point. <c>SocialCard.Create()</c> starts a card; chain from there.</summary>
public static class SocialCard
{
    /// <summary>Start a card at an explicit size (defaults to the 1200×630 Open Graph standard).</summary>
    public static CardBuilder Create(int width = 1200, int height = 630)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
        return new CardBuilder(new SkiaSharp.SKSizeI(width, height));
    }

    /// <summary>Start a card from a size preset.</summary>
    public static CardBuilder Create(CardSize size) => new(CardSizes.Resolve(size));
}
