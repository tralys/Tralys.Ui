using System.Numerics;

namespace Tralys.Ui.Rendering;

public sealed partial class DrawList
{
    RenderTexture? ResolveFill(
        ref UiVertex vertex,
        in Paint paint,
        Rect bounds,
        in Matrix3x2 absoluteToLocal
    )
    {
        var resolved = Resolve(paint, bounds, absoluteToLocal);
        vertex.Fill = resolved.Color;
        vertex.FillGradient = resolved.Gradient;
        vertex.Params.Y += resolved.Kind;
        vertex.Params.Z = resolved.Ramp;
        if (resolved.Image is not null)
            MarkImage(ref vertex, UiPaint.FillImage, resolved);
        return resolved.Image;
    }

    RenderTexture? ResolveStroke(ref UiVertex vertex, in Paint paint, Rect bounds)
    {
        var resolved = Resolve(paint, bounds, Matrix3x2.Identity);
        vertex.Stroke = resolved.Color;
        vertex.StrokeGradient = resolved.Gradient;
        vertex.Params.Y += resolved.Kind * 4f;
        vertex.Params.W = resolved.Ramp;
        if (resolved.Image is not null)
            MarkImage(ref vertex, UiPaint.StrokeImage, resolved);
        return resolved.Image;
    }

    static void MarkImage(ref UiVertex vertex, float which, in ResolvedPaint image)
    {
        vertex.Params.Y += which + (image.Premultiplied ? UiPaint.PremultipliedTexture : 0f);
        vertex.ImageWindow = image.Window;
        vertex.ImageSample = image.Sample;
    }

    ResolvedPaint Resolve(in Paint paint, Rect bounds, in Matrix3x2 absoluteToLocal)
    {
        var opacity = paint.Opacity * _state.Opacity;
        switch (paint)
        {
            case { Kind: PaintKind.Solid }:
                return new ResolvedPaint(paint.Color.ToPremultipliedRgba(opacity), UiPaint.Solid);
            case { Kind: PaintKind.LinearGradient or PaintKind.RadialGradient, Stops: { } stops }:
                var row = _ramps.Acquire(stops);
                if (row < 0)
                    return new ResolvedPaint(
                        stops.Stops[0].Color.ToPremultipliedRgba(opacity),
                        UiPaint.Solid
                    );
                RecordRamp(stops, row);

                return new ResolvedPaint(
                    Tint(opacity),
                    paint.Kind == PaintKind.LinearGradient
                        ? UiPaint.LinearGradient
                        : UiPaint.RadialGradient,
                    GradientGeometry(paint, bounds, absoluteToLocal),
                    (row + 0.5f) / _ramps.Rows
                );
            case { Kind: PaintKind.Image, Image: { } image }:
                return MapImage(paint, image, bounds, opacity);
            default:
                return default;
        }
    }

    ResolvedPaint MapImage(in Paint paint, PaintImage image, Rect bounds, float opacity)
    {
        var texture = image.Source.Resolve(Textures);
        if (texture is null)
            return default;

        texture = Bind(texture, out var index);
        float width = texture.Width;
        float height = texture.Height;
        var area = ImageArea(paint, bounds);
        var box =
            image.ViewboxUnits == GradientSpace.Absolute
                ? image.Viewbox
                : new Rect(
                    image.Viewbox.X * width,
                    image.Viewbox.Y * height,
                    image.Viewbox.Width * width,
                    image.Viewbox.Height * height
                );
        if (!(width > 0f && height > 0f) || box.IsEmpty || area.IsEmpty)
            return default;

        var scale = StretchScale.Of(area.Size, box.Size, image.Stretch, StretchDirection.Both);
        var size = new Vector2(box.Width * scale.X, box.Height * scale.Y);
        if (!(size.X > 0f && size.Y > 0f))
            return default;

        var origin = new Vector2(
            area.X + (area.Width - size.X) * (int)image.AlignmentX * 0.5f,
            area.Y + (area.Height - size.Y) * (int)image.AlignmentY * 0.5f
        );
        var step = new Vector2(box.Width / size.X / width, box.Height / size.Y / height);
        var start = new Vector2(box.X / width, box.Y / height) - origin * step;
        var window = Uv(box, width, height);

        return new ResolvedPaint(
            Tint(opacity),
            UiPaint.Solid,
            new Vector4(step.X, step.Y, start.X, start.Y),
            texture is RenderTextureArray ? index : -1f,
            texture,
            window,
            ImageSampling(
                new Vector2(box.Width / size.X, box.Height / size.Y),
                box,
                texture,
                window
            ),
            texture.IsPremultiplied
        );
    }

    static Rect ImageArea(in Paint paint, Rect bounds) =>
        paint.Space == GradientSpace.Absolute
            ? new Rect(paint.Start.X, paint.Start.Y, paint.End.X, paint.End.Y)
            : bounds;

    void RecordRamp(GradientStops stops, int row)
    {
        if (Replaying)
        {
            ReplayRamp(row);
            return;
        }
        if (_rampCount > 0 && _rampRows[_rampCount - 1] == row)
            return;
        if (_rampCount == _rampStops.Length)
        {
            Array.Resize(ref _rampStops, _rampStops.Length * 2);
            Array.Resize(ref _rampRows, _rampStops.Length);
        }
        _rampStops[_rampCount] = stops;
        _rampRows[_rampCount++] = row;
    }

    static Vector4 GradientGeometry(in Paint paint, Rect bounds, in Matrix3x2 absoluteToLocal)
    {
        if (paint.Space == GradientSpace.Absolute)
        {
            var start = Vector2.Transform(paint.Start, absoluteToLocal);
            var end =
                paint.Kind == PaintKind.LinearGradient
                    ? Vector2.Transform(paint.End, absoluteToLocal)
                    : paint.End;
            return new Vector4(start.X, start.Y, end.X, end.Y);
        }

        var pinned = paint.PinnedTo(bounds);
        return new Vector4(pinned.Start.X, pinned.Start.Y, pinned.End.X, pinned.End.Y);
    }

    readonly record struct ResolvedPaint(
        uint Color,
        float Kind,
        Vector4 Gradient = default,
        float Ramp = 0f,
        RenderTexture? Image = null,
        Vector4 Window = default,
        Vector3 Sample = default,
        bool Premultiplied = false
    );
}
