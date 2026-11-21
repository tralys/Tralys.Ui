using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Media;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

/// <summary>How an image fills its box, as CSS <c>object-fit</c>: centred on each axis, what overflows the box cut off
/// evenly on both sides.</summary>
public enum ObjectFit : byte
{
    /// <summary>Stretched to the box on each axis.</summary>
    Fill,

    /// <summary>Scaled to fit inside the box, keeping its aspect.</summary>
    Contain,

    /// <summary>Scaled to cover the box, keeping its aspect; its middle shows.</summary>
    Cover,

    /// <summary>At a unit per texel; its middle shows.</summary>
    None,

    /// <summary>As <see cref="Contain"/>, but never scaled up.</summary>
    ScaleDown,
}

/// <summary>A sprite sheet an image plays: <paramref name="FrameCount"/> frames of <paramref name="FrameSize"/>
/// texels, <paramref name="Columns"/> to a row, cut from the image's source rectangle at
/// <paramref name="FramesPerSecond"/> of its context's animation time. Every sprite at one rate turns its frame on
/// the same advance, and a turn draws the image again without rendering.</summary>
/// <param name="FrameSize">One frame's texels.</param>
/// <param name="Columns">The frames in one row of the sheet.</param>
/// <param name="FrameCount">The frames played, row after row from <paramref name="Row"/>.</param>
/// <param name="Row">The sheet row the first frame is in.</param>
/// <param name="FramesPerSecond">0 holds the first frame.</param>
/// <param name="Paused">Holds the frame shown; playing again starts over from the first.</param>
/// <param name="TexelInset">Texels each frame's source is pulled in by on every side, so filtering never reads the
/// frame beside it on a sheet with no gutters; 0.5 keeps a sample a half texel inside.</param>
[FramePath]
public readonly record struct Sprite(
    Size FrameSize,
    int Columns,
    int FrameCount,
    int Row = 0,
    float FramesPerSecond = 10f,
    bool Paused = false,
    float TexelInset = 0f
);

/// <summary>The image primitive.</summary>
[FramePath]
public static class ImagePrimitive
{
    /// <summary>An image: a key the context's <see cref="ITextureProvider"/> resolves once, or a texture in hand. Its
    /// intrinsic size is <paramref name="sourceRect"/> (the whole texture by default) at a unit per texel, and
    /// <paramref name="fit"/> places it in its box as CSS <c>object-fit</c> does, centred where it fits and from the
    /// left or top where it overflows. With <paramref name="slice"/> it is cut into nine and stretched or tiled to its
    /// box, its intrinsic size its corners'. A key nothing resolves draws nothing and keeps the size its props give it.
    /// It hits over what it draws, once resolved. With <paramref name="sprite"/> it plays frames cut from that
    /// rectangle, each standing in for it, starting on the first when it mounts or its sprite changes, and holding its
    /// frame while hidden, collapsed or faulted.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The sprite's frame size isn't positive, its columns or frame count
    /// are below 1, its row is negative, or its rate is negative or not finite.</exception>
    public static void Image(
        this Ui ui,
        ImageSource source,
        ObjectFit fit = default,
        Rect sourceRect = default,
        NineSlice? slice = null,
        Sprite? sprite = null,
        Callback onClick = default,
        float grow = 0,
        float? shrink = null,
        Length basis = default,
        AlignSelf alignSelf = default,
        AlignSelf justifySelf = default,
        Thickness margin = default,
        Position position = default,
        Length left = default,
        Length top = default,
        Length right = default,
        Length bottom = default,
        int zIndex = 0,
        int column = 0,
        int row = 0,
        int columnSpan = 0,
        int rowSpan = 0,
        bool hidden = false,
        bool collapsed = false,
        string? id = null,
        Length width = default,
        Length height = default,
        Length minWidth = default,
        Length minHeight = default,
        Length maxWidth = default,
        Length maxHeight = default,
        float aspectRatio = 0,
        float? opacity = null,
        Transform transform = default,
        Vector2? transformOrigin = null,
        Cursor? cursor = null,
        PointerEvents pointerEvents = default,
        NodeRef? nodeRef = null,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var b = ui.Builder;
        if (b.Discarding)
            return;
        var node = b.Claim(NodeKind.Image, null, key, file, line, out _);
        var view = b.View;
        if (node.Payload is not ImagePayload payload)
        {
            payload = Pool<ImagePayload>.TryRent() ?? new ImagePayload();
            node.Payload = payload;
        }
        var changed = payload.Write(view, node, source, fit, sourceRect, slice, sprite);
        if (changed)
            view.MarkPaint(node);
        Primitives.Write(
            view,
            node,
            new LayoutStyle
            {
                Width = width,
                Height = height,
                MinWidth = minWidth,
                MinHeight = minHeight,
                MaxWidth = maxWidth,
                MaxHeight = maxHeight,
                AspectRatio = aspectRatio > 0f ? aspectRatio : payload.AspectRatio,
                IntrinsicSize = payload.IntrinsicSize,
            },
            new VisualStyle
            {
                Opacity = opacity,
                Transform = transform,
                TransformOrigin = transformOrigin,
            },
            new InheritStyle { PointerEvents = pointerEvents },
            new InteractionProps { OnClick = onClick, Cursor = cursor },
            new Placement
            {
                Grow = grow,
                Shrink = shrink,
                Basis = basis,
                AlignSelf = alignSelf,
                JustifySelf = justifySelf,
                Margin = margin,
                Position = position,
                Left = left,
                Top = top,
                Right = right,
                Bottom = bottom,
                ZIndex = zIndex,
                Column = column,
                Row = row,
                ColumnSpan = columnSpan,
                RowSpan = rowSpan,
                Hidden = hidden,
                Collapsed = collapsed,
                Id = id,
            },
            false,
            nodeRef,
            changed
        );
    }
}

