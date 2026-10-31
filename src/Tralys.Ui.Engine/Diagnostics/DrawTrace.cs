using System.Globalization;
using System.Numerics;
using System.Text;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Diagnostics;

/// <summary>Writes each push and primitive a <see cref="DrawList"/> is given as one line in local space, handed with the
/// drawer flattening it, or null for what the view pushes itself. A translation placing a drawer is left out, since a
/// layout dump records where it is, as is what a drawer pops at its end, since its children close inside it; a
/// translation or pop recorded in a drawer's own drawing is written.</summary>
sealed class DrawTrace(Action<object?, string> line)
{
    readonly StringBuilder _text = new();
    object? _drawer;

    /// <returns>The drawer flattening before <paramref name="drawer"/>, for <see cref="Leave"/>.</returns>
    public object? Enter(object drawer)
    {
        var outer = _drawer;
        _drawer = drawer;
        return outer;
    }

    public void Leave(object? outer) => _drawer = outer;

    public void Placement(in Matrix3x2 transform)
    {
        if (!IsTranslation(transform))
            Transform(transform);
    }

    public void Transform(in Matrix3x2 transform)
    {
        if (IsTranslation(transform))
            Start("translate").Point(transform.M31, transform.M32);
        else
            Start("matrix")
                .Number(transform.M11)
                .Comma(transform.M12)
                .Comma(transform.M21)
                .Comma(transform.M22)
                .Comma(transform.M31)
                .Comma(transform.M32);
        End();
    }

    public void Opacity(float opacity)
    {
        Start("opacity").Number(opacity);
        End();
    }

    public void Clip(Rect clip, CornerRadius radius)
    {
        Start("clip").Box(clip).Radius(radius);
        End();
    }

    public void Clip(ReadOnlySpan<PathVerb> verbs, ReadOnlySpan<Vector2> points, FillRule rule)
    {
        Start("clip path").Shape(verbs, points, rule);
        End();
    }

    public void Layer(float opacity)
    {
        Start("layer").Number(opacity);
        End();
    }

    public void Mask(in Paint mask, Rect bounds)
    {
        Start("mask").Box(bounds).Paint(mask);
        End();
    }

    public void Shadow(in DropShadow shadow)
    {
        Start("drop-shadow")
            .Color(shadow.Color)
            .Word("direction")
            .Number(shadow.Direction)
            .Word("depth")
            .Number(shadow.ShadowDepth)
            .Word("blur")
            .Number(shadow.BlurRadius)
            .Word("opacity")
            .Number(shadow.Opacity);
        End();
    }

    public void Pop()
    {
        Start("pop");
        End();
    }

