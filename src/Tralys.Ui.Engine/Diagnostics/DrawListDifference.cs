using System.Runtime.InteropServices;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Diagnostics;

static class DrawListDifference
{
    const int RowBytes = GradientRampAtlas.Width * 4;

    public static string? Of(DrawList drawn, DrawList rebuilt)
    {
        if (drawn.QuadCount != rebuilt.QuadCount)
            return $"{drawn.QuadCount} quads against {rebuilt.QuadCount}";
        if (!drawn.Batches.SequenceEqual(rebuilt.Batches))
            return $"batches differ ({drawn.Batches.Length} against {rebuilt.Batches.Length})";
        if (!drawn.Layers.SequenceEqual(rebuilt.Layers))
            return $"layers differ ({drawn.Layers.Length} against {rebuilt.Layers.Length})";
        if (!drawn.Indices.SequenceEqual(rebuilt.Indices))
            return "indices differ";

        var vertices = drawn.Vertices;
        var others = rebuilt.Vertices;
        if (vertices.Length != others.Length)
            return $"{vertices.Length} vertices against {others.Length}";
        for (var i = 0; i < vertices.Length; i++)
        {
            var mine = vertices[i];
            var theirs = others[i];
            if (!SameRamps(ref mine, drawn.Ramps, ref theirs, rebuilt.Ramps))
                return $"vertex {i} samples another gradient";
            if (
                !MemoryMarshal
                    .AsBytes(MemoryMarshal.CreateReadOnlySpan(ref mine, 1))
                    .SequenceEqual(
                        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref theirs, 1))
                    )
            )
                return $"vertex {i} differs";
        }
        return null;
    }

    // Each list's atlas puts a ramp in a row of its own choosing, so rows compare by texels, then drop out of the bytes.
    static bool SameRamps(
        ref UiVertex mine,
        GradientRampAtlas mineRamps,
        ref UiVertex theirs,
        GradientRampAtlas theirRamps
    )
    {
        var paints = (int)mine.Params.Y;
        if (paints != (int)theirs.Params.Y)
            return true;
        if (IsGradient(paints & 3))
        {
            if (!Row(mineRamps, mine.Params.Z).SequenceEqual(Row(theirRamps, theirs.Params.Z)))
                return false;
            mine.Params.Z = theirs.Params.Z = 0f;
        }
        if (IsGradient((paints >> 2) & 3))
        {
            if (!Row(mineRamps, mine.Params.W).SequenceEqual(Row(theirRamps, theirs.Params.W)))
                return false;
            mine.Params.W = theirs.Params.W = 0f;
        }
        return true;
    }

    static bool IsGradient(int paint) =>
        paint is (int)UiPaint.LinearGradient or (int)UiPaint.RadialGradient;

    static ReadOnlySpan<byte> Row(GradientRampAtlas ramps, float v)
    {
        var row = (int)(v * ramps.Rows);
        return (uint)row < (uint)ramps.Rows
            ? ramps.Pixels.Slice(row * RowBytes, RowBytes)
            : ReadOnlySpan<byte>.Empty;
    }
}