[FramePath]
internal sealed class ImagePayload : NodePayload
{
    ImageSource _source;
    ObjectFit _fit;
    Rect _sourceRect;
    NineSlice? _slice;
    Sprite? _sprite;
    RenderTexture? _texture;
    long _start;
    int _frame;
    bool _written;
    internal Node? Node;
    internal int SpriteSlot = -1;
    internal bool Resting;

    internal RenderTexture? Texture => _texture;

    bool Plays =>
        _sprite is { Paused: false, FrameCount: > 1, FramesPerSecond: > 0f }
        && _texture is not null;

    internal Size IntrinsicSize
    {
        get
        {
            if (_texture is not { } texture)
                return default;
            if (_slice is { } slice)
                return new Size(
                    (slice.Slice.Left + slice.Slice.Right) * slice.Scale,
                    (slice.Slice.Top + slice.Slice.Bottom) * slice.Scale
                );
            return Source(texture).Size;
        }
    }

    // A sliced image stretches to any box, so only a whole one has an aspect of its own.
    internal float AspectRatio
    {
        get
        {
            if (_slice is not null || _texture is not { } texture)
                return 0f;
            var size = Source(texture).Size;
            return size.Width > 0f && size.Height > 0f ? size.Width / size.Height : 0f;
        }
    }

    internal bool Write(
        UiView view,
        Node node,
        in ImageSource source,
        ObjectFit fit,
        Rect sourceRect,
        NineSlice? slice,
        Sprite? sprite
    )
    {
        if (sprite is { } next)
            Check(next);
        Node = node;
        var changed = false;
        if (!_written || _source != source)
        {
            _written = true;
            _source = source;
            _texture = view.Context.ResolveTexture(source);
            changed = true;
        }
        if (_fit != fit || _sourceRect != sourceRect || !Nullable.Equals(_slice, slice))
        {
            _fit = fit;
            _sourceRect = sourceRect;
            _slice = slice;
            changed = true;
        }
        if (!Nullable.Equals(_sprite, sprite))
        {
            var pausing =
                sprite is { Paused: true } now
                && _sprite is { } was
                && was with { Paused = true } == now;
            _sprite = sprite;
            if (!pausing && sprite is { } started)
            {
                _frame = 0;
                _start = Step(view.Context.AnimationTime, started.FramesPerSecond);
            }
            changed = true;
        }
        if (Plays)
            view.PlaySprite(this);
        else
            view.StopSprite(this);
        return changed;
    }

    static void Check(in Sprite sprite)
    {
        if (
            !(sprite.FrameSize.Width > 0f && sprite.FrameSize.Height > 0f)
            || !float.IsFinite(sprite.FrameSize.Width)
            || !float.IsFinite(sprite.FrameSize.Height)
            || sprite.Columns < 1
            || sprite.FrameCount < 1
            || sprite.Row < 0
            || !(sprite.FramesPerSecond >= 0f && float.IsFinite(sprite.FramesPerSecond))
            || !(sprite.TexelInset >= 0f && float.IsFinite(sprite.TexelInset))
        )
            throw Faults.OutOfRange(
                "sprite",
                sprite,
                "A sprite's frame size is positive and finite, its columns and frame count at least 1, its row not negative, and its rate and texel inset finite and not negative."
            );
    }