    public void Draw(in RenderCommand command, RenderCommandBuffer buffer)
    {
        switch (command.Op)
        {
            case RenderOp.Box:
                Start("box")
                    .Box(command.Rect)
                    .Radius(command.Radius)
                    .Word("fill")
                    .Paint(command.Fill);
                if (command.Stroke.IsVisible)
                    Word("border").Edges(command.Border).Paint(command.Stroke);
                break;
            case RenderOp.Ellipse:
                Start("ellipse").Box(command.Rect).Word("fill").Paint(command.Fill);
                if (command.Stroke.IsVisible)
                    Stroke(command.StrokeWidth, command.Stroke);
                break;
            case RenderOp.Arc:
                Start("arc")
                    .Box(command.Rect)
                    .Word("width")
                    .Number(command.StrokeWidth)
                    .Word("from")
                    .Number(Degrees(command.ArcStart))
                    .Word("sweep")
                    .Number(Degrees(command.ArcSweep))
                    .Paint(command.Fill);
                break;
            case RenderOp.Line:
                Start("line")
                    .Point(command.LineFrom.X, command.LineFrom.Y)
                    .Point(command.LineTo.X, command.LineTo.Y)
                    .Word("width")
                    .Number(command.StrokeWidth)
                    .Paint(command.Fill);
                break;
            case RenderOp.Image:
                Start("image").Box(command.Rect).Texels(command.Source, command.Texture!);
                Faded(command.Opacity);
                break;
            case RenderOp.NineSlice:
                Start("nine-slice")
                    .Box(command.Rect)
                    .Texels(command.Source, command.Texture!)
                    .Word("slice")
                    .Edges(command.Border)
                    .Word("scale")
                    .Number(command.SliceScale);
                if (command.SliceTiles)
                    Word("tiled");
                if (command.SliceInset > 0f)
                    Word("inset").Number(command.SliceInset);
                Faded(command.Opacity);
                break;
            case RenderOp.Glyphs:
                Start("glyphs")
                    .Number(command.Count)
                    .Word("in")
                    .Box(command.Rect)
                    .Paint(command.Fill);
                break;
            case RenderOp.BoxShadow or RenderOp.InsetBoxShadow:
                Start(command.Op == RenderOp.BoxShadow ? "shadow" : "inset-shadow")
                    .Box(command.Rect)
                    .Radius(command.Radius)
                    .Word("offset")
                    .Point(command.ShadowOffset.X, command.ShadowOffset.Y)
                    .Word("blur")
                    .Number(command.ShadowBlur)
                    .Word("spread")
                    .Number(command.ShadowSpread)
                    .Paint(command.Fill);
                break;
            case RenderOp.Path:
                var path = buffer.Paths[command.Start];
                Start("path")
                    .Shape(buffer.VerbsOf(path), buffer.PointsOf(path), path.FillRule)
                    .Word("fill")
                    .Paint(command.Fill);
                if (command.StrokeWidth > 0f)
                {
                    Stroke(command.StrokeWidth, command.Stroke);
                    Word(Name(command.LineJoin))
                        .Word(Name(command.StartCap))
                        .Word(Name(command.EndCap));
                    Word("miter").Number(command.MiterLimit);
                    if (command.Dash.Count > 0)
                    {
                        var dashes = buffer.DashesOf(command.Dash);
                        Word("dashes").Number(dashes[0]);
                        for (var i = 1; i < dashes.Length; i++)
                            Comma(dashes[i]);
                        Word("offset").Number(command.Dash.Offset).Word(Name(command.Dash.Cap));
                    }
                }
                break;
            default:
                throw Unknown(command.Op);
        }
        End();
    }

    static ArgumentOutOfRangeException Unknown(RenderOp op) => new("command", op, null);

    void Stroke(float width, in Paint paint) => Word("stroke").Number(width).Paint(paint);

    void Faded(float opacity)
    {
        if (opacity < 1f)
            Word("opacity").Number(opacity);
    }

    DrawTrace Start(string op)
    {
        _text.Clear().Append(op);
        return this;
    }

    void End() => line(_drawer, _text.ToString());

    DrawTrace Word(string word)
    {
        _text.Append(' ').Append(word);
        return this;
    }

    DrawTrace Number(float value)
    {
        _text.Append(' ');
        return Value(value);
    }

    DrawTrace Comma(float value)
    {
        _text.Append(',');
        return Value(value);
    }

    DrawTrace Value(float value)
    {
        // Adding zero turns a rounded -0 into 0, which would otherwise be written as -0.
        _text.Append((MathF.Round(value, 2) + 0f).ToString("0.##", CultureInfo.InvariantCulture));
        return this;
    }

    DrawTrace Point(float x, float y) => Number(x).Comma(y);

    DrawTrace Box(Rect rect)
    {
        Point(rect.X, rect.Y);
        _text.Append(' ');
        Value(rect.Width);
        _text.Append('x');
        return Value(rect.Height);
    }

    DrawTrace Radius(CornerRadius radius) =>
        radius == default
            ? this
            : Word("radius")
                .Number(radius.TopLeft)
                .Comma(radius.TopRight)
                .Comma(radius.BottomRight)
                .Comma(radius.BottomLeft);

