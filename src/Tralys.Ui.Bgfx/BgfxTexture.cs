using System.Numerics;
using Bgfx.Net;
using Tralys.Ui.Rendering;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Bgfx;

/// <summary>A <see cref="RenderTexture"/> <see cref="BgfxViewRenderer"/> can bind.</summary>
public interface IBgfxTexture
{
    /// <summary>Read once per frame by each batch drawing the texture. While it is invalid, such as while the pixels
    /// load or after they were evicted, the batch is skipped; an adapter over evicted pixels asks for them again here.</summary>
    [FramePath]
    TextureHandle Handle { get; }
}

static unsafe class BgfxTexels
{
    public static TextureHandle CreateZeroed(
        int width,
        int height,
        ushort layers,
        TextureFormat format,
        int bytesPerTexel,
        bool hasMips = false
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var handle = Bx.CreateTexture2d(
            checked((ushort)width),
            checked((ushort)height),
            hasMips,
            layers,
            format,
            (ulong)(SamplerFlags.UClamp | SamplerFlags.VClamp),
            null,
            0
        );
        if (!handle.Valid)
            throw new InvalidOperationException(
                $"bgfx could not create a {width}x{height}x{layers} {format} texture."
            );

        // bgfx makes a texture created with memory immutable, so the zeros go in as the first updates.
        var levels = hasMips ? MipLevels(width, height) : 1;
        for (ushort layer = 0; layer < layers; layer++)
        {
            for (byte level = 0; level < levels; level++)
            {
                var levelWidth = Math.Max(width >> level, 1);
                var levelHeight = Math.Max(height >> level, 1);
                var stride = levelWidth * bytesPerTexel;
                var zeros = Bx.Alloc(checked((uint)(stride * levelHeight)));
                new Span<byte>(zeros->Data, (int)zeros->Size).Clear();
                Bx.UpdateTexture2d(
                    handle,
                    layer,
                    level,
                    0,
                    0,
                    (ushort)levelWidth,
                    (ushort)levelHeight,
                    zeros,
                    checked((ushort)stride)
                );
            }
        }
        return handle;
    }

    public static int MipLevels(int width, int height) =>
        1 + BitOperations.Log2((uint)Math.Max(width, height));

    public static void ThrowUnlessImage(int width, int height, int stride, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(stride, width * 4);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, stride * height);
    }

    public static void Premultiply(
        ReadOnlySpan<byte> straight,
        int stride,
        int width,
        int height,
        byte* to
    )
    {
        for (var y = 0; y < height; y++)
        {
            var row = straight.Slice(y * stride, width * 4);
            for (var x = 0; x < row.Length; x += 4)
            {
                int alpha = row[x + 3];
                *to++ = (byte)((row[x] * alpha + 127) / 255);
                *to++ = (byte)((row[x + 1] * alpha + 127) / 255);
                *to++ = (byte)((row[x + 2] * alpha + 127) / 255);
                *to++ = (byte)alpha;
            }
        }
    }

    // Each texel averages the share of the level above that it covers, so an odd size shrinks without shifting the image.
    public static void ShrinkRgba(
        byte* from,
        int fromWidth,
        int fromHeight,
        byte* to,
        int toWidth,
        int toHeight
    )
    {
        var total = (long)fromWidth * fromHeight;
        for (var y = 0; y < toHeight; y++)
        {
            var top = y * fromHeight;
            var bottom = top + fromHeight;
            for (var x = 0; x < toWidth; x++)
            {
                var left = x * fromWidth;
                var right = left + fromWidth;
                long r = 0,
                    g = 0,
                    b = 0,
                    a = 0;
                for (var fromY = top / toHeight; fromY * toHeight < bottom; fromY++)
                {
                    var tall =
                        Math.Min(bottom, (fromY + 1) * toHeight) - Math.Max(top, fromY * toHeight);
                    for (var fromX = left / toWidth; fromX * toWidth < right; fromX++)
                    {
                        var wide =
                            Math.Min(right, (fromX + 1) * toWidth)
                            - Math.Max(left, fromX * toWidth);
                        var weight = (long)wide * tall;
                        var texel = from + (fromY * fromWidth + fromX) * 4;
                        r += texel[0] * weight;
                        g += texel[1] * weight;
                        b += texel[2] * weight;
                        a += texel[3] * weight;
                    }
                }
                *to++ = (byte)((r + total / 2) / total);
                *to++ = (byte)((g + total / 2) / total);
                *to++ = (byte)((b + total / 2) / total);
                *to++ = (byte)((a + total / 2) / total);
            }
        }
    }

    [FramePath]
    public static void Write(
        RenderTexture texture,
        TextureHandle handle,
        ushort layer,
        int bytesPerTexel,
        int x,
        int y,
        int width,
        int height,
        ReadOnlySpan<byte> texels,
        int stride
    )
    {
        ObjectDisposedException.ThrowIf(!handle.Valid, texture);
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x + width, texture.Width);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(y + height, texture.Height);
        ArgumentOutOfRangeException.ThrowIfLessThan(stride, width * bytesPerTexel);
        ArgumentOutOfRangeException.ThrowIfLessThan(texels.Length, stride * height);

        Bx.UpdateTexture2d(
            handle,
            layer,
            0,
            (ushort)x,
            (ushort)y,
            (ushort)width,
            (ushort)height,
            Bx.Copy(texels[..(stride * height)]),
            checked((ushort)stride)
        );
    }

    [FramePath]
    public static void Destroy(ref TextureHandle handle)
    {
        if (!handle.Valid)
            return;

        Bx.DestroyTexture(handle);
        handle = new TextureHandle(ushort.MaxValue);
    }
}

