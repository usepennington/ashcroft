using System.Collections.Immutable;
using Pennington.Content;
using Pennington.Pipeline;
using Pennington.Routing;

namespace Ashcroft.Docs.Rendering;

/// <summary>
/// Surfaces each sample's PNG route to Pennington discovery. Pairing the route with
/// <see cref="EndpointSource"/> makes the static-build crawler fetch <c>/samples/{id}.png</c>
/// over HTTP from the running host — the same <c>MapGet</c> the dev server answers — so the
/// served image and the emitted file always flow through one code path.
/// </summary>
public sealed class SampleImageContentService : IContentService
{
    public string DefaultSectionLabel => "Samples";
    public int SearchPriority => 0;

    public async IAsyncEnumerable<DiscoveredItem> DiscoverAsync()
    {
        foreach (var id in SampleGallery.Samples.Keys)
        {
            // OutputFile is pinned explicitly: FromUrl documents directory-style URLs
            // (…/index.html), not extension-bearing ones.
            var route = ContentRouteFactory.FromUrl(new UrlPath($"/samples/{id}.png")) with
            {
                OutputFile = new FilePath($"samples/{id}.png"),
            };
            yield return new DiscoveredItem(route, new EndpointSource());
        }

        await Task.CompletedTask;
    }

    /// <summary>Nothing to copy or generate — the crawler writes each route's HTTP response.</summary>
    public Task<ImmutableList<ContentToCopy>> GetContentToCopyAsync()
        => Task.FromResult(ImmutableList<ContentToCopy>.Empty);

    public Task<ImmutableList<ContentToCreate>> GetContentToCreateAsync()
        => Task.FromResult(ImmutableList<ContentToCreate>.Empty);

    /// <summary>Images carry no navigation or search presence.</summary>
    public Task<ImmutableList<ContentTocItem>> GetContentTocEntriesAsync()
        => Task.FromResult(ImmutableList<ContentTocItem>.Empty);

    public Task<ImmutableList<CrossReference>> GetCrossReferencesAsync()
        => Task.FromResult(ImmutableList<CrossReference>.Empty);
}
