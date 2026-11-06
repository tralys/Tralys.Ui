using StbImageSharp;
using StbImageWriteSharp;

namespace Tralys.Ui.Demo;

/// <summary>An RGBA image, rows from the top, as a pixel golden holds it.</summary>
sealed class StoryImage(int width, int height, byte[] rgba)
{
    /// <summary>How far a channel may move before its pixel differs, so a blend or blur rounding a step another way
    /// passes.</summary>
    public const int Tolerance = 8;

    public int Width => width;

    public int Height => height;

    /// <summary>A PNG or TGA file, such as the frame a story capture writes.</summary>
    public static StoryImage Load(string path)
    {
        var image = ImageResult.FromMemory(
            File.ReadAllBytes(path),
            StbImageSharp.ColorComponents.RedGreenBlueAlpha
        );
        return new StoryImage(image.Width, image.Height, image.Data);
    }

    /// <summary>Writes the image as a PNG, making its directory.</summary>
    public void Save(string path)
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
            Directory.CreateDirectory(directory);
        using var file = File.Create(path);
        new ImageWriter().WritePng(
            rgba,
            width,
            height,
            StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha,
            file
        );
    }

    /// <summary>How many pixels have a channel more than <see cref="Tolerance"/> from <paramref name="other"/>'s, both
    /// from their top left, counting every pixel that only one of the two covers.</summary>
    public int Differences(StoryImage other)
    {
        var differing = 0;
        for (var y = 0; y < Math.Max(height, other.Height); y++)
        {
            for (var x = 0; x < Math.Max(width, other.Width); x++)
            {
                if (Differs(other, x, y))
                    differing++;
            }
        }
        return differing;
    }

    /// <summary>This image dimmed, with every pixel <see cref="Differences"/> counts in red.</summary>
    public StoryImage Diff(StoryImage other)
    {
        var w = Math.Max(width, other.Width);
        var h = Math.Max(height, other.Height);
        var diff = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var at = (y * w + x) * 4;
                if (Differs(other, x, y))
                {
                    diff[at] = 255;
                }
                else
                {
                    var from = (y * width + x) * 4;
                    var gray = (rgba[from] + rgba[from + 1] + rgba[from + 2]) / 12;
                    diff[at] = diff[at + 1] = diff[at + 2] = (byte)gray;
                }
                diff[at + 3] = 255;
            }
        }
        return new StoryImage(w, h, diff);
    }

    bool Differs(StoryImage other, int x, int y)
    {
        if (x >= width || y >= height || x >= other.Width || y >= other.Height)
            return true;

        var mine = (y * width + x) * 4;
        var theirs = (y * other.Width + x) * 4;
        for (var channel = 0; channel < 4; channel++)
        {
            if (Math.Abs(rgba[mine + channel] - other.Pixels[theirs + channel]) > Tolerance)
                return true;
        }
        return false;
    }

    byte[] Pixels => rgba;
}
