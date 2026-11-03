using Bgfx.Net;
using Tralys.Ui.Rendering;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Bgfx.Tests;

/// <summary>A texture created from pixels and owned by this object.</summary>
sealed unsafe class BgfxTexture2D : RenderTexture, IBgfxTexture, IDisposable
{
    readonly int _width;
    readonly int _height;
    readonly bool _premultiplied;
    TextureHandle _handle;

    BgfxTexture2D(int width, int height, bool premultiplied, TextureHandle handle)
    {
        _width = width;
        _height = height;
        _premultiplied = premultiplied;
        _handle = handle;
    }

    public override int Width => _width;

    public override int Height => _height;

    public override bool IsPremultiplied => _premultiplied;

    public TextureHandle Handle => _handle;

    /// <summary>A texture of <paramref name="pixels"/>, tightly packed in <paramref name="format"/>, sampled as
    /// <paramref name="flags"/> say: clamped and bilinear unless given others.</summary>
    public static BgfxTexture2D Create(
        int width,
        int height,
        TextureFormat format,
        ReadOnlySpan<byte> pixels,
        bool premultiplied = false,
        ulong flags = (ulong)(SamplerFlags.UClamp | SamplerFlags.VClamp)
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var handle = Bx.CreateTexture2d(
            checked((ushort)width),
            checked((ushort)height),
            false,
            1,
            format,
            flags,
            Bx.Copy(pixels),
            0
        );
        if (!handle.Valid)
            throw new InvalidOperationException(
                $"bgfx could not create a {width}x{height} {format} texture."
            );

        return new BgfxTexture2D(width, height, premultiplied, handle);
    }

    /// <summary>A texture of the straight-alpha RGBA8 <paramref name="pixels"/>, with its mip levels.</summary>
    public static BgfxTexture2D CreateMipmapped(int width, int height, ReadOnlySpan<byte> pixels) =>
        new(width, height, true, BgfxImage.CreateTexture(width, height, pixels));

    public void Dispose()
    {
        if (!_handle.Valid)
            return;

        Bx.DestroyTexture(_handle);
        _handle = new TextureHandle(ushort.MaxValue);
    }
}
