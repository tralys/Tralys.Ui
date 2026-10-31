using System.Numerics;

namespace Tralys.Ui.Rendering;

public sealed partial class DrawList
{
    void EmitImage(RenderTexture texture, Rect destination, Rect source, float opacity) =>
        EmitImage(
            texture,
            destination,
            source,
            opacity,
            destination,
            source,
            new Vector2(source.Width / destination.Width, source.Height / destination.Height)
        );

    // Only outer's edges soften: a cell inside it meets its neighbours exactly, where soft edges would show as seams.
    void EmitImage(
        RenderTexture texture,
        Rect destination,
        Rect source,
        float opacity,
        Rect outer,
        Rect frame,
        Vector2 texelsPerLocal
    )
    {
        texture = Bind(texture, out var layer);
        float width = texture.Width;
        float height = texture.Height;
        if (!(width > 0f && height > 0f) || destination.IsEmpty)
            return;

        var uv = Uv(source, width, height);
        var transform = _state.Transform;
        var pad = LandsOnPixels(outer, transform) ? Vector2.Zero : DevicePixel(transform);
        var quad = Rect.FromEdges(
            destination.X <= outer.X ? destination.X - pad.X : destination.X,
            destination.Y <= outer.Y ? destination.Y - pad.Y : destination.Y,
            destination.Right >= outer.Right ? destination.Right + pad.X : destination.Right,
            destination.Bottom >= outer.Bottom ? destination.Bottom + pad.Y : destination.Bottom
        );
        var uvPerLocal = new Vector2(
            (uv.Z - uv.X) / destination.Width,
            (uv.W - uv.Y) / destination.Height
        );

        UiVertex vertex = default;
        vertex.Bounds = ToVector(outer);
        vertex.Inner = vertex.Bounds;
        vertex.ImageWindow = uv;
        vertex.ImageSample = ImageSampling(texelsPerLocal, frame, texture, uv);
        vertex.Fill = Tint(opacity * _state.Opacity);
        vertex.Shape.X = layer;
        vertex.Params = new Vector4(
            texture is RenderTextureArray ? UiPrimitive.ArrayImage : UiPrimitive.Image,
            UiPaint.Solid + (texture.IsPremultiplied ? UiPaint.PremultipliedTexture : 0f),
            0f,
            0f
        );
        Emit(
            quad,
            transform,
            ref vertex,
            texture,
            new Vector4(
                uv.X + (quad.X - destination.X) * uvPerLocal.X,
                uv.Y + (quad.Y - destination.Y) * uvPerLocal.Y,
                uv.Z + (quad.Right - destination.Right) * uvPerLocal.X,
                uv.W + (quad.Bottom - destination.Bottom) * uvPerLocal.Y
            ),
            null
        );
    }

    Vector3 ImageSampling(Vector2 texelsPerLocal, Rect frame, RenderTexture texture, Vector4 window)
    {
        var level = MipLevel(texelsPerLocal, frame, texture.Width, texture.Height);
        var coarsest = (int)MathF.Ceiling(level);
        return new Vector3(
            level,
            MathF.Min(0.5f / Math.Max(texture.Width >> coarsest, 1), (window.Z - window.X) * 0.5f),
            MathF.Min(0.5f / Math.Max(texture.Height >> coarsest, 1), (window.W - window.Y) * 0.5f)
        );
    }

    // A box-filtered level at a pixel's exact footprint reads visibly soft, so this picks a little finer.
    float MipLevel(Vector2 texelsPerLocal, Rect frame, int width, int height)
    {
        var transform = _state.Transform;
        var determinant = MathF.Abs(transform.GetDeterminant());
        if (!(determinant > 0f))
            return 0f;

        var across = new Vector2(
            texelsPerLocal.X * transform.M22,
            texelsPerLocal.Y * transform.M12
        );
        var down = new Vector2(texelsPerLocal.X * transform.M21, texelsPerLocal.Y * transform.M11);
        var level =
            MathF.Log2(MathF.Max(across.Length(), down.Length()) / determinant) - MipSharpening;
        return level > 0f ? MathF.Min(level, CleanMipLevels(frame, width, height)) : 0f;
    }

    // A level whose texels straddle the frame's edge would bleed in what lies past it, such as a sprite's neighbour.
    static int CleanMipLevels(Rect frame, int width, int height)
    {
        var level = 0;
        while (
            level < MaxMipLevel
            && OnMipEdge(frame.X, width, level + 1)
            && OnMipEdge(frame.Right, width, level + 1)
            && OnMipEdge(frame.Y, height, level + 1)
            && OnMipEdge(frame.Bottom, height, level + 1)
        )
            level++;
        return level;
    }

