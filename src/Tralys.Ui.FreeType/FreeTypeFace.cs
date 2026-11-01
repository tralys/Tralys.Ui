using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using FreeTypeSharp;
using Tralys.Ui.Fonts;
using static FreeTypeSharp.FT;

namespace Tralys.Ui.FreeType;

sealed unsafe class FreeTypeFace : IFontFace
{
    const FT_LOAD OutlineFlags =
        FT_LOAD.FT_LOAD_NO_SCALE | FT_LOAD.FT_LOAD_NO_BITMAP | FT_LOAD.FT_LOAD_NO_SVG;

    // A tricky face places its subglyphs by hinting, so it loads at one size and scales back to font units.
    const FT_LOAD HintedFlags = FT_LOAD.FT_LOAD_NO_BITMAP | FT_LOAD.FT_LOAD_NO_SVG;

    const byte TagOn = 1;
    const byte TagCubic = 2;

    const uint Os2Tag = 0x4F532F32;
    const uint TrueTypeVersion = 0x00010000;
    const uint TrueTag = 0x74727565;
    const uint Typ1Tag = 0x74797031;
    const uint OpenTypeTag = 0x4F54544F;
    const uint CollectionTag = 0x74746366;
    const int SfntHeaderSize = 12;
    const int SfntRecordSize = 16;

    readonly FreeTypeBackend _backend;
    readonly ReadOnlyMemory<byte> _fontData;
    readonly int _directory;
    readonly bool _tricky;
    readonly float _outlineScale = 1f;
    MemoryHandle _pin;
    FT_FaceRec_* _face;

    public FreeTypeFace(FreeTypeBackend backend, ReadOnlyMemory<byte> fontData, int faceIndex)
    {
        _backend = backend;
        _fontData = fontData;
        _pin = fontData.Pin();

        FT_FaceRec_* face;
        var error = FT_New_Memory_Face(
            backend.Library,
            (byte*)_pin.Pointer,
            fontData.Length,
            faceIndex,
            &face
        );
        if (error != FT_Error.FT_Err_Ok)
        {
            _pin.Dispose();
            FreeTypeError.Check(error, nameof(FT_New_Memory_Face));
        }

        if ((face->face_flags & (nint)FT_FACE_FLAG.FT_FACE_FLAG_SCALABLE) == 0)
        {
            FT_Done_Face(face);
            _pin.Dispose();
            throw new ArgumentException("The face has no outlines to scale.", nameof(fontData));
        }

        _face = face;
        _tricky = (face->face_flags & (nint)FT_FACE_FLAG.FT_FACE_FLAG_TRICKY) != 0;
        _directory = FindDirectory(fontData.Span, faceIndex);
        FamilyName = Marshal.PtrToStringUTF8((nint)face->family_name) ?? string.Empty;
        IsItalic = (face->style_flags & (nint)FT_STYLE_FLAG.FT_STYLE_FLAG_ITALIC) != 0;
        UnitsPerEm = face->units_per_EM;
        Metrics = new FaceMetrics(
            face->ascender,
            -face->descender,
            face->height - face->ascender + face->descender,
            -face->underline_position,
            face->underline_thickness
        );
        if (_tricky)
        {
            var size = GlyphAtlas.EmSize * 64f;
            error = FT_Set_Char_Size(face, 0, (nint)size, 0, 0);
            if (error != FT_Error.FT_Err_Ok)
            {
                FT_Done_Face(face);
                _face = null;
                _pin.Dispose();
                FreeTypeError.Check(error, nameof(FT_Set_Char_Size));
            }
            _outlineScale = UnitsPerEm / size;
        }
        var os2 = GetTable(Os2Tag).Span;
        Weight =
            os2.Length >= 6
                ? Math.Clamp((int)BinaryPrimitives.ReadUInt16BigEndian(os2[4..]), 1, 1000)
            : (face->style_flags & (nint)FT_STYLE_FLAG.FT_STYLE_FLAG_BOLD) != 0 ? 700
            : 400;
    }

    public string FamilyName { get; }

    public int Weight { get; }

    public bool IsItalic { get; }

    public int UnitsPerEm { get; }

    public FaceMetrics Metrics { get; }

    public int GetGlyphIndex(Rune rune)
    {
        ObjectDisposedException.ThrowIf(_face == null, this);
        return (int)FT_Get_Char_Index(_face, (nuint)rune.Value);
    }

    public float GetAdvance(int glyph)
    {
        ObjectDisposedException.ThrowIf(_face == null, this);
        // FT_Fixed is 32 bits on Windows, so the upper half has to start at zero.
        nint units = 0;
        return
            FT_Get_Advance(_face, (uint)glyph, FT_LOAD.FT_LOAD_NO_SCALE, &units)
            == FT_Error.FT_Err_Ok
            ? units
            : 0f;
    }

