using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Tralys.Ui.Build;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: Tralys.Ui.Build <assembly> <map to write>");
            return 2;
        }

        var (assembly, map) = (args[0], args[1]);
        try
        {
            var count = CallerPaths.Strip(assembly, map);
            Console.WriteLine(
                $"{Path.GetFileName(assembly)}: {count} caller paths replaced by tokens."
            );
            return 0;
        }
        catch (Exception e) when (e is InvalidDataException or BadImageFormatException)
        {
            Console.Error.WriteLine($"{assembly} : error TRL6001: {e.Message}");
            return 1;
        }
    }
}

static class CallerPaths
{
    public static int Strip(string assemblyPath, string mapPath)
    {
        var image = File.ReadAllBytes(assemblyPath);
        var literals = new List<(int Offset, string Path)>();
        string name;

        using (var pe = new PEReader(new MemoryStream(image, writable: false)))
        {
            var metadata = pe.GetMetadataReader();
            name = metadata.GetString(metadata.GetAssemblyDefinition().Name);
            var heap =
                pe.PEHeaders.MetadataStartOffset
                + metadata.GetHeapMetadataOffset(HeapIndex.UserString);

            for (
                var handle = metadata.GetNextHandle(default(UserStringHandle));
                !handle.IsNil;
                handle = metadata.GetNextHandle(handle)
            )
            {
                var literal = metadata.GetUserString(handle);
                if (IsSourcePath(literal))
                    literals.Add((heap + MetadataTokens.GetHeapOffset(handle), literal));
            }
        }

        string[] paths = [.. literals.Select(l => l.Path).Distinct().Order(StringComparer.Ordinal)];
        var tokens = paths
            .Select((path, i) => (path, i))
            .ToDictionary(p => p.path, p => $"{name}#{p.i + 1}");
        foreach (var (offset, path) in literals)
            Rewrite(image, offset, tokens[path]);

        if (literals.Count > 0)
            File.WriteAllBytes(assemblyPath, image);

        // Line N decodes token N.
        File.WriteAllLines(mapPath, paths);
        return literals.Count;
    }

    static bool IsSourcePath(string literal) =>
        literal.EndsWith(".cs", StringComparison.Ordinal) && Path.IsPathRooted(literal);

    static void Rewrite(byte[] image, int offset, string token)
    {
        var (length, header) = ReadLength(image, offset);
        var end = offset + header + length;
        var size = token.Length * 2 + 1;
        if (size > length)
            throw new InvalidDataException(
                $"The token '{token}' is longer than the source path it replaces."
            );

        var at = offset + WriteLength(image, offset, size);
        var flag = false;
        foreach (var c in token)
        {
            image[at++] = (byte)c;
            image[at++] = (byte)(c >> 8);
            flag |= NeedsFlag(c);
        }
        image[at++] = flag ? (byte)1 : (byte)0;
        image.AsSpan(at, end - at).Clear();
    }

    static (int Length, int Header) ReadLength(byte[] image, int at) =>
        image[at] switch
        {
            < 0x80 => (image[at], 1),
            < 0xC0 => (((image[at] & 0x3F) << 8) | image[at + 1], 2),
            _ => (
                ((image[at] & 0x1F) << 24)
                    | (image[at + 1] << 16)
                    | (image[at + 2] << 8)
                    | image[at + 3],
                4
            ),
        };

    static int WriteLength(byte[] image, int at, int length)
    {
        if (length < 0x80)
        {
            image[at] = (byte)length;
            return 1;
        }

        if (length < 0x4000)
        {
            image[at] = (byte)(0x80 | (length >> 8));
            image[at + 1] = (byte)length;
            return 2;
        }

        image[at] = (byte)(0xC0 | (length >> 24));
        image[at + 1] = (byte)(length >> 16);
        image[at + 2] = (byte)(length >> 8);
        image[at + 3] = (byte)length;
        return 4;
    }

    // ECMA-335 II.24.2.4: the byte after a user string marks any char outside these.
    static bool NeedsFlag(char c) =>
        c > 0xFF || (byte)c is >= 0x01 and <= 0x08 or >= 0x0E and <= 0x1F or 0x27 or 0x2D or 0x7F;
}
