using Bgfx.Net;
using Tralys.Ui.Bgfx;
using Tralys.Ui.Rendering;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Gallery.Textures;

/// <summary>An image file with a texture of its own, its size known from the header. Its pixels load when it is first
/// drawn.</summary>
sealed unsafe class FileTexture(AssetTextures owner, string path, ImageHeader header)
    : RenderTexture,
        IBgfxTexture,
        IImageLoad,
        IDisposable
{
    static readonly TextureHandle Invalid = new(ushort.MaxValue);

    TextureHandle _handle = Invalid;
    bool _requested;

    public override int Width => header.Width;

    public override int Height => header.Height;

    public override bool IsPremultiplied => !header.IsDds;

    public string Path => path;

    public TextureHandle Handle
    {
        get
        {
            if (!_handle.Valid && !_requested)
            {
                _requested = true;
                owner.Request(this);
            }
            return _handle;
        }
    }

    /// <summary>Reads, decodes and uploads the file; a file that fails to stays undrawn.</summary>
    public void Load()
    {
        if (_handle.Valid)
            return;

        var bytes = File.ReadAllBytes(path);
        if (header.IsDds)
        {
            _handle = Bx.CreateTexture(Bx.Copy<byte>(bytes), AssetTextures.Sampling, 0, null);
        }
        else
        {
            var image = header.Decode(bytes);
            _handle = BgfxImage.CreateTexture(image.Width, image.Height, image.Data);
        }

        if (!_handle.Valid)
            throw new InvalidDataException($"bgfx could not create a texture from {path}.");
    }

    public void Dispose()
    {
        _requested = true;
        if (!_handle.Valid)
            return;

        Bx.DestroyTexture(_handle);
        _handle = Invalid;
    }
}
