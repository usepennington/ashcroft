using Ashcroft;
using Ashcroft.Docs.Samples;

namespace Ashcroft.Docs.Rendering;

/// <summary>
/// The single registry of renderable samples, keyed by the id used in <c>/samples/{id}.png</c>.
/// Both the dev-time <c>MapGet</c> and the build-time emitter call <see cref="Render"/>, so the
/// served image and the emitted file can never diverge.
/// </summary>
public static class SampleGallery
{
    public static readonly IReadOnlyDictionary<string, Func<CardBuilder>> Samples =
        new Dictionary<string, Func<CardBuilder>>
        {
            ["minimum"]    = CardSamples.MinimumViable,
            ["scrim"]      = CardSamples.ImageBackgroundWithScrim,
            ["scrim-off"]  = CardSamples.ImageBackgroundWithoutScrim,
            ["blog"]       = CardSamples.BlogCard,
            ["title-fits"] = CardSamples.TitleThatFits,
            ["title-long"] = CardSamples.TitleThatShrinks,
            ["emoji"]      = CardSamples.EmojiAndCjk,
            ["generative"] = CardSamples.Generative,
            ["themed"]     = CardSamples.Themed,
            ["colors"]     = CardSamples.ElementColors,
            ["font"]       = CardSamples.CustomFont,
            ["fine-tuning"] = CardSamples.FineTuning,
        };

    /// <summary>Runs the sample through Ashcroft and returns the encoded PNG bytes.</summary>
    public static byte[] Render(string id) => Samples[id]().ToBytes(ImageFormat.Png);
}
