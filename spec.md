# Ashcroft — Social Card Generation for .NET

**Status:** Draft v1
**Target:** .NET 10 class library, distributed as a NuGet package
**Dependencies:** SkiaSharp, HarfBuzzSharp (via SkiaSharp.HarfBuzz)

## Overview

Ashcroft generates social cards — Open Graph images, Twitter/X cards, blog post headers — from a tiny, declarative C# API. You give it a background, describe your content, pin that content to an anchor point, and save. Everything else (typography, spacing, wrapping, legibility) has a sensible default so that the zero-configuration result already looks designed.

```csharp
SocialCard.Create()
    .Background("assets/header.jpg")
    .At(Anchor.BottomLeft, stack => stack
        .Title("Shaping Text the Hard Way")
        .Subtitle("What HarfBuzz actually does, and why you want it")
        .Meta("philscott.dev · June 2026"))
    .Save("og.png");
```

That's the whole pitch: five lines to a good-looking 1200×630 PNG.

## Goals

1. **Stumble into good design.** A user who supplies only a background and a title gets a card with correct OG dimensions, a readable type scale, comfortable margins, and legible text over any image.
2. **Simple, discoverable syntax.** One static entry point (`SocialCard.Create`), a fluent chain, and lambdas for nesting. No XML, no templates, no separate layout files.
3. **Real text rendering.** All text is shaped with HarfBuzz — correct kerning, ligatures, combining marks, and non-Latin scripts. No `SKCanvas.DrawText` naïveté.
4. **Escape hatches, not walls.** Every default is overridable, and the raw `SKCanvas` is reachable when someone needs to draw something we didn't anticipate.

## Non-Goals (v1)

