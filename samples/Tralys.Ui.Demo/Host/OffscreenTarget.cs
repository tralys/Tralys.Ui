using System.Buffers.Binary;
using Bgfx.Net;
using Tralys.Ui.Bgfx;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Demo;

sealed unsafe class OffscreenTarget : IDisposable
{
    const int HeaderSize = 18;
    const byte TrueColor = 2;
    const byte BitsPerPixel = 24;
    const byte TopDown = 0x20;

    readonly BgfxRenderTarget _target;
    readonly TextureHandle _readback;
    readonly byte[] _pixels;
    bool _disposed;

    public OffscreenTarget(int width, int height)
    {
        try
        {
            _target = new BgfxRenderTarget(width, height);
        }
        catch (InvalidOperationException e)
        {
            throw new DemoException(e.Message);
        }
        _readback = Bx.CreateTexture2d(
            (ushort)width,
            (ushort)height,
            false,
            1,
            TextureFormat.RGBA8,
            (ulong)(TextureFlags.BlitDst | TextureFlags.ReadBack),
            null,
            0
        );
        if (!_readback.Valid)
        {
            _target.Dispose();
            throw new DemoException(
                $"bgfx could not create a {width}x{height} texture to read the target back into."
            );
        }

        // bgfx fills this frames after Read's fixed scope ends, so it stays pinned.
        _pixels = GC.AllocateUninitializedArray<byte>(width * height * 4, pinned: true);
    }

    public FrameBufferHandle FrameBuffer => _target.FrameBuffer;

    /// <summary>What the last <see cref="Read"/> read back, RGBA rows from the top; valid once its frame is reached.</summary>
    public ReadOnlySpan<byte> Pixels => _pixels;

    // blitView must sort after the drawing views; Save is valid only once the returned frame is reached.
    public uint Read(ushort blitView)
    {
        var source = new TextureRegion
        {
            Handle = _target.Color,
            Width = (ushort)_target.Width,
            Height = (ushort)_target.Height,
            Depth = 1,
        };
        var destination = new TextureRegion
        {
            Handle = _readback,
            Width = (ushort)_target.Width,
            Height = (ushort)_target.Height,
            Depth = 1,
        };
        Bx.Blit(blitView, &destination, &source);
        fixed (byte* data = _pixels)
            return Bx.ReadTexture(&destination, data);
    }

    public void Save(string path)
    {
        var file = new byte[HeaderSize + _target.Width * _target.Height * 3];
        file[2] = TrueColor;
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(12), (ushort)_target.Width);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(14), (ushort)_target.Height);
        file[16] = BitsPerPixel;
        file[17] = TopDown;
        for (int from = 0, to = HeaderSize; from < _pixels.Length; from += 4, to += 3)
        {
            file[to] = _pixels[from + 2];
            file[to + 1] = _pixels[from + 1];
            file[to + 2] = _pixels[from];
        }
        File.WriteAllBytes(path, file);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _target.Dispose();
        Bx.DestroyTexture(_readback);
    }
}
