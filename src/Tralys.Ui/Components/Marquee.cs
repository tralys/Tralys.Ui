using System.Numerics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Components;

public readonly record struct MarqueeProps
{
    public Length Width { get; init; }
}

/// <summary>Shows what it holds at its own width, and while that is wider than the marquee, scrolls it across and back
/// so all of it reads in turn: it holds 1.5 s, scrolls to the far end at 30 units a second, holds, and scrolls back,
/// for as long as it overflows and shows, at the context's animation speed. Both edges fade out over 12 units while it
/// overflows. The scroll moves only the drawing, on whole device pixels, and starts over when the overflow
/// changes.</summary>
[Children]
public sealed class Marquee : Component<MarqueeProps>
{
    const float Speed = 30f;
    const float Hold = 1.5f;
    const float EdgeFade = 12f;

    protected override void Render(Ui ui, in MarqueeProps p)
    {
        var scroller = ui.Remember(static () => new Scroller());
        ui.Effect(scroller, 0, static (s, _, cx) => s.View = cx.View);
        var frame = ui.Ref();
        var content = ui.Ref();
        var width = frame.Size.Width;
        var overflow = MathF.Max(0f, content.Size.Width - width);
        var offset = 0f;
        if (overflow > 0f)
        {
            var travel = overflow / Speed;
            var at = ui.Loop(2f * (Hold + travel), key: BitConverter.SingleToInt32Bits(overflow));
            offset = Along(at * 2f * (Hold + travel), travel) * overflow;
            var scale = scroller.View?.Scale ?? 1f;
            offset = MathF.Round(offset * scale) / scale;
        }
        using (
            ui.Box(
                width: p.Width,
                overflow: Overflow.Clip,
                mask: overflow > 0f && width > 0f ? scroller.Fade(width) : default(Paint),
                nodeRef: frame
            )
        )
        using (
            ui.Box(
                shrink: 0f,
                transform: offset > 0f ? Transform.Translate(-offset, 0f) : default,
                nodeRef: content
            )
        )
            ui.Children();
    }

    static float Along(float time, float travel) =>
        time < Hold ? 0f
        : time < Hold + travel ? (time - Hold) / travel
        : time < 2f * Hold + travel ? 1f
        : MathF.Max(0f, 1f - (time - 2f * Hold - travel) / travel);

    [FramePath]
    sealed class Scroller
    {
        static readonly Color Clear = Color.FromArgb(0, 255, 255, 255);
        static readonly Color Opaque = Color.FromRgb(255, 255, 255);

        internal UiView? View;
        float _fadeWidth = float.NaN;
        Paint _fade;

        public Paint Fade(float width)
        {
            if (width == _fadeWidth)
                return _fade;
            _fadeWidth = width;
            var share = MathF.Min(EdgeFade / width, 0.5f);
            _fade = Paint.Linear(
                new GradientStops([
                    new ColorStop(0f, Clear),
                    new ColorStop(share, Opaque),
                    new ColorStop(1f - share, Opaque),
                    new ColorStop(1f, Clear),
                ]),
                Vector2.Zero,
                Vector2.UnitX
            );
            return _fade;
        }
    }
}