/// <summary>A clamped, bilinear R8 texture whose texels are written after it is created.</summary>
public sealed class BgfxWritableTexture : WritableTexture, IBgfxTexture
{
    const int BytesPerTexel = 1;

    readonly int _width;
    readonly int _height;
    TextureHandle _handle;

    public BgfxWritableTexture(int width, int height)
    {
        _width = width;
        _height = height;
        _handle = BgfxTexels.CreateZeroed(width, height, 1, TextureFormat.R8, BytesPerTexel);
    }

    public override int Width => _width;

    public override int Height => _height;

    public TextureHandle Handle => _handle;

    public override void Write(
        int x,
        int y,
        int width,
        int height,
        ReadOnlySpan<byte> texels,
        int stride
    ) => BgfxTexels.Write(this, _handle, 0, BytesPerTexel, x, y, width, height, texels, stride);

    public override void Dispose() => BgfxTexels.Destroy(ref _handle);
}

/// <summary>Uploads decoded images with the mip levels that let them shrink without stair-stepping.</summary>
public static unsafe class BgfxImage
{
    /// <summary>A clamped, bilinear RGBA8 texture with every mip level of <paramref name="straightRgba"/>, tightly packed
    /// straight-alpha texels. Its texels are stored premultiplied, so the <see cref="RenderTexture"/> over it reports
    /// <see cref="RenderTexture.IsPremultiplied"/>. The caller destroys it.</summary>
    /// <exception cref="InvalidOperationException">bgfx could not create it.</exception>
    public static TextureHandle CreateTexture(
        int width,
        int height,
        ReadOnlySpan<byte> straightRgba
    )
    {
        BgfxTexels.ThrowUnlessImage(width, height, width * 4, straightRgba.Length);

        var bytes = 0L;
        var levels = BgfxTexels.MipLevels(width, height);
        for (var level = 0; level < levels; level++)
            bytes += (long)Math.Max(width >> level, 1) * Math.Max(height >> level, 1) * 4;

        var chain = Bx.Alloc(checked((uint)bytes));
        var at = chain->Data;
        BgfxTexels.Premultiply(straightRgba, width * 4, width, height, at);
        for (var level = 1; level < levels; level++)
        {
            var fromWidth = Math.Max(width >> (level - 1), 1);
            var fromHeight = Math.Max(height >> (level - 1), 1);
            var next = at + (long)fromWidth * fromHeight * 4;
            BgfxTexels.ShrinkRgba(
                at,
                fromWidth,
                fromHeight,
                next,
                Math.Max(width >> level, 1),
                Math.Max(height >> level, 1)
            );
            at = next;
        }

        var handle = Bx.CreateTexture2d(
            checked((ushort)width),
            checked((ushort)height),
            true,
            1,
            TextureFormat.RGBA8,
            (ulong)(SamplerFlags.UClamp | SamplerFlags.VClamp),
            chain,
            0
        );
        if (!handle.Valid)
            throw new InvalidOperationException(
                $"bgfx could not create a {width}x{height} RGBA8 texture."
            );
        return handle;
    }
}

