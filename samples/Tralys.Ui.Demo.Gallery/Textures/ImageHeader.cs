using System.Buffers.Binary;
using StbImageSharp;

namespace Tralys.Ui.Gallery.Textures;

/// <summary>An image file's size, read from its header without decoding it.</summary>
readonly record struct ImageHeader(int Width, int Height, bool IsDds)
{
    const int DdsHeaderBytes = 20;

    public static bool TryRead(string path, out ImageHeader header)
    {
        header = default;
        using var stream = File.OpenRead(path);
        Span<byte> start = stackalloc byte[DdsHeaderBytes];
        if (
            stream.ReadAtLeast(start, DdsHeaderBytes, throwOnEndOfStream: false) == DdsHeaderBytes
            && start[..4].SequenceEqual("DDS "u8)
        )
        {
            header = new ImageHeader(
                (int)BinaryPrimitives.ReadUInt32LittleEndian(start[16..]),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(start[12..]),
                IsDds: true
            );
            return header.Width > 0 && header.Height > 0;
        }

        stream.Position = 0;
        if (ImageInfo.FromStream(stream) is not { Width: > 0, Height: > 0 } info)
            return false;

        header = new ImageHeader(info.Width, info.Height, IsDds: false);
        return true;
    }

    /// <summary>Decodes the file's <paramref name="bytes"/> to RGBA8.</summary>
    /// <exception cref="InvalidDataException">They hold another size than this header gave.</exception>
    public ImageResult Decode(byte[] bytes)
    {
        var image = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha);
        if (image.Width != Width || image.Height != Height)
            throw new InvalidDataException(
                $"it decoded to {image.Width}x{image.Height}, not the {Width}x{Height} its header gives."
            );
        return image;
    }
}
