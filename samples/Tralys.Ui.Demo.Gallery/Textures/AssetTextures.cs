using Bgfx.Net;
using Tralys.Ui.Bgfx;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Gallery.Textures;

/// <summary>The images in the gallery's <c>Assets</c> folder, by their path under it. Resolving reads only the file's
/// header; <see cref="Pump"/>, which the host calls each frame, loads the pixels a few images at a time. Icon-sized
/// images share a <see cref="BgfxTextureArray"/> per size; the rest, DDS included, get a texture each.</summary>
public sealed class AssetTextures : ITextureProvider, IDisposable
{
    internal const ulong Sampling = (ulong)(SamplerFlags.UClamp | SamplerFlags.VClamp);

    const int LoadsPerFrame = 8;
    const int IconBytes = 64 * 1024;
    const int FirstArrayLayers = 16;
    const int MaxArrayLayers = 1024;
    const int ArrayGrowth = 4;

    static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Assets");

    readonly Dictionary<string, RenderTexture?> _byPath = new(StringComparer.Ordinal);
    readonly Dictionary<(int Width, int Height), IconPool> _icons = [];
    readonly List<FileTexture> _files = [];
    readonly Queue<IImageLoad> _pending = new();

    public RenderTexture? Resolve(string source)
    {
        if (Locate(source) is not { } path)
            return null;
        if (_byPath.TryGetValue(path, out var known))
            return known;

        var texture = Open(path);
        _byPath.Add(path, texture);
        return texture;
    }

    /// <summary>Loads the pixels of up to a few of the images asked for since the last call.</summary>
    [FramePath]
    public void Pump()
    {
        for (var i = 0; i < LoadsPerFrame && _pending.TryDequeue(out var load); i++)
            Run(load);
    }

    internal void Request(FileTexture texture) => _pending.Enqueue(texture);

    /// <summary>Whether an image asked for still waits for <see cref="Pump"/> to load its pixels.</summary>
    public bool IsLoading => _pending.Count > 0;

    public void Dispose()
    {
        _pending.Clear();
        foreach (var file in _files)
            file.Dispose();
        foreach (var pool in _icons.Values)
        {
            foreach (var array in pool.Arrays)
                array.Dispose();
        }
    }

    /// <summary>The file under the <c>Assets</c> folder that <paramref name="key"/> names, if one exists.</summary>
    public static string? Locate(string key)
    {
        var path = Path.GetFullPath(Path.Combine(Root, key.Trim()));
        return File.Exists(path) ? path : null;
    }

    RenderTexture? Open(string path)
    {
        if (!ImageHeader.TryRead(path, out var header))
        {
            Console.Error.WriteLine($"{path} is not an image the gallery reads.");
            return null;
        }

        if (header.IsDds || header.Width * header.Height * 4 > IconBytes)
        {
            var file = new FileTexture(this, path, header);
            _files.Add(file);
            return file;
        }

        var (array, layer) = AllocateIcon(header.Width, header.Height);
        _pending.Enqueue(new IconLoad(path, header, array, layer));
        return new RenderTextureLayer(array, layer);
    }

    (BgfxTextureArray Array, int Layer) AllocateIcon(int width, int height)
    {
        if (!_icons.TryGetValue((width, height), out var pool))
            _icons.Add((width, height), pool = new IconPool());

        var arrays = pool.Arrays;
        if (arrays.Count == 0 || pool.Used == arrays[^1].LayerCount)
        {
            var layers =
                arrays.Count == 0
                    ? FirstArrayLayers
                    : Math.Min(arrays[^1].LayerCount * ArrayGrowth, MaxArrayLayers);
            arrays.Add(new BgfxTextureArray(width, height, layers));
            pool.Used = 0;
        }
        return (arrays[^1], pool.Used++);
    }

    static void Run(IImageLoad load)
    {
        try
        {
            load.Load();
        }
        catch (Exception e)
            when (e
                    is IOException
                        or InvalidDataException
                        or InvalidOperationException
                        or ArgumentException
            )
        {
            var error = Console.Error;
            error.Write("Could not load ");
            error.Write(load.Path);
            error.Write(": ");
            error.WriteLine(e.Message);
        }
    }

    sealed class IconPool
    {
        public readonly List<BgfxTextureArray> Arrays = [];
        public int Used;
    }

    sealed class IconLoad(string path, ImageHeader header, BgfxTextureArray array, int layer)
        : IImageLoad
    {
        public string Path => path;

        public void Load()
        {
            var image = header.Decode(File.ReadAllBytes(path));
            array.Write(layer, image.Data, image.Width * 4);
        }
    }
}

/// <summary>An image's pixels, still to load.</summary>
interface IImageLoad
{
    string Path { get; }

    void Load();
}
