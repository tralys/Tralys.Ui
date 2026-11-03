using Bgfx.Net;
using Tralys.Ui.Rendering;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Bgfx;

/// <summary>An offscreen RGBA8 colour target, clamped and bilinear when sampled. A view draws into it through
/// <see cref="FrameBuffer"/>, and an image draws it as a texture. Its size is fixed: another size takes a new target,
/// and a new source for the images showing it.</summary>
public sealed unsafe class BgfxRenderTarget : RenderTexture, IBgfxTexture, IDisposable
{
    readonly bool _premultiplied = true;
    TextureHandle _color;
    TextureHandle _depth;
    int _width;
    int _height;

    /// <param name="width">The width in pixels, up to 65535.</param>
    /// <param name="height">The height in pixels, up to 65535.</param>
    /// <param name="mips">Whether to keep a mip chain for sampling it smaller than it is, such as on a distant surface.
    /// bgfx rebuilds the chain when the next view drawing into another target runs, as the view sampling it does.</param>
    /// <param name="depth">Whether to attach a depth and stencil buffer, for a 3D scene drawn into it. A view draws without
    /// one.</param>
    /// <param name="premultiplied">Whether its colour is premultiplied, as a view draws it; false for a scene drawn with
    /// straight alpha.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is not from 1 to 65535.</exception>
    /// <exception cref="InvalidOperationException">bgfx could not create the target.</exception>
    public BgfxRenderTarget(
        int width,
        int height,
        bool mips = false,
        bool depth = false,
        bool premultiplied = true
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, ushort.MaxValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, ushort.MaxValue);
        _premultiplied = premultiplied;
        if (!TryOpen((ushort)width, (ushort)height, mips, depth))
            throw new InvalidOperationException(
                $"bgfx could not create a {width}x{height} render target."
            );
    }

    BgfxRenderTarget() { }

    public override int Width => _width;

    public override int Height => _height;

    public override bool IsPremultiplied => _premultiplied;

    public TextureHandle Handle => _color;

    public FrameBufferHandle FrameBuffer { get; private set; }

    /// <summary>The colour attachment, to sample or to blit from.</summary>
    public TextureHandle Color => _color;

    [FramePath]
    internal static BgfxRenderTarget? TryCreate(ushort width, ushort height)
    {
        var target = new BgfxRenderTarget();
        return target.TryOpen(width, height, false, false) ? target : null;
    }

    [FramePath]
    bool TryOpen(ushort width, ushort height, bool mips, bool depth)
    {
        _width = width;
        _height = height;
        _color = Bx.CreateTexture2d(
            width,
            height,
            mips,
            1,
            TextureFormat.RGBA8,
            (ulong)TextureFlags.Rt | (ulong)(SamplerFlags.UClamp | SamplerFlags.VClamp),
            null,
            0
        );

        _depth = depth
            ? Bx.CreateTexture2d(
                width,
                height,
                false,
                1,
                TextureFormat.D24S8,
                (ulong)TextureFlags.RtWriteOnly,
                null,
                0
            )
            : new TextureHandle(ushort.MaxValue);

        var frameBuffer = new FrameBufferHandle(ushort.MaxValue);
        if (_color.Valid && (_depth.Valid || !depth))
        {
            // bgfx takes a framebuffer's textures on some failures and not others, so the target keeps them itself.
            var attachments = stackalloc TextureHandle[2];
            attachments[0] = _color;
            attachments[1] = _depth;
            frameBuffer = Bx.CreateFrameBufferFromHandles(
                (byte)(depth ? 2 : 1),
                attachments,
                false
            );
        }
        FrameBuffer = frameBuffer;
        if (frameBuffer.Valid)
            return true;

        BgfxTexels.Destroy(ref _color);
        BgfxTexels.Destroy(ref _depth);
        return false;
    }

    [FramePath]
    public void Dispose()
    {
        if (!FrameBuffer.Valid)
            return;

        Bx.DestroyFrameBuffer(FrameBuffer);
        FrameBuffer = new FrameBufferHandle(ushort.MaxValue);
        BgfxTexels.Destroy(ref _color);
        BgfxTexels.Destroy(ref _depth);
    }
}