    // Seconds summed advance by advance can land a hair short of a step they reached.
    static long Step(double time, float framesPerSecond) =>
        (long)Math.Floor(time * framesPerSecond + 1e-6);

    internal bool Turn(double time, bool reduced)
    {
        if (_sprite is not { } sprite)
            return false;
        var frame = reduced
            ? 0
            : (int)((Step(time, sprite.FramesPerSecond) - _start) % sprite.FrameCount);
        if (frame == _frame)
            return false;
        _frame = frame;
        return true;
    }

    Rect Source(RenderTexture texture)
    {
        var sheet =
            _sourceRect == default ? new Rect(0f, 0f, texture.Width, texture.Height) : _sourceRect;
        if (_sprite is not { } sprite)
            return sheet;
        var size = sprite.FrameSize;
        return new Rect(
            sheet.X + _frame % sprite.Columns * size.Width,
            sheet.Y + (sprite.Row + _frame / sprite.Columns) * size.Height,
            size.Width,
            size.Height
        );
    }

    internal void Record(DrawingContext dc, Size size)
    {
        if (_texture is not { } texture)
            return;
        var source = Source(texture);
        var inset = _sprite is { } sprite ? sprite.TexelInset : 0f;
        if (_slice is { } slice)
        {
            dc.DrawImage(
                texture,
                new Rect(size),
                source,
                inset > slice.TexelInset ? slice with { TexelInset = inset } : slice
            );
            return;
        }
        if (Place(size, source, out var destination, out var cropped))
            dc.DrawImage(
                texture,
                destination,
                inset > 0f ? Inside(cropped, source, inset) : cropped
            );
    }

    static Rect Inside(Rect cropped, Rect cell, float inset)
    {
        var x = MathF.Min(inset, cell.Width * 0.5f);
        var y = MathF.Min(inset, cell.Height * 0.5f);
        return Rect.FromEdges(
            MathF.Max(cropped.X, cell.X + x),
            MathF.Max(cropped.Y, cell.Y + y),
            MathF.Min(cropped.Right, cell.Right - x),
            MathF.Min(cropped.Bottom, cell.Bottom - y)
        );
    }

    internal bool Hits(Vector2 point, Size size)
    {
        if (_texture is not { } texture)
            return false;
        if (_slice is not null)
            return true;
        return Place(size, Source(texture), out var destination, out _)
            && destination.Contains(point);
    }

    // What overflows the box is cropped from the source, never drawn outside it.
    bool Place(Size box, Rect source, out Rect destination, out Rect cropped)
    {
        destination = cropped = default;
        if (source.Width <= 0f || source.Height <= 0f)
            return false;
        var fitX = box.Width / source.Width;
        var fitY = box.Height / source.Height;
        float scaleX,
            scaleY;
        switch (_fit)
        {
            case ObjectFit.Contain:
                scaleX = scaleY = MathF.Min(fitX, fitY);
                break;
            case ObjectFit.Cover:
                scaleX = scaleY = MathF.Max(fitX, fitY);
                break;
            case ObjectFit.None:
                scaleX = scaleY = 1f;
                break;
            case ObjectFit.ScaleDown:
                scaleX = scaleY = MathF.Min(1f, MathF.Min(fitX, fitY));
                break;
            default:
                scaleX = fitX;
                scaleY = fitY;
                break;
        }
        if (!(scaleX > 0f && scaleY > 0f))
            return false;
        Axis(
            box.Width,
            source.X,
            source.Width,
            scaleX,
            out var x,
            out var width,
            out var u,
            out var texelsX
        );
        Axis(
            box.Height,
            source.Y,
            source.Height,
            scaleY,
            out var y,
            out var height,
            out var v,
            out var texelsY
        );
        destination = new Rect(x, y, width, height);
        cropped = new Rect(u, v, texelsX, texelsY);
        return true;
    }

    static void Axis(
        float box,
        float start,
        float length,
        float scale,
        out float at,
        out float drawn,
        out float from,
        out float texels
    )
    {
        var full = length * scale;
        if (full <= box)
        {
            at = (box - full) / 2f;
            drawn = full;
            from = start;
            texels = length;
            return;
        }
        at = 0f;
        drawn = box;
        texels = box / scale;
        from = start + (length - texels) / 2f;
    }

    public override void Release(UiView view)
    {
        view.StopSprite(this);
        _source = default;
        _fit = default;
        _sourceRect = default;
        _slice = null;
        _sprite = null;
        _texture = null;
        _start = 0;
        _frame = 0;
        _written = false;
        Node = null;
        Pool<ImagePayload>.Return(this);
    }
}