- HTML/CSS-style layout engines (flexbox, grid, measurement constraints between siblings).
- Collision detection between anchored groups (you can overlap two stacks; we won't stop you).
- Animated or multi-frame output.
- A template/theming marketplace. (A `Theme` record exists, but it's a value you construct, not a file format.)
- ASP.NET endpoint helpers — likely a v2 companion package (`Ashcroft.AspNetCore`) that serves cards from a minimal-API route with caching.

## Core Model

A card is built from four ideas, applied in order:

```
Card  =  Canvas (size)  +  Background  +  Anchored content groups  +  Output
```

### 1. The canvas

```csharp
SocialCard.Create()              // 1200 × 630 — the OG standard
SocialCard.Create(1600, 900)     // explicit size
SocialCard.Create(CardSize.Square)   // presets: OpenGraph (default), Square (1080), Wide (1920×1080), Story (1080×1920)
```

### 2. Background

Three ways to fill the canvas, in increasing order of effort:

```csharp
// a. Solid color or built-in gradient
.Background("#1e293b")
.Background(Backgrounds.LinearGradient("#0f172a", "#3b0764"))

// b. An image — loaded from path, stream, or bytes; scaled cover-fit and center-cropped
.Background("assets/header.jpg")
.Background(stream)

// c. A lambda over the raw SKCanvas, for geometric/generative backgrounds
.Background((canvas, size) =>
{
    canvas.Clear(SKColors.MidnightBlue);
    var paint = new SKPaint { Color = SKColors.SlateBlue.WithAlpha(60), IsAntialias = true };
    for (var i = 0; i < 12; i++)
        canvas.DrawCircle(size.Width * i / 12f, size.Height * 0.8f, 180, paint);
});
```

If no background is set, the card uses a near-black (`#111827`) solid — text-on-dark is the failure mode that still looks intentional.

**The scrim (a load-bearing default).** Text over arbitrary photos is usually illegible. When any anchored group contains text *and* the background is an image or a lambda, Ashcroft draws a subtle dark gradient (a scrim) behind that region — fading from ~55% black at the card edge nearest the anchor to transparent toward the center. This single default is most of why zero-config output looks deliberate. Opt out per card with `.NoScrim()`, or tune it with `.Scrim(opacity: 0.7f)`. Solid/gradient color backgrounds get no scrim.

### 3. Content: anchors and stacks

Content is placed by anchoring a **stack** (a vertical flow of elements) to one of nine positions:

```
TopLeft       TopCenter       TopRight
MiddleLeft    Center          MiddleRight
BottomLeft    BottomCenter    BottomRight
```

```csharp
.At(Anchor.BottomLeft, stack => stack
    .Title("The post title, which will wrap if it needs to")
    .Subtitle("A one-line description")
    .Meta("author · date"))

.At(Anchor.TopRight, stack => stack
    .Image("assets/logo.png", height: 56))
```

Rules that make this "just work":

- **Padding:** every anchor is inset 64px from the card edges by default (`.Padding(int)` to change).
- **Alignment is inherited from the anchor.** A `BottomLeft` stack left-aligns its elements and its text; `TopCenter` center-aligns; `MiddleRight` right-aligns. Overridable per stack with `.Align(HorizontalAlign)`.
- **Stacks flow downward** with a default gap of 12px between elements (`.Gap(int)`). Anchoring to a `Bottom*` position positions the *whole measured stack* so its bottom edge sits at the padding line — the user never does math.
- **Max width** of a stack defaults to `cardWidth − 2 × padding`, so text wraps instead of escaping the canvas. Override with `.MaxWidth(int)` — e.g. 60% width for a title that shares the card with right-side art.

### 4. Elements

Stacks contain elements. v1 ships five:

| Element | Signature | Default styling |
|---|---|---|
| `Title` | `.Title(string text)` | 64px, bold (700), white, line-height 1.15, wraps up to 3 lines then shrinks, then ellipsizes |
| `Subtitle` | `.Subtitle(string text)` | 30px, regular (400), white at 85% opacity, line-height 1.35, wraps up to 2 lines |
| `Meta` | `.Meta(string text)` | 22px, medium (500), white at 65% opacity, single line, ellipsized |
| `Text` | `.Text(string text, TextStyle style)` | fully custom — for anything the three roles above don't cover |
| `Image` | `.Image(string path, int? width = null, int? height = null, ImageShape shape = Rect)` | aspect-preserving scale; `ImageShape.Circle` for avatars |
| `Spacer` | `.Spacer(int px)` | extra vertical gap beyond the stack default |

Plus one container:

- **`Row`** — a horizontal group inside a stack, vertically centered, 12px gap. This exists almost entirely for the "avatar + name" pattern:

```csharp
.At(Anchor.BottomLeft, stack => stack
    .Title("Why Your OG Images Look Like Everyone Else's")
    .Row(row => row
        .Image("assets/phil.jpg", height: 44, shape: ImageShape.Circle)
        .Meta("Phil Scott · 9 min read")))
```

The role-based elements (`Title`/`Subtitle`/`Meta`) are the heart of the "good by default" promise: they encode a tested type scale and opacity ramp, so users describe *what the text is*, not what it looks like.

### Styling and themes

Per-element overrides ride on optional parameters; card-wide changes go through a `Theme`:

```csharp
// One-off override
.Title("Hello", color: "#fbbf24")
.Subtitle("World", size: 36)

// Card-wide
.Theme(new Theme
{
    FontFamily = "Inter",                  // resolved via SKFontManager; falls back to the
                                           //   embedded Noto Sans when not installed
    FontPath   = "assets/Inter.ttf",       // optional: load an exact file as one card-wide face
    FontFiles  = ["assets/SpaceGrotesk.ttf"], // register files by name → mix several bundled faces
    TextColor  = "#f8fafc",
    Scale      = 1.1f                      // multiplies the whole type scale
})
```

Colors are accepted as hex strings everywhere (with an implicit conversion to `SKColor`), so casual users never type `new SKColor(...)`.

### 5. Output

```csharp
card.Save("og.png");                     // format inferred from extension: .png, .jpg/.jpeg, .webp
card.Save(stream, ImageFormat.Png);
byte[] bytes = card.ToBytes(ImageFormat.Webp, quality: 90);
using SKImage img = card.ToImage();      // escape hatch for further Skia work
```

- PNG is lossless and the default recommendation for OG images; JPEG/WebP take `quality` (default 90).
- `.Scale(2)` renders at 2× pixel density (2400×1260 for the default card) with all layout values multiplied — for crisp display on high-DPI surfaces.
- Rendering is deferred: nothing rasterizes until `Save`/`ToBytes`/`ToImage`, so the builder is cheap to construct and reuse.

## Text Rendering Details

This is the part SkiaSharp alone gets wrong and the reason HarfBuzzSharp is in the package:

- **Shaping.** All text runs through a HarfBuzz shaper (`SKShaper` from SkiaSharp.HarfBuzz, wrapped to support our wrapping logic). Kerning pairs, ligatures, Arabic/Indic shaping, and combining diacritics all render correctly.
- **Wrapping** is balanced word-wrap on shaped-cluster boundaries, measured with shaped (not per-glyph-advance) widths so the wrap point is honest. Greedy packing fixes the line count (the vertical budget shrink-to-fit depends on); the breaks within that budget are then re-chosen to minimise total raggedness — the score-based "balance" of CSS `text-wrap` — so lines come out near-equal in width and a long title never strands a one-word last line.
- **Shrink-to-fit.** `Title` tries its default size first; if the text exceeds its max line count, it steps the font size down (to a floor of 70% of the default) before falling back to ellipsis on the last line. This keeps long titles on the card without the user thinking about it.
- **Fallback fonts.** When the primary typeface lacks a glyph (emoji, CJK in a Latin face), we fall back per run — first to the bundled Noto Color Emoji and Noto Sans JP faces, then to `SKFontManager` for other scripts — so `"Shipping 🚀 to 東京"` renders rather than tofu-boxing on any machine. Runs are split per resolved typeface before shaping.
- **Bundled fonts.** The assembly embeds a variable Noto Sans (`wght` 100–900), Noto Color Emoji, and Noto Sans JP (400/700) — all OFL-licensed (`Fonts/OFL.txt` ships in the package; ~20 MB embedded). Per-element `Weight` instances the variable face along its `wght` axis, so any weight 100–900 renders distinctly. The default typeface is the embedded Noto Sans on every platform, so a card renders pixel-identically on a dev laptop, CI, or a bare container. Korean/Chinese and other scripts beyond the bundle still resolve from system fonts when present.
- **Baseline math.** Stack layout uses font metrics (ascent/descent), not glyph bounds, so multi-line spacing is stable regardless of which glyphs appear.

## Public API Surface (complete for v1)

```csharp
namespace Ashcroft;

public static class SocialCard
{
    public static CardBuilder Create(int width = 1200, int height = 630);
    public static CardBuilder Create(CardSize size);
}

public sealed class CardBuilder
{
    public CardBuilder Background(string colorOrPath);          // "#rgb"/"#rrggbb"/"#aarrggbb" → color, else file path
    public CardBuilder Background(Stream image);
    public CardBuilder Background(ReadOnlyMemory<byte> image);
    public CardBuilder Background(Action<SKCanvas, SKSizeI> draw);
    public CardBuilder Background(BackgroundFill fill);         // gradients etc. via Backgrounds factory

    public CardBuilder At(Anchor anchor, Action<StackBuilder> content);

    public CardBuilder Theme(Theme theme);
    public CardBuilder Padding(int pixels);                     // default 64
    public CardBuilder Scrim(float opacity);                    // default auto (~0.55 when needed)
    public CardBuilder NoScrim();
    public CardBuilder Scale(float factor);                     // default 1

    public void   Save(string path);
    public void   Save(Stream destination, ImageFormat format, int quality = 90);
    public byte[] ToBytes(ImageFormat format = ImageFormat.Png, int quality = 90);
    public SKImage ToImage();
}

public sealed class StackBuilder
{
    public StackBuilder Title(string text, string? color = null, float? size = null);
    public StackBuilder Subtitle(string text, string? color = null, float? size = null);
    public StackBuilder Meta(string text, string? color = null, float? size = null);
    public StackBuilder Text(string text, TextStyle style);
    public StackBuilder Image(string path, int? width = null, int? height = null,
                              ImageShape shape = ImageShape.Rect, float cornerRadius = 0);
    public StackBuilder Row(Action<StackBuilder> content);      // horizontal flow, vertically centered
    public StackBuilder Spacer(int pixels);

    public StackBuilder Gap(int pixels);                        // default 12
    public StackBuilder MaxWidth(int pixels);                   // default cardWidth − 2·padding
    public StackBuilder Align(HorizontalAlign align);           // default: inherited from anchor
}

public enum Anchor
{
    TopLeft, TopCenter, TopRight,
    MiddleLeft, Center, MiddleRight,
    BottomLeft, BottomCenter, BottomRight,
}

public enum CardSize { OpenGraph, Square, Wide, Story }
public enum ImageFormat { Png, Jpeg, Webp }
public enum ImageShape { Rect, Rounded, Circle }
public enum HorizontalAlign { Left, Center, Right }

public sealed record TextStyle
{
    public string? FontFamily { get; init; }    // null → theme font
    public float   Size { get; init; } = 30;
    public int     Weight { get; init; } = 400; // 100–900; drives the embedded font's wght axis (named fonts: SKFontStyleWeight)
    public string  Color { get; init; } = "#ffffff";
    public float   LineHeight { get; init; } = 1.35f;
    public int     MaxLines { get; init; } = 2;
    public bool    ShrinkToFit { get; init; } = false;
    public float   LetterSpacing { get; init; } = 0;
}

public sealed record Theme
{
    public string  FontFamily { get; init; } = "";   // "" → embedded Noto Sans default
    public string? FontPath   { get; init; }         // a single card-wide face loaded from a file
    public IReadOnlyList<string> FontFiles { get; init; } = []; // files registered by reported family name; a per-element/theme FontFamily resolves to these before the system
    public string  TextColor  { get; init; } = "#ffffff";
    public float   Scale      { get; init; } = 1.0f;
}

public static class Backgrounds
{
    public static BackgroundFill Solid(string color);
    public static BackgroundFill LinearGradient(string from, string to, float angleDegrees = 135);
    public static BackgroundFill RadialGradient(string center, string edge);
}
```

Everything else (`StackElement`, layout engine, shaper, font resolver) is `internal`.

## Worked Examples

**Minimum viable card** — one line of content, all defaults:

```csharp
SocialCard.Create()
    .Background(Backgrounds.LinearGradient("#0f172a", "#1e3a8a"))
    .At(Anchor.Center, s => s.Title("April Release Notes"))
    .Save("release.png");
```

**Blog post card** — the expected common case:

```csharp
SocialCard.Create()
    .Background("hero.jpg")
    .At(Anchor.TopRight, s => s.Image("logo.png", height: 48))
    .At(Anchor.BottomLeft, s => s
        .Title(post.Title)
        .Subtitle(post.Description)
        .Spacer(8)
        .Row(r => r
            .Image(post.AuthorAvatar, height: 44, shape: ImageShape.Circle)
            .Meta($"{post.Author} · {post.Date:MMM yyyy}")))
    .Save($"og/{post.Slug}.png");
```

**Generative background, custom theme:**

```csharp
SocialCard.Create(CardSize.Square)
    .Theme(new Theme { FontFamily = "JetBrains Mono", TextColor = "#a7f3d0" })
    .Background((canvas, size) => DrawIsoGrid(canvas, size))
    .At(Anchor.Center, s => s
        .Title("ashcroft v1.0", size: 88)
        .Meta("dotnet add package Ashcroft"))
    .Save("announce.png");
```

## Layout Algorithm (informative)

Per anchored group, at render time:

1. Resolve effective max width (explicit, or card width minus horizontal padding).
2. Measure each element top-down: shape text, wrap, apply shrink-to-fit, compute image scale. Rows measure children left-to-right and take the max child height.
3. Sum heights + gaps → stack size.
4. Position the stack rectangle against the anchor within the padded content box (e.g. `BottomRight` → right edge at `width − padding`, bottom edge at `height − padding`; `Center` → both axes centered).
5. If the group contains text and the background warrants a scrim, draw the scrim gradient for that anchor's region first.
6. Draw elements with the inherited or explicit horizontal alignment.

Groups render in the order they were added; later groups draw on top. No constraint solving, no collision handling — overlap is the user's choice.

## Error Handling

- Missing background/element image file → `FileNotFoundException` at render time with the offending path in the message.
- Unresolvable font family → silent fallback to the embedded Noto Sans default (design tools warn; libraries shouldn't crash a build pipeline over a font), but the resolved family is exposed for diagnostics via `AshcroftDiagnostics` logging hooks (v1: simple `Action<string>` you can attach).
- Empty card (no content groups) renders the background alone — valid, not an error.
- Text that cannot fit even after shrink + ellipsis (pathological `MaxWidth`) renders its first line ellipsized; never throws.

## Project Layout

```
ashcroft/
├── spec.md
├── src/
│   └── Ashcroft/
│       ├── Ashcroft.csproj            # net10.0; SkiaSharp, SkiaSharp.HarfBuzz
│       ├── SocialCard.cs              # entry point
│       ├── CardBuilder.cs
│       ├── StackBuilder.cs
│       ├── Theme.cs / TextStyle.cs / enums
│       ├── Backgrounds.cs
│       └── Internal/
│           ├── LayoutEngine.cs
│           ├── TextShaper.cs          # HarfBuzz shaping, wrapping, shrink-to-fit, fallback runs
│           ├── FontResolver.cs
│           └── ScrimPainter.cs
└── tests/
    └── Ashcroft.Tests/
        ├── LayoutTests.cs             # pure measurement/positioning assertions
        ├── TextShapingTests.cs        # wrap points, shrink, ellipsis, fallback runs
        └── SnapshotTests.cs           # render → compare against approved PNGs (Verify.SkiaSharp or pixel-diff with tolerance)
```

Native asset note: the package depends on `SkiaSharp.NativeAssets.*` transitively; Linux consumers (CI, Docker) need `SkiaSharp.NativeAssets.Linux.NoDependencies` — document this prominently in the README, since it is the #1 support question for every Skia-based library.

## Open Questions

1. **Bundle a default font?** Embedding an OFL font (e.g. Inter) guarantees identical output across OSes — valuable for snapshot tests and Docker — at ~300KB package cost. Leaning **yes**, as an opt-in `Ashcroft.DefaultFont` companion package, with system-font resolution remaining the in-box default.
2. **`Row` nesting depth** — v1 allows `Row` only as a direct child of a stack (no rows in rows). Revisit if real use demands it.
3. **Async I/O** — file reads are synchronous in v1 (cards are built in build pipelines and request handlers where sync Skia work dominates anyway). `SaveAsync` can be added without breaking changes.
