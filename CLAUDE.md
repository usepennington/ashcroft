# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Ashcroft is a .NET 10 library (NuGet package) that generates social cards (Open Graph images, blog headers) from a fluent C# API, rendering with SkiaSharp and shaping all text with HarfBuzz. `spec.md` is the complete v1 specification — public API surface, layout algorithm, error-handling contract — and is the source of truth when behavior is in question.

## Commands

```sh
dotnet build Ashcroft.slnx
dotnet test  Ashcroft.slnx
dotnet test tests/Ashcroft.Tests --filter "FullyQualifiedName~SnapshotTests"   # one class
dotnet test tests/Ashcroft.Tests --filter "Minimum_viable_card"               # one test
```

Library and tests pin SDK 10.0.300 via the root `global.json`. The docs site is separate: it requires a **.NET 11 preview SDK** (its own `docs/Ashcroft.Docs/global.json`) and runs from that directory:

```sh
cd docs/Ashcroft.Docs
dotnet run                  # dev server with live reload
dotnet run -- build         # static site to ./output
dotnet run -- diag warnings # link / xref / :symbol health check
```

## Snapshot tests

`SnapshotTests` are self-seeding: the first run on a machine writes baselines to `tests/Ashcroft.Tests/Approved/` and passes; later runs pixel-diff against them (mean per-channel tolerance 2.0). Default text renders from the embedded Noto fonts (deterministic), but anti-aliasing and non-bundled-script fallback can still differ per machine, so baselines stay gitignored-by-intent — after an intentional visual change, delete `Approved/` and re-run to re-seed. A snapshot failure after a layout/text change may be the expected result of your change, not a bug.

On Linux/CI, SkiaSharp needs `SkiaSharp.NativeAssets.Linux.NoDependencies` (the test csproj adds it conditionally); a `DllNotFoundException` for libSkiaSharp means that package is missing.

## Architecture

**Deferred rendering.** `CardBuilder` / `StackBuilder` (public, in `src/Ashcroft/`) only accumulate state — backgrounds become `BackgroundSource` subclasses, fluent calls become `StackElement` subclasses (`TextElement`, `ImageElement`, `RowElement`, `SpacerElement`). Nothing touches Skia until `Save`/`ToBytes`/`ToImage`, which invoke the render pass.

**Render pass** (`Internal/CardRenderer.cs`): create a surface (pre-scaled by the pixel-density factor, so everything downstream works in logical units) → paint the background → `LayoutEngine` measures and positions each anchored stack into `Placed*` records → for each group containing text over an image/lambda background, `ScrimPainter` draws the legibility gradient first → draw elements. Groups render in add-order; no collision handling by design.

**Text** (`Internal/TextShaper.cs`) is the heart of the library: HarfBuzz shaping, greedy wrap on shaped-cluster boundaries, shrink-to-fit (Title steps down to 70% before ellipsizing), and per-run typeface fallback for emoji/CJK. Measurement and drawing share the same shaped glyphs — never measure text any other way. `FontResolver` defaults to the embedded Noto Sans (`Internal/EmbeddedFonts.cs` — bundled Noto Sans/Color Emoji/Sans JP, OFL) so output is machine-independent; a requested family that isn't installed silently falls back to it, reported via `AshcroftDiagnostics`; fonts must not throw. Embedded faces are process-wide singletons — never dispose them.

**Conventions that carry the design:**
- Everything under `Internal/` stays `internal` (tests reach it via `InternalsVisibleTo`). The public API surface is exactly what `spec.md` lists — additions are a spec change first.
- Colors are hex strings everywhere in the public API, parsed by `Internal/Color.cs`; users never construct `SKColor`.
- `Title`/`Subtitle`/`Meta` get their type scale and opacity ramp (1.0 / 0.85 / 0.65) from `Internal/StackElement.cs` (`Roles`) — tune defaults there, not at call sites.
- Errors follow the spec contract: missing image files throw `FileNotFoundException` with the path at render time; unfittable text ellipsizes rather than throwing; empty cards are valid.

**Docs site** (`docs/Ashcroft.Docs/`) is a Pennington site that exercises the library end-to-end: `Samples/CardSamples.cs` holds the worked examples, `Rendering/SampleGallery.cs` is the single registry used both by the dev server (`/samples/{id}.png` renders on request) and the static build (`SampleImageEmitter` writes the PNGs). The page's `csharp:symbol` fences embed the same source, so code shown, code run, and image emitted cannot diverge — when changing a sample, change it only in `CardSamples.cs`.
