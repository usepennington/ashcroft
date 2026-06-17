---
title: Ashcroft
description: Social card generation for .NET — a tiny declarative API over SkiaSharp + HarfBuzz.
uid: ashcroft.home
---

Every code sample on this page is pulled straight from the doc site's compiled source, and **every
image below it was produced by running that exact sample through Ashcroft** — there are no
screenshots here, only live output.

> [!IMPORTANT]
> On **Linux / Docker / CI** the native binaries ship in separate packages and Ashcroft can't
> reference them for you — add **both** `SkiaSharp.NativeAssets.Linux.NoDependencies` **and**
> `HarfBuzzSharp.NativeAssets.Linux` to your startup project. Windows and macOS bundle both in the
> box. The HarfBuzz one is the easy miss: without it Skia paints the background fine and then the
> first line of text throws. Full setup and the exact versions are in
> [Running on Linux, Docker, or CI](#running-on-linux-docker-or-ci).

## The whole pitch

Five lines to a good-looking 1200×630 PNG. A built-in gradient, a centered title, all defaults:

<div class="sample">

```csharp:symbol,bodyonly
Samples/CardSamples.cs > CardSamples.MinimumViable
```

![Minimum viable card](/samples/minimum.png)

</div>

## Backgrounds and the scrim

A background can be a color, a built-in gradient, an image (cover-fit and center-cropped), or a
lambda over the raw `SKCanvas`. When an anchored group contains text **and** the background is a
photo or a lambda, Ashcroft draws a subtle dark **scrim** behind that region — fading from ~55%
black at the nearest edge to transparent toward the center. This single default is most of why
zero-config output looks deliberate. Watch the bottom of the card darken:

<div class="sample">

```csharp:symbol,bodyonly
Samples/CardSamples.cs > CardSamples.ImageBackgroundWithScrim
```

![Text over a photo with the automatic scrim](/samples/scrim.png)

</div>

To see what that default buys you, here is the same card with and without it — opt out with
`.NoScrim()`, or pin a specific strength with `.Scrim(0.7f)`:

<div class="compare">
<figure>

![The card with the automatic scrim](/samples/scrim.png)

<figcaption>default — automatic scrim</figcaption>
</figure>
<figure>

![The same card rendered with NoScrim, the text fighting the photo](/samples/scrim-off.png)

<figcaption>.NoScrim() — text fights the photo</figcaption>
</figure>
</div>

## Anchors, stacks, and elements

Content is a **stack** pinned to one of nine anchors; alignment is inherited from the anchor. Stacks
hold role-based elements — `Title`, `Subtitle`, `Meta` — plus `Image`, `Spacer`, and a `Row` for the
avatar-and-byline pattern. The roles encode a tested type scale and opacity ramp, so you describe
*what the text is*, not what it looks like. Here's the expected common case, a blog card:

<div class="sample">

```csharp:symbol,bodyonly
Samples/CardSamples.cs > CardSamples.BlogCard
```

![Blog post card with logo, title, subtitle, and an avatar row](/samples/blog.png)

</div>

## The text engine

Every string is shaped by HarfBuzz and wrapped on shaped-cluster boundaries — measurement and
drawing share the same glyphs, so nothing ever clips mid-character. The payoff shows up when a
title refuses to fit: a `Title` wraps up to three lines, then steps its size down toward a 70%
floor, and only then ellipsizes. It never throws and never overflows the card:

<div class="sample">

```csharp:symbol,bodyonly
Samples/CardSamples.cs > CardSamples.TitleThatShrinks
```

![A very long title that shrank and ellipsized instead of overflowing](/samples/title-long.png)

</div>

The same layout with a title of each length — no code changes between them:

<div class="compare">
<figure>

![A short title rendered at the full 64px](/samples/title-fits.png)

<figcaption>short — full 64px</figcaption>
</figure>
<figure>

![A long title shrunk toward the 70% floor and ellipsized](/samples/title-long.png)

<figcaption>long — shrunk to 70%, then ellipsized</figcaption>
</figure>
</div>

Shaping is also per **run**: when the primary face can't cover a codepoint, Ashcroft finds a
system face that can — emoji and CJK in one string need zero configuration:

<div class="sample">

```csharp:symbol,bodyonly
Samples/CardSamples.cs > CardSamples.EmojiAndCjk
```

![A title mixing Latin text, an emoji, and Japanese characters](/samples/emoji.png)

</div>

## Generative backgrounds

Need something we didn't anticipate? Draw it yourself — `Background` takes a
`(canvas, size)` lambda over the raw Skia surface, and the role text still sits on top with its
defaults intact:

<div class="sample">

```csharp:symbol,bodyonly
Samples/CardSamples.cs > CardSamples.Generative
```

![Generative iso-grid background with a centered title](/samples/generative.png)

</div>

## Theming

Card-wide changes go through a `Theme` — font family, text color, and a scale multiplier over the
whole type ramp. One-off overrides ride on optional parameters per element. Colors are hex strings
everywhere, so casual users never construct an `SKColor`:

<div class="sample">

```csharp:symbol,bodyonly
Samples/CardSamples.cs > CardSamples.Themed
```

![Custom-themed card over a radial gradient](/samples/themed.png)

</div>

## Coloring individual elements

When the theme is right but one element isn't, every role takes an optional `color:` and `size:` —
overrides are opt-in, not all-or-nothing. And when the roles themselves aren't enough, `Text()`
takes a full `TextStyle` (size, weight, letter-spacing, line height); its color is used exactly as
given, with no role opacity ramp applied. Here the kicker is a custom `Text()`, the title stays on
the theme default, and the subtitle and meta are tinted:

<div class="sample">

```csharp:symbol,bodyonly
Samples/CardSamples.cs > CardSamples.ElementColors
```

![A card with an amber kicker, default title, and tinted subtitle and meta](/samples/colors.png)

</div>

## Custom fonts

Two ways in. `Theme.FontFamily` asks for an installed font by name and falls back silently down a
chain (requested → Segoe UI → Helvetica Neue → platform sans) — hook `AshcroftDiagnostics.Log` to
hear about it. `Theme.FontPath` skips resolution entirely and loads a TTF/OTF you ship with your
app, which is the reproducible choice for CI and containers. One file is one face — it carries
every weight on the card, so pick a face that reads well everywhere it'll land:

<div class="sample">

```csharp:symbol,bodyonly
Samples/CardSamples.cs > CardSamples.CustomFont
```

![A card set in Space Grotesk loaded from a bundled font file](/samples/font.png)

</div>

## Mixing fonts in one card

`Theme.FontPath` sets a single face for the whole card, but a display headline over body text wants
*two*. `Theme.FontFiles` registers any number of bundled files under the family name each one reports,
so a per-element `FontFamily` resolves to the bundled file **before** any system lookup — deterministic
on every machine, with nothing installed. Here the headline is Space Grotesk from a shipped file while
the body and meta stay on the embedded Noto Sans default:

<div class="sample">

```csharp:symbol,bodyonly
Samples/CardSamples.cs > CardSamples.MixedFonts
```

![A Space Grotesk display headline over Noto Sans body text in one card](/samples/mixed-fonts.png)

</div>

## Weight and letter-spacing

The bundled default is a **variable** Noto Sans, so `Weight` is a continuous knob from 100 to 900 —
not a handful of presets. Pair it with `LetterSpacing` on a `Text()` and one typeface covers a whole
specimen: a wide-tracked label, a hairline subhead, a heavy headline. Because shaping is
weight-aware, measurement follows the weight too, so wrapping and centering stay honest at every step:

<div class="sample">

```csharp:symbol,bodyonly
Samples/CardSamples.cs > CardSamples.WeightsAndSpacing
```

![A type specimen mixing a letter-spaced label with light and black weights of one variable font](/samples/weights.png)

</div>

## Fine-tuning the layout

The defaults — 64px padding, 12px gap, text wrapping at the card width — are tuned for the common
case, and each has an override: `Padding` on the card; `MaxWidth`, `Gap`, and `Align` on a stack;
`Spacer` for a one-off gap between two elements. Images clip to `Rounded` (with a corner radius)
or `Circle`. Here a capped text column shares the card with a rounded image on the opposite edge:

<div class="sample">

```csharp:symbol,bodyonly
Samples/CardSamples.cs > CardSamples.FineTuning
```

![A two-column card: a narrow text stack on the left, a rounded image on the right](/samples/fine-tuning.png)

</div>

## Output

Rendering is deferred — nothing rasterizes until you ask for bytes:

```csharp
card.Save("og.png");                       // format inferred from the extension
card.Save(stream, ImageFormat.Png);
byte[] bytes = card.ToBytes(ImageFormat.Webp, quality: 90);
using SKImage img = card.ToImage();        // escape hatch for further Skia work
```

`.Scale(2)` renders at 2× pixel density with all layout values multiplied — crisp on high-DPI
surfaces. PNG is the default and the recommendation for OG images.

## Running on Linux, Docker, or CI

Ashcroft renders with SkiaSharp and shapes **every** string with HarfBuzz, so it needs two native
libraries at runtime: `libSkiaSharp` and `libHarfBuzzSharp`. On Windows and macOS both arrive
automatically with the `SkiaSharp` and `SkiaSharp.HarfBuzz` packages Ashcroft already depends on, so
there's nothing to do. **On Linux those base packages contain no native binaries** — you add the
platform-specific native asset packages yourself, and you need *both*. NuGet won't pull them in
transitively, and Ashcroft deliberately doesn't list them as dependencies: doing so would force the
Linux `.so` files onto every Windows and macOS consumer that never needs them.

Add both to the project that actually builds or runs your app — your web app or worker, not a class
library in the middle:

```xml
<ItemGroup>
  <PackageReference Include="SkiaSharp.NativeAssets.Linux.NoDependencies" Version="4.148.0-rc.1.2" />
  <PackageReference Include="HarfBuzzSharp.NativeAssets.Linux" Version="14.2.0-rc.1.2" />
</ItemGroup>
```

A few things that trip people up:

- **Two packages, two version lines.** SkiaSharp and HarfBuzzSharp version independently — Skia is
  on `4.148.x` here while HarfBuzzSharp is on `14.2.x`. Don't try to align the numbers; use the
  versions Ashcroft is built against (above, matching what the `SkiaSharp` / `SkiaSharp.HarfBuzz`
  packages resolve to).
- **The HarfBuzz native is the one people forget.** With only the Skia asset present, the
  background and shapes draw and then the first run of text throws
  `DllNotFoundException: libHarfBuzzSharp`. The mirror symptom, `DllNotFoundException:
  libSkiaSharp`, means the Skia asset is the one missing.
- **`.NoDependencies` vs plain.** `SkiaSharp.NativeAssets.Linux.NoDependencies` is the headless
  build and the right default for containers and CI — it needs no system `fontconfig` installed.
  Reach for plain `SkiaSharp.NativeAssets.Linux` only if you specifically want fontconfig-based
  system-font discovery; Ashcroft's embedded fonts render without it.
- **Build host vs deploy target.** The references above are unconditional, so they're included no
  matter where you build — the right call when you build on Windows or macOS and deploy to Linux. If
  you only ever build on the same OS you ship to, you can guard the group with
  `Condition="$([MSBuild]::IsOSPlatform('Linux'))"` to keep the Linux `.so` out of other outputs.