    static bool OnMipEdge(float edge, int size, int level)
    {
        var whole = (long)edge;
        return whole == edge && whole * Math.Max(size >> level, 1) % size == 0;
    }

    void EmitNineSlice(in RenderCommand command)
    {
        var texture = command.Texture!;
        var destination = command.Rect;
        var source = command.Source;
        var slice = command.Border;
        var scale = command.SliceScale;
        var tile = command.SliceTiles;
        var inset = command.SliceInset;

        var left = slice.Left * scale;
        var right = slice.Right * scale;
        var top = slice.Top * scale;
        var bottom = slice.Bottom * scale;
        var fitX = MathF.Min(1f, destination.Width / MathF.Max(left + right, 1e-6f));
        var fitY = MathF.Min(1f, destination.Height / MathF.Max(top + bottom, 1e-6f));

        // Every cell samples at the corners' level: an edge squeezed along its length would otherwise blur across it.
        var texelsPerLocal = new Vector2(1f / (scale * fitX), 1f / (scale * fitY));

        Span<float> sourceX =
        [
            source.X,
            source.X + slice.Left,
            source.Right - slice.Right,
            source.Right,
        ];
        Span<float> sourceY =
        [
            source.Y,
            source.Y + slice.Top,
            source.Bottom - slice.Bottom,
            source.Bottom,
        ];
        Span<float> destinationX =
        [
            destination.X,
            destination.X + left * fitX,
            destination.Right - right * fitX,
            destination.Right,
        ];
        Span<float> destinationY =
        [
            destination.Y,
            destination.Y + top * fitY,
            destination.Bottom - bottom * fitY,
            destination.Bottom,
        ];

        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var cell = Rect.FromEdges(
                    sourceX[column],
                    sourceY[row],
                    sourceX[column + 1],
                    sourceY[row + 1]
                );
                var target = Rect.FromEdges(
                    destinationX[column],
                    destinationY[row],
                    destinationX[column + 1],
                    destinationY[row + 1]
                );
                if (cell.IsEmpty || target.IsEmpty)
                    continue;

                var tileSize = new Vector2(
                    column == 1 ? cell.Width * scale : target.Width,
                    row == 1 ? cell.Height * scale : target.Height
                );
                cell = cell.Inflate(
                    -MathF.Min(inset, cell.Width * 0.5f),
                    -MathF.Min(inset, cell.Height * 0.5f)
                );
                if (tile && (row == 1 || column == 1))
                    EmitTiles(
                        texture,
                        target,
                        cell,
                        tileSize,
                        command.Opacity,
                        destination,
                        source,
                        texelsPerLocal
                    );
                else
                    EmitImage(
                        texture,
                        target,
                        cell,
                        command.Opacity,
                        destination,
                        source,
                        texelsPerLocal
                    );
            }
        }
    }

    void EmitTiles(
        RenderTexture texture,
        Rect target,
        Rect cell,
        Vector2 tileSize,
        float opacity,
        Rect outer,
        Rect frame,
        Vector2 texelsPerLocal
    )
    {
        const int MaxTiles = 1024;
        var tileWidth = tileSize.X;
        var tileHeight = tileSize.Y;
        var columns = (int)MathF.Min(MathF.Ceiling(target.Width / tileWidth - 1e-3f), MaxTiles + 1);
        var rows = (int)MathF.Min(MathF.Ceiling(target.Height / tileHeight - 1e-3f), MaxTiles + 1);
        if (columns * rows > MaxTiles)
        {
            EmitImage(texture, target, cell, opacity, outer, frame, texelsPerLocal);
            return;
        }

        for (var row = 0; row < rows; row++)
        {
            var y = target.Y + row * tileHeight;
            var height = MathF.Min(tileHeight, target.Bottom - y);
            for (var column = 0; column < columns; column++)
            {
                var x = target.X + column * tileWidth;
                var width = MathF.Min(tileWidth, target.Right - x);
                var source = new Rect(
                    cell.X,
                    cell.Y,
                    cell.Width * width / tileWidth,
                    cell.Height * height / tileHeight
                );
                EmitImage(
                    texture,
                    new Rect(x, y, width, height),
                    source,
                    opacity,
                    outer,
                    frame,
                    texelsPerLocal
                );
            }
        }
    }
}
