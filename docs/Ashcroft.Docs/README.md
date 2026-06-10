# Ashcroft doc site (Pennington POC)

A single-page, bare-host [Pennington](https://usepennington.github.io/pennington/) site that
documents Ashcroft — and exercises the library end-to-end. Every code sample is pulled from real
source via a `:symbol` embed, and every image beneath it is rendered by *running that same sample
through Ashcroft*.

## How the live images work

`Samples/CardSamples.cs` holds the worked examples (each returns a deferred `CardBuilder`).
`Rendering/SampleGallery.cs` is the one registry both consumers share:

- **Dev server** — `MapGet("/samples/{id}.png")` runs the sample and returns the PNG on request.
- **Static build** — `SampleImageEmitter : IContentEmitter` returns one `ContentToCreate` per
  sample, so `dotnet run -- build` writes `output/samples/{id}.png`.

Same registry → the served image and the emitted file can't diverge. `Samples/CardSamples.cs` is
also what the page's `csharp:symbol` fences display, so the code shown is the code run.

## Run it

```sh
dotnet run                 # dev server with live reload — open the printed URL
dotnet run -- build        # static site to ./output (index.html + samples/*.png)
dotnet run -- diag warnings # link / xref / :symbol health check
```

Requires the .NET 11 SDK (pinned in `global.json`). References the Ashcroft library at
`../../src/Ashcroft`. Sample input images (`assets/`) are generated on first startup.
