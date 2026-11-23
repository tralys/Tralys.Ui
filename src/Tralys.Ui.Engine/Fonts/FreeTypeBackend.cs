using FreeType.Net;
using static FreeType.Net.FT;

namespace Tralys.Ui.Fonts;

/// <summary>An <see cref="IFontBackend"/> on FreeType, which reads the faces' outlines and metrics in font units. A backend
/// and its faces belong to one thread.</summary>
sealed unsafe class FreeTypeBackend : IFontBackend, IDisposable
{
    readonly List<FreeTypeFace> _faces = [];
    FT_LibraryRec* _library;

    public FreeTypeBackend()
    {
        FT_LibraryRec* library;
        FreeTypeError.Check(FT_Init_FreeType(&library), nameof(FT_Init_FreeType));
        _library = library;
    }

    public FT_LibraryRec* Library => _library;

    /// <summary>Pins <paramref name="fontData"/> until the face is disposed. Disposing the backend disposes every face
    /// it opened.</summary>
    public IFontFace LoadFace(ReadOnlyMemory<byte> fontData, int faceIndex = 0)
    {
        ObjectDisposedException.ThrowIf(_library == null, this);
        ArgumentOutOfRangeException.ThrowIfNegative(faceIndex);

        var face = new FreeTypeFace(this, fontData, faceIndex);
        _faces.Add(face);
        return face;
    }

    public void Release(FreeTypeFace face) => _faces.Remove(face);

    public void Dispose()
    {
        if (_library == null)
            return;

        for (var i = _faces.Count - 1; i >= 0; i--)
            _faces[i].Dispose();

        FT_Done_FreeType(_library);
        _library = null;
    }
}

static class FreeTypeError
{
    public static void Check(FT_Error error, string call)
    {
        if (error != FT_Error.FT_Err_Ok)
            throw new InvalidOperationException($"FreeType's {call} failed with {error}.");
    }
}
