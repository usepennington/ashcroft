using SkiaSharp;

namespace Ashcroft.Internal;

/// <summary>
/// Decodes images once and caches them for the duration of a render pass (layout measures them,
/// the renderer draws them). A missing file surfaces as <see cref="FileNotFoundException"/> with the
/// offending path, per spec.
/// </summary>
internal sealed class ImageLoader : IDisposable
{
    private readonly Dictionary<string, SKImage> _byPath = new();
    private readonly List<SKImage> _owned = new();

    public SKImage FromPath(string path)
    {
        if (_byPath.TryGetValue(path, out var cached))
            return cached;

        if (!File.Exists(path))
            throw new FileNotFoundException($"Image not found: {path}", path);

        using var data = SKData.Create(path);
        var image = data is not null ? SKImage.FromEncodedData(data) : null;
        if (image is null)
            throw new InvalidOperationException($"Image could not be decoded: {path}");

        _byPath[path] = image;
        _owned.Add(image);
        return image;
    }

    public SKImage FromBytes(ReadOnlyMemory<byte> bytes)
    {
        using var data = SKData.CreateCopy(bytes.ToArray());
        var image = SKImage.FromEncodedData(data)
                    ?? throw new InvalidOperationException("Background image bytes could not be decoded.");
        _owned.Add(image);
        return image;
    }

    public void Dispose()
    {
        foreach (var image in _owned)
            image.Dispose();
        _owned.Clear();
        _byPath.Clear();
    }
}