/// <summary>A clamped, bilinear RGBA8 texture array with mip levels, whose layers are written whole after it is created.
/// They are written straight-alpha and stored premultiplied.</summary>
public sealed unsafe class BgfxTextureArray : RenderTextureArray, IBgfxTexture, IDisposable
{
    // Direct3D 11 views a one-layer texture as plain 2D, which an array sampler cannot read.
    internal const ushort MinLayers = 2;
    const int BytesPerTexel = 4;

    readonly int _width;
    readonly int _height;
    readonly int _layers;
    TextureHandle _handle;

    public BgfxTextureArray(int width, int height, int layers)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(layers);
        _width = width;
        _height = height;
        _layers = layers;
        _handle = BgfxTexels.CreateZeroed(
            width,
            height,
            Math.Max(checked((ushort)layers), MinLayers),
            TextureFormat.RGBA8,
            BytesPerTexel,
            hasMips: true
        );
    }

    public override int Width => _width;

    public override int Height => _height;

    public override int LayerCount => _layers;

    public override bool IsPremultiplied => true;

    public TextureHandle Handle => _handle;

    /// <summary>Replaces <paramref name="layer"/>'s image and its mip levels with the straight-alpha
    /// <paramref name="texels"/>, <paramref name="stride"/> bytes per row; they draw from the next frame submitted.</summary>
    public void Write(int layer, ReadOnlySpan<byte> texels, int stride)
    {
        ObjectDisposedException.ThrowIf(!_handle.Valid, this);
        ArgumentOutOfRangeException.ThrowIfNegative(layer);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(layer, _layers);
        BgfxTexels.ThrowUnlessImage(_width, _height, stride, texels.Length);

        var image = Bx.Alloc(checked((uint)(_width * _height * BytesPerTexel)));
        BgfxTexels.Premultiply(texels, stride, _width, _height, image->Data);
        var levels = BgfxTexels.MipLevels(_width, _height);
        for (byte level = 0; level < levels; level++)
        {
            var width = Math.Max(_width >> level, 1);
            var height = Math.Max(_height >> level, 1);
            Memory* next = null;
            if (level + 1 < levels)
            {
                var nextWidth = Math.Max(_width >> (level + 1), 1);
                var nextHeight = Math.Max(_height >> (level + 1), 1);
                next = Bx.Alloc(checked((uint)(nextWidth * nextHeight * BytesPerTexel)));
                BgfxTexels.ShrinkRgba(
                    image->Data,
                    width,
                    height,
                    next->Data,
                    nextWidth,
                    nextHeight
                );
            }
            Bx.UpdateTexture2d(
                _handle,
                (ushort)layer,
                level,
                0,
                0,
                (ushort)width,
                (ushort)height,
                image,
                checked((ushort)(width * BytesPerTexel))
            );
            image = next;
        }
    }

    public void Dispose() => BgfxTexels.Destroy(ref _handle);
}

/// <summary>Creates <see cref="BgfxWritableTexture"/>s on the bgfx device.</summary>
public sealed class BgfxTextureFactory : ITextureFactory
{
    public WritableTexture CreateWritable(int width, int height) =>
        new BgfxWritableTexture(width, height);
}