    public bool TryReadOutline<TSink>(int glyph, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        ObjectDisposedException.ThrowIf(_face == null, this);
        return FT_Load_Glyph(_face, (uint)glyph, _tricky ? HintedFlags : OutlineFlags)
                == FT_Error.FT_Err_Ok
            && _face->glyph->format == FT_Glyph_Format_.FT_GLYPH_FORMAT_OUTLINE
            && _face->glyph->outline.n_contours > 0
            && Trace(&_face->glyph->outline, _outlineScale, ref sink);
    }

    [FramePath]
    static bool Trace<TSink>(FT_Outline_* outline, float scale, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var points = outline->points;
        var tags = outline->tags;
        var first = 0;
        for (var contour = 0; contour < outline->n_contours; contour++)
        {
            var last = (int)outline->contours[contour];
            if (last < first || last >= outline->n_points)
                return false;

            var start = Point(points, first, scale);
            var limit = last;
            var point = first;
            var tag = tags[first] & 3;
            if (tag == TagCubic)
                return false;

            if (tag != TagOn)
            {
                if ((tags[last] & 3) == TagOn)
                {
                    start = Point(points, last, scale);
                    limit--;
                }
                else
                {
                    start = (start + Point(points, last, scale)) / 2;
                }
                point--;
            }

            sink.MoveTo(start);
            while (point < limit)
            {
                point++;
                tag = tags[point] & 3;
                var at = Point(points, point, scale);
                if (tag == TagOn)
                {
                    sink.LineTo(at);
                    continue;
                }

                if (tag == TagCubic)
                {
                    if (point + 1 > limit || (tags[point + 1] & 3) != TagCubic)
                        return false;

                    var second = Point(points, point + 1, scale);
                    point += 2;
                    sink.CubicTo(at, second, point <= limit ? Point(points, point, scale) : start);
                    continue;
                }

                var control = at;
                var closed = true;
                while (point < limit)
                {
                    point++;
                    tag = tags[point] & 3;
                    at = Point(points, point, scale);
                    if (tag == TagOn)
                    {
                        sink.QuadraticTo(control, at);
                        closed = false;
                        break;
                    }

                    if (tag == TagCubic)
                        return false;

                    sink.QuadraticTo(control, (control + at) / 2);
                    control = at;
                }

                if (closed)
                    sink.QuadraticTo(control, start);
            }

            first = last + 1;
        }
        return true;
    }

    static Vector2 Point(FT_Vector_* points, int index, float scale) =>
        new Vector2(points[index].x, points[index].y) * scale;

    public ReadOnlyMemory<byte> GetTable(uint tag)
    {
        ObjectDisposedException.ThrowIf(_face == null, this);
        if (_directory < 0)
            return default;

        var font = _fontData.Span;
        var count = BinaryPrimitives.ReadUInt16BigEndian(font[(_directory + 4)..]);
        for (var i = 0; i < count; i++)
        {
            var record = _directory + SfntHeaderSize + i * SfntRecordSize;
            if (record > font.Length - SfntRecordSize)
                return default;
            if (BinaryPrimitives.ReadUInt32BigEndian(font[record..]) != tag)
                continue;

            var offset = BinaryPrimitives.ReadUInt32BigEndian(font[(record + 8)..]);
            var length = BinaryPrimitives.ReadUInt32BigEndian(font[(record + 12)..]);
            return offset <= (uint)font.Length && length <= (uint)font.Length - offset
                ? _fontData.Slice((int)offset, (int)length)
                : default;
        }
        return default;
    }

    // A WOFF's tables are compressed behind a directory of its own, so it reads as having none.
    static int FindDirectory(ReadOnlySpan<byte> font, int faceIndex)
    {
        if (font.Length < SfntHeaderSize)
            return -1;

        var directory = 0u;
        var signature = BinaryPrimitives.ReadUInt32BigEndian(font);
        if (signature == CollectionTag)
        {
            // The high bits of a FreeType face index name a variation instance.
            var entry = SfntHeaderSize + (faceIndex & 0xFFFF) * sizeof(uint);
            if (entry > font.Length - sizeof(uint))
                return -1;
            directory = BinaryPrimitives.ReadUInt32BigEndian(font[entry..]);
        }
        else if (signature is not (TrueTypeVersion or TrueTag or Typ1Tag or OpenTypeTag))
        {
            return -1;
        }
        return directory <= (uint)(font.Length - SfntHeaderSize) ? (int)directory : -1;
    }

    public void Dispose()
    {
        if (_face == null)
            return;

        FT_Done_Face(_face);
        _face = null;
        _pin.Dispose();
        _backend.Release(this);
    }
}
