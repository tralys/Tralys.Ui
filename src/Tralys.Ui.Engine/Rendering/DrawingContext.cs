using System.Numerics;

namespace Tralys.Ui.Rendering;

/// <summary>Records drawing into a <see cref="RenderCommandBuffer"/>. It lives only for the call that renders.</summary>
[FramePath]
public readonly ref struct DrawingContext
{
    readonly RenderCommandBuffer _buffer;

    internal DrawingContext(RenderCommandBuffer buffer) => _buffer = buffer;

    public void DrawRectangle(Paint fill, Rect rect) =>
        DrawBox(rect, default, default, fill, default);

    public void DrawRoundedRectangle(Paint fill, Rect rect, CornerRadius radius) =>
        DrawBox(rect, radius, default, fill, default);

    /// <summary>Records <paramref name="rect"/> drawing nothing, in place of a solid rectangle shown there at other
    /// times, as a blinking caret's is: a visual switching between the two keeps the same shapes, so its frame is
    /// patched in place rather than rebuilt.</summary>
    public void HoldRectangle(Rect rect)
    {
        if (rect.IsEmpty)
            return;

        ref var command = ref _buffer.Add(RenderOp.Box);
        command.Rect = rect;
        command.Fill = Color.Transparent;
    }

    /// <summary>Paints the band between <paramref name="rect"/>'s edge and the edge inset by <paramref name="border"/>,
    /// and fills inside that inner edge, both with rounded corners, as a Border does.</summary>
    public void DrawBox(
        Rect rect,
        CornerRadius radius,
        Thickness border,
        Paint fill,
        Paint borderPaint
    )
    {
        var hasBorder = borderPaint.IsVisible && !border.IsZero;
        if (rect.IsEmpty || !(fill.IsVisible || hasBorder))
            return;

        ref var command = ref _buffer.Add(RenderOp.Box);
        command.Rect = rect;
        command.Radius = radius;
        command.Border = border;
        command.Fill = fill;
        command.Stroke = hasBorder ? borderPaint : default;
    }

    /// <summary>The ellipse filling <paramref name="bounds"/>, its stroke inside them.</summary>
    public void DrawEllipse(Rect bounds, Paint fill, float strokeThickness, Paint stroke)
    {
        var hasStroke = stroke.IsVisible && strokeThickness > 0f;
        if (bounds.IsEmpty || !(fill.IsVisible || hasStroke))
            return;

        ref var command = ref _buffer.Add(RenderOp.Ellipse);
        command.Rect = bounds;
        command.StrokeWidth = hasStroke ? strokeThickness : 0f;
        command.Fill = fill;
        command.Stroke = hasStroke ? stroke : default;
    }

    /// <summary>A ring segment whose outer edge sits at <paramref name="radius"/>, <paramref name="thickness"/> deep,
    /// from <paramref name="startAngle"/> sweeping <paramref name="sweepAngle"/> radians clockwise; 0 points along +X.</summary>
    public void DrawArc(
        Vector2 center,
        float radius,
        float thickness,
        float startAngle,
        float sweepAngle,
        Paint paint
    )
    {
        if (!(radius > 0f && thickness > 0f && sweepAngle != 0f) || !paint.IsVisible)
            return;

        if (sweepAngle < 0f)
        {
            startAngle += sweepAngle;
            sweepAngle = -sweepAngle;
        }

        ref var command = ref _buffer.Add(RenderOp.Arc);
        command.Rect = new Rect(center.X - radius, center.Y - radius, radius * 2f, radius * 2f);
        command.StrokeWidth = MathF.Min(thickness, radius);
        command.Data = new Vector4(startAngle, sweepAngle, 0f, 0f);
        command.Fill = paint;
    }

    /// <summary>A straight line <paramref name="thickness"/> wide with flat ends.</summary>
    public void DrawLine(Vector2 from, Vector2 to, float thickness, Paint paint)
    {
        if (!(thickness > 0f) || from == to || !paint.IsVisible)
            return;

        ref var command = ref _buffer.Add(RenderOp.Line);
        command.Data = new Vector4(from.X, from.Y, to.X, to.Y);
        command.StrokeWidth = thickness;
        command.Fill = paint;
    }

    public void DrawImage(RenderTexture texture, Rect destination, float opacity = 1f)
    {
        ArgumentNullException.ThrowIfNull(texture);
        DrawImage(texture, destination, new Rect(0f, 0f, texture.Width, texture.Height), opacity);
    }

    /// <summary>The <paramref name="source"/> texels of <paramref name="texture"/> stretched over <paramref name="destination"/>.</summary>
    public void DrawImage(RenderTexture texture, Rect destination, Rect source, float opacity = 1f)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (destination.IsEmpty || source.IsEmpty || !(opacity > 0f))
            return;

        ref var command = ref _buffer.Add(RenderOp.Image);
        command.Rect = destination;
        command.Source = source;
        command.Opacity = opacity;
        command.Texture = texture;
    }

    /// <summary>The <paramref name="source"/> texels of <paramref name="texture"/> cut by <paramref name="slice"/> and
    /// laid over <paramref name="destination"/>. Corners too wide or tall for it shrink to fit.</summary>
    public void DrawImage(
        RenderTexture texture,
        Rect destination,
        Rect source,
        NineSlice slice,
        float opacity = 1f
    )
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (destination.IsEmpty || source.IsEmpty || !(opacity > 0f && slice.Scale > 0f))
            return;

        ref var command = ref _buffer.Add(RenderOp.NineSlice);
        command.Rect = destination;
        command.Source = source;
        command.Border = slice.Slice;
        command.Data = new Vector4(
            slice.Scale,
            slice.Fill == SliceFill.Tile ? 1f : 0f,
            MathF.Max(0f, slice.TexelInset),
            0f
        );
        command.Opacity = opacity;
        command.Texture = texture;
    }

    /// <summary>Glyphs whose <see cref="Fonts.GlyphAtlas"/> distance fields are read from the red channel of
    /// <paramref name="atlas"/>, painted with <paramref name="paint"/>; a gradient spans the whole run.</summary>
    public void DrawGlyphs(RenderTexture atlas, ReadOnlySpan<GlyphQuad> glyphs, Paint paint)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        if (glyphs.IsEmpty || !paint.IsVisible)
            return;

        var bounds = glyphs[0].Destination;
        for (var i = 1; i < glyphs.Length; i++)
            bounds = bounds.Union(glyphs[i].Destination);

        var start = _buffer.AddGlyphs(glyphs);
        ref var command = ref _buffer.Add(RenderOp.Glyphs);
        command.Rect = bounds;
        command.Texture = atlas;
        command.Generation = atlas.Generation;
        command.Fill = paint;
        command.Start = start;
        command.Count = glyphs.Length;
    }

    /// <summary><paramref name="path"/> filled by its fill rule with <paramref name="fill"/>, then outlined with
    /// <paramref name="stroke"/>; either may draw nothing. The path is copied, so the builder can be reused at once.</summary>
    public void DrawPath(PathBuilder path, Paint fill, Pen stroke)
    {
        ArgumentNullException.ThrowIfNull(path);
        var hasStroke = stroke.IsVisible;
        if (path.IsEmpty || !(fill.IsVisible || hasStroke))
            return;

        var index = _buffer.AddPath(path);
        ref var command = ref _buffer.Add(RenderOp.Path);
        command.Fill = fill;
        command.Start = index;
        if (!hasStroke)
            return;

        command.Stroke = stroke.Paint;
        command.StrokeWidth = stroke.Thickness;
        command.Data = new Vector4(
            (float)stroke.LineJoin,
            (float)stroke.StartLineCap,
            (float)stroke.EndLineCap,
            MathF.Max(1f, stroke.MiterLimit)
        );
        command.Dash = _buffer.AddDashes(stroke);
    }

    /// <summary>The shadow <paramref name="shadow"/> casts from the box <paramref name="rect"/> with corners
    /// <paramref name="radius"/>, one quad drawn analytically. Draw an outer shadow before its box, and an inset one
    /// after the box's background.</summary>
    public void DrawBoxShadow(Rect rect, CornerRadius radius, BoxShadow shadow)
    {
        if (rect.IsEmpty || shadow.Color.A == 0)
            return;

        ref var command = ref _buffer.Add(
            shadow.IsInset ? RenderOp.InsetBoxShadow : RenderOp.BoxShadow
        );
        command.Rect = rect;
        command.Radius = radius;
        command.Data = new Vector4(
            shadow.OffsetX,
            shadow.OffsetY,
            MathF.Max(0f, shadow.Blur),
            shadow.Spread
        );
        command.Fill = shadow.Color;
    }

    public void PushClip(Rect clip) => _buffer.AddPush(RenderOp.PushClip).Rect = clip;

    /// <summary>Clips to <paramref name="clip"/> with rounded corners, softening its edge through an offscreen layer.</summary>
    public void PushClip(Rect clip, CornerRadius radius)
    {
        ref var command = ref _buffer.AddPush(RenderOp.PushRoundedClip);
        command.Rect = clip;
        command.Radius = radius;
    }

    /// <summary>Clips to the region <paramref name="path"/> encloses by its fill rule, softening its edge through an offscreen layer.
    /// The path is copied, so the builder can be reused at once.</summary>
    public void PushClip(PathBuilder path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var index = _buffer.AddPath(path);
        _buffer.AddPush(RenderOp.PushPathClip).Start = index;
    }

    public void PushTransform(Matrix3x2 transform) =>
        _buffer.AddPush(RenderOp.PushTransform).Transform = transform;

    public void PushOpacity(float opacity) =>
        _buffer.AddPush(RenderOp.PushOpacity).Opacity = opacity;

    public void Pop() => _buffer.Pop();
}
