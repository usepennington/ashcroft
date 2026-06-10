using Ashcroft.Docs.Components;
using Ashcroft.Docs.Rendering;
using Pennington.Content;
using Pennington.FrontMatter;
using Pennington.Infrastructure;
using Pennington.MonorailCss;
using Pennington.Routing;
using Pennington.TreeSitter;

var builder = WebApplication.CreateBuilder(args);

// The generated sample inputs (assets/hero.png etc.) are gitignored, so a fresh checkout
// (CI included) must create them before any sample renders.
SampleAssets.EnsureInputs(builder.Environment.ContentRootPath);

// Core Pennington: parser, renderer, highlighting, sitemap, llms.txt, plus the Markdown source
// that discovers Content/index.md and gives it the route "/".
builder.Services.AddPennington(penn =>
{
    penn.SiteTitle = "Ashcroft";
    penn.SiteDescription = "Social card generation for .NET";
    penn.ContentRootPath = new FilePath("Content");
    penn.AddMarkdownContent<DocFrontMatter>(md =>
    {
        md.ContentPath = "Content";
        md.BasePageUrl = "/";
    });
});

// Styling + syntax theme, served at /styles.css. Rendered-Markdown typography is a prose
// customization (AshcroftStyles.ExtendProse) layered over Pennington's defaults; the page
// tokens and the JS / code-bar chrome that utilities can't reach come from AshcroftStyles.Css.
builder.Services.AddMonorailCss(_ => new MonorailCssOptions
{
    ColorScheme = new AlgorithmicColorScheme { PrimaryHue = 259, Chroma = 0.14 },
    SyntaxTheme = SyntaxTheme.Default,
    ExtendProseCustomization = AshcroftStyles.ExtendProse,
    ExtraStyles = AshcroftStyles.Css,
});

// Pulls live C# source into `csharp:symbol` fences so the code on the page can't drift.
builder.Services.AddTreeSitter(o => o.ContentRoot = builder.Environment.ContentRootPath);

// Surfaces each sample's PNG route to discovery so the static-build crawler fetches it
// through the same MapGet below that serves the dev server.
builder.Services.AddSingleton<IContentService, SampleImageContentService>();

// Markdown pages render through Blazor static SSR: MarkdownPage.razor catches the URL
// and asks IPageResolver for the RenderedItem.
builder.Services.AddRazorComponents();

var app = builder.Build();

app.UsePennington();          // must precede route mapping (sitemap/llms/redirects)
app.UseMonorailCss();

// Sample images, dev and build alike: run the referenced sample through Ashcroft and return
// the PNG. SampleImageContentService discovers these routes, so the static-build crawler
// fetches this endpoint too — one rendering path always.
app.MapGet("/samples/{id}.png", (string id) =>
    SampleGallery.Samples.ContainsKey(id)
        ? Results.Bytes(SampleGallery.Render(id), "image/png")
        : Results.NotFound());

// Content pages route through Blazor: MarkdownPage.razor's catch-all resolves the URL
// to a RenderedItem via IPageResolver. The /samples MapGet above is more specific, so
// it wins over the catch-all.
app.UseAntiforgery();
app.MapRazorComponents<App>();

// `dotnet run` serves; `dotnet run -- build [baseUrl] [outDir]` writes the static site.
await app.RunOrBuildAsync(args);