    DrawTrace Edges(Thickness edges) =>
        Number(edges.Left).Comma(edges.Top).Comma(edges.Right).Comma(edges.Bottom);

    DrawTrace Texels(Rect source, RenderTexture texture)
    {
        Word("from").Box(source).Word("of");
        _text.Append(' ').Append(texture.Width).Append('x').Append(texture.Height);
        return this;
    }

    DrawTrace Color(Color color)
    {
        _text.Append(' ').Append('#');
        _text.Append(color.R.ToString("x2", CultureInfo.InvariantCulture));
        _text.Append(color.G.ToString("x2", CultureInfo.InvariantCulture));
        _text.Append(color.B.ToString("x2", CultureInfo.InvariantCulture));
        if (color.A != 255)
            _text.Append(color.A.ToString("x2", CultureInfo.InvariantCulture));
        return this;
    }

    DrawTrace Paint(in Paint paint)
    {
        switch (paint.Kind)
        {
            case PaintKind.None:
                Word("none");
                break;
            case PaintKind.Solid:
                Color(paint.Color);
                break;
            case PaintKind.LinearGradient or PaintKind.RadialGradient:
                Word(paint.Kind == PaintKind.LinearGradient ? "linear" : "radial")
                    .Point(paint.Start.X, paint.Start.Y)
                    .Number(paint.End.X)
                    .Comma(paint.End.Y);
                if (paint.Space == GradientSpace.Absolute)
                    Word("absolute");
                foreach (var stop in paint.Stops!.Stops)
                    Number(stop.Offset).Color(stop.Color);
                break;
            case PaintKind.Image:
                var image = paint.Image!;
                Word("image");
                _text.Append(' ').Append(image.Source.ToString());
                Word("viewbox").Box(image.Viewbox);
                if (image.ViewboxUnits == GradientSpace.Absolute)
                    Word("absolute");
                Word(Named(image.Stretch))
                    .Word(Named(image.AlignmentX))
                    .Word(Named(image.AlignmentY));
                break;
        }
        if (paint.Kind != PaintKind.None && paint.Opacity < 1f)
            Word("opacity").Number(paint.Opacity);
        return this;
    }

    DrawTrace Shape(ReadOnlySpan<PathVerb> verbs, ReadOnlySpan<Vector2> points, FillRule rule)
    {
        var hash = 2166136261u;
        foreach (var verb in verbs)
            hash = (hash ^ (uint)verb) * 16777619u;
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var point in points)
        {
            hash = (hash ^ (uint)(int)MathF.Round(point.X * 100f)) * 16777619u;
            hash = (hash ^ (uint)(int)MathF.Round(point.Y * 100f)) * 16777619u;
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        if (points.IsEmpty)
            min = max = Vector2.Zero;
        Box(new Rect(min.X, min.Y, max.X - min.X, max.Y - min.Y));
        _text
            .Append(' ')
            .Append(verbs.Length)
            .Append(" verbs ")
            .Append(points.Length)
            .Append(" points #");
        _text.Append(hash.ToString("x8", CultureInfo.InvariantCulture));
        return Word(rule == FillRule.EvenOdd ? "even-odd" : "nonzero");
    }

    static bool IsTranslation(in Matrix3x2 transform) =>
        transform.M11 == 1f && transform.M12 == 0f && transform.M21 == 0f && transform.M22 == 1f;

    static float Degrees(float radians) => radians * (180f / MathF.PI);

    static string Named<T>(T value)
        where T : struct, Enum => Enum.GetName(value) ?? "?";

    static string Name(PenLineJoin join) =>
        join switch
        {
            PenLineJoin.Miter => "miter",
            PenLineJoin.Bevel => "bevel",
            PenLineJoin.Round => "round",
            _ => "?",
        };

    static string Name(PenLineCap cap) =>
        cap switch
        {
            PenLineCap.Flat => "flat",
            PenLineCap.Square => "square",
            PenLineCap.Round => "round",
            PenLineCap.Triangle => "triangle",
            _ => "?",
        };
}
