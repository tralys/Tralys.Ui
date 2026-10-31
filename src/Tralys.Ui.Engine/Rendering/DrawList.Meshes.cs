using System.Numerics;

namespace Tralys.Ui.Rendering;

public sealed partial class DrawList
{
    void EmitPath(in RenderCommand command, RenderCommandBuffer buffer)
    {
        var path = buffer.Paths[command.Start];
        var transform = _state.Transform;
        var reach = command.StrokeReach + 1f;
        if (!Overlaps(path.Bounds.Inflate(reach, reach).TransformedBy(transform), _state.Clip))
            return;

        _tessellator.Clear();
        _tessellator.AddPath(buffer.VerbsOf(path), buffer.PointsOf(path), transform);
        if (command.Fill.IsVisible)
        {
            _tessellator.Fill(path.FillRule);
            EmitMesh(command.Fill, path.Bounds);
        }

        if (command.StrokeWidth > 0f)
        {
            _tessellator.ClearMesh();
            var scale = UniformScale(transform);
            var dash = command.Dash;
            _tessellator.Stroke(
                command.StrokeWidth * 0.5f * scale,
                command.LineJoin,
                command.StartCap,
                command.EndCap,
                command.MiterLimit,
                buffer.DashesOf(dash),
                scale,
                dash.Offset,
                dash.Cap
            );
            var half = command.StrokeWidth * 0.5f;
            EmitMesh(command.Stroke, path.Bounds.Inflate(half, half));
        }
    }

    void EmitMesh(in Paint paint, Rect bounds)
    {
        if (_tessellator.Overflowed || _tessellator.Indices.IsEmpty)
            return;
        if (!Matrix3x2.Invert(_state.Transform, out var toLocal))
            return;

        var clip = _state.Clip;
        UiVertex vertex = default;
        var image = ResolveFill(ref vertex, paint, bounds, Matrix3x2.Identity);
        vertex.Params.X = UiPrimitive.Mesh;
        vertex.Clip = Edges(clip);
        WriteMesh(ref vertex, toLocal, _state.Layer, -1, image);
        Track(MeshBounds().Intersect(clip));
    }

    Rect MeshBounds()
    {
        var positions = _tessellator.Positions;
        var min = positions[0];
        var max = positions[0];
        foreach (var position in positions)
        {
            min = Vector2.Min(min, position);
            max = Vector2.Max(max, position);
        }
        return Rect.FromEdges(min.X, min.Y, max.X, max.Y);
    }

    void WriteMesh(
        ref UiVertex vertex,
        in Matrix3x2 toLocal,
        short target,
        short source,
        RenderTexture? paint
    )
    {
        var count = _tessellator.Positions.Length;
        var indices = _tessellator.Indices;
        if (count <= MaxBatchVertices)
        {
            var baseVertex = OpenMeshBatch(paint, count, indices.Length, target, source);
            for (var i = 0; i < count; i++)
                WriteMeshVertex(ref vertex, toLocal, i);
            foreach (var index in indices)
                WriteIndex(baseVertex + index);
            return;
        }

        ValueList.EnsureCapacity(ref _meshSlots, count);
        ValueList.EnsureCapacity(ref _meshChunk, MaxBatchVertices);
        _meshSlots.AsSpan(0, count).Fill(-1);
        var start = 0;
        var used = 0;
        for (var i = 0; i < indices.Length; i += 3)
        {
            var fresh = 0;
            for (var k = 0; k < 3; k++)
            {
                if (_meshSlots[indices[i + k]] < 0)
                    fresh++;
            }
            if (used + fresh > MaxBatchVertices)
            {
                WriteMeshChunk(ref vertex, toLocal, target, source, paint, indices[start..i], used);
                start = i;
                used = 0;
            }
            for (var k = 0; k < 3; k++)
            {
                ref var slot = ref _meshSlots[indices[i + k]];
                if (slot >= 0)
                    continue;
                slot = used;
                _meshChunk[used++] = indices[i + k];
            }
        }
        WriteMeshChunk(ref vertex, toLocal, target, source, paint, indices[start..], used);
    }

    void WriteMeshChunk(
        ref UiVertex vertex,
        in Matrix3x2 toLocal,
        short target,
        short source,
        RenderTexture? paint,
        ReadOnlySpan<int> indices,
        int count
    )
    {
        var baseVertex = OpenMeshBatch(paint, count, indices.Length, target, source);
        var chunk = _meshChunk.AsSpan(0, count);
        foreach (var index in chunk)
            WriteMeshVertex(ref vertex, toLocal, index);
        foreach (var index in indices)
            WriteIndex(baseVertex + _meshSlots[index]);
        foreach (var index in chunk)
            _meshSlots[index] = -1;
    }

    int OpenMeshBatch(
        RenderTexture? paint,
        int vertices,
        int indices,
        short target,
        short source
    ) => OpenBatch(null, null, paint, vertices, indices, target, source, true);

    void WriteMeshVertex(ref UiVertex vertex, in Matrix3x2 toLocal, int index)
    {
        var position = _tessellator.Positions[index];
        vertex.Position = position;
        var local = Vector2.Transform(position, toLocal);
        vertex.Local = new Vector4(local.X, local.Y, 0f, 0f);
        vertex.Inner.X = _tessellator.Coverage[index];
        _vertices[_vertexCount++] = vertex;
    }

    void WriteIndex(int index)
    {
        var value = (ushort)index;
        if (Replaying)
            ReplayIndex(value);
        _indices[_indexCount++] = value;
    }
}
