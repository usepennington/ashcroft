# Ashcroft

Generate social cards — Open Graph images, Twitter/X cards, blog headers — from a tiny, declarative C# API. Give it a background, describe your content, pin that content to an anchor, and save. Typography, spacing, wrapping, and legibility all have sensible defaults, so the zero-configuration result already looks designed.

```csharp
SocialCard.Create()
    .Background("assets/header.jpg")
    .At(Anchor.BottomLeft, stack => stack
        .Title("Shaping Text the Hard Way")
        .Subtitle("What HarfBuzz actually does, and why you want it")
        .Meta("philscott.dev · June 2026"))
    .Save("og.png");
```

Five lines to a good-looking 1200×630 PNG.

## Install

```sh
dotnet add package Ashcroft
```

Targets **.NET 10**. Depends on [SkiaSharp](https://github.com/mono/SkiaSharp) and HarfBuzz (via `SkiaSharp.HarfBuzz`).

## ⚠️ Linux / Docker / CI: you need a native assets package

SkiaSharp ships native binaries per platform. On Windows and macOS they come in the box, but on **Linux (including most CI runners and Docker images) you must add the headless native assets package yourself**, or the first render throws a `DllNotFoundException` / `Cannot find libSkiaSharp`:

```sh
dotnet add package SkiaSharp.NativeAssets.Linux.NoDependencies
dotnet add package HarfBuzzSharp.NativeAssets.Linux
```

Use `SkiaSharp.NativeAssets.Linux` (without `.NoDependencies`) only if you also want the fontconfig/freetype system dependencies pulled in. This is the single most common support question for every Skia-based library — handle it up front.

**Fonts need no setup.** Ashcroft embeds Noto Sans (the default typeface), Noto Color Emoji, and Noto Sans JP — all OFL-licensed — so text, emoji, and Japanese render identically on a bare container, CI runner, or your laptop with zero system fonts installed. Other scripts (Korean, Chinese, Arabic, …) and any `Theme.FontFamily` you request by name still resolve from system fonts when present, falling back to the bundled Noto Sans otherwise.

## Why HarfBuzz

All text is shaped with HarfBuzz, not drawn with naïve `SKCanvas.DrawText`. That buys you correct kerning, ligatures, combining marks, and non-Latin scripts; honest wrapping measured on shaped widths; shrink-to-fit before ellipsis on long titles; and per-run font fallback, so `"Shipping 🚀 to 東京"` renders rather than tofu-boxing.

## Core model

A card is four ideas, applied in order:

```
Card  =  Canvas (size)  +  Background  +  Anchored content groups  +  Output
```

### Canvas

```csharp
SocialCard.Create()               // 1200 × 630 — the OG standard
SocialCard.Create(1600, 900)      // explicit size
SocialCard.Create(CardSize.Square) // OpenGraph (default), Square, Wide, Story
```

### Background

```csharp
.Background("#1e293b")                                   // solid color
.Background(Backgrounds.LinearGradient("#0f172a", "#3b0764"))
.Background("assets/header.jpg")                          // image: cover-fit, center-cropped
.Background(stream)                                       // or a Stream / ReadOnlyMemory<byte>
.Background((canvas, size) => { /* raw SKCanvas */ });    // generative
```

No background set ⇒ a near-black `#111827` solid (text-on-dark still looks intentional).

**The scrim.** When an anchored group contains text *and* the background is an image or a lambda, Ashcroft draws a subtle dark gradient behind that region — fading from ~55% black at the card edge nearest the anchor to transparent toward the center. This is most of why zero-config output looks deliberate. Opt out with `.NoScrim()`, or tune it with `.Scrim(0.7f)`. Solid/gradient backgrounds get none.

### Content: anchors and stacks

Pin a vertical **stack** to one of nine anchors. Alignment is inherited from the anchor (a `BottomLeft` stack left-aligns; `TopCenter` centers; `MiddleRight` right-aligns).

```csharp
.At(Anchor.BottomLeft, stack => stack
    .Title("The post title, which wraps if it needs to")
    .Subtitle("A one-line description")
    .Row(row => row
        .Image("avatar.jpg", height: 44, shape: ImageShape.Circle)
        .Meta("Phil Scott · 9 min read")))
```

- **Padding:** 64px inset from the edges by default (`.Padding(int)`).
- **Stacks flow downward** with a 12px gap (`.Gap(int)`); `.Spacer(px)` adds more.
- **Max width** defaults to `cardWidth − 2·padding` so text wraps; override with `.MaxWidth(int)`.

### Elements

| Element | Default styling |
|---|---|
| `Title` | 64px bold white, wraps to 3 lines, then shrinks, then ellipsizes |
| `Subtitle` | 30px regular, white @ 85%, wraps to 2 lines |
| `Meta` | 22px medium, white @ 65%, single line, ellipsized |
| `Text(text, style)` | fully custom via `TextStyle` |
| `Image(path, …)` | aspect-preserving; `Rect` / `Rounded` / `Circle` |
| `Spacer(px)` | extra vertical gap |
| `Row(…)` | horizontal group, vertically centered (avatar + name) |

### Themes

```csharp
.Theme(new Theme
{
    FontFamily = "Inter",            // resolved via SKFontManager; falls back to the embedded Noto Sans
    FontPath   = "assets/Inter.ttf", // optional: load an exact file
    TextColor  = "#f8fafc",
    Scale      = 1.1f                // multiplies the whole type scale
})
```

Colors are hex strings everywhere (`#rgb`, `#rrggbb`, `#aarrggbb`).

### Output

```csharp
card.Save("og.png");                       // format inferred from extension
card.Save(stream, ImageFormat.Png);
byte[] bytes = card.ToBytes(ImageFormat.Webp, quality: 90);
using SKImage img = card.ToImage();        // escape hatch for further Skia work
```

`.Scale(2)` renders at 2× pixel density with all layout values multiplied. Rendering is deferred — nothing rasterizes until `Save`/`ToBytes`/`ToImage`, so a builder is cheap to construct and reuse.

## Diagnostics

An unresolvable font falls back silently rather than crashing your build pipeline, but you can observe what happened:

```csharp
AshcroftDiagnostics.Log = msg => logger.LogInformation(msg);
```

## Errors

- Missing background/element image ⇒ `FileNotFoundException` with the path, at render time.
- Empty card (no content) renders the background alone — valid.
- Text that can't fit even after shrink + ellipsis renders its first line ellipsized; it never throws.

## Building from source

```sh
dotnet build Ashcroft.slnx
dotnet test  Ashcroft.slnx
```

Snapshot baselines under `tests/Ashcroft.Tests/Approved/` are self-seeding: the first run on a machine writes them, later runs pixel-diff against them. Delete that folder to re-seed after an intentional visual change.

See [`spec.md`](spec.md) for the full v1 specification.
