using System.Numerics;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>Which way a <see cref="Collapsible"/> opens from its header.</summary>
public enum ExpandDirection : byte
{
    Down,
    Up,
    Left,
    Right,
}

public readonly record struct CollapsibleProps
{
    public TextValue Header { get; init; }

    /// <summary>What the header shows after its arrow when it has no <see cref="Header"/> text.</summary>
    public Slot HeaderContent { get; init; }

    /// <summary>Whether the content shows; null leaves it to the collapsible, starting at
    /// <see cref="DefaultOpen"/>.</summary>
    public bool? Open { get; init; }

    public bool DefaultOpen { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    public ExpandDirection Direction { get; init; }

    /// <summary>The space round the content: 34 before it under a header above or below it, in line with the header's
    /// text, and 8 on its other sides, or 8 all round beside a header; 0 lays the content out in its parent's
    /// coordinates.</summary>
    public Thickness? ContentPadding { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A header that shows or hides the content written inside it: a press, Space or Enter on the header reports
/// the opposite of <see cref="CollapsibleProps.Open"/> through <see cref="CollapsibleProps.OnOpenChange"/>, and is the
/// collapsible's own when null. The content grows into view on the side <see cref="CollapsibleProps.Direction"/> names,
/// clipped rather than squashed, so what follows slides with it, while the header's arrow turns on the same motion;
/// closed, it is collapsed, out of the layout and the Tab order.</summary>
[Children]
public sealed class Collapsible : Component<CollapsibleProps>
{
    protected override void Render(Ui ui, in CollapsibleProps p)
    {
        var t = ui.Theme;
        var disclosure = ui.Remember(static () => new Disclosure());
        disclosure.Open = p.Open;
        disclosure.OnOpenChange = p.OnOpenChange;
        disclosure.Own = ui.State(p.DefaultOpen);
        var open = p.Open ?? disclosure.Own.Value;
        var shown = ui.Animate(
            open ? 1f : 0f,
            t.Motion(DurationToken.Disclosure, EasingToken.Settle)
        );
        var content = ui.Ref();
        var natural = content.Size;
        var vertical = p.Direction is ExpandDirection.Down or ExpandDirection.Up;
        var before = p.Direction is ExpandDirection.Up or ExpandDirection.Left;
        var partial = shown < 1f;
        using (
            ui.Box(
                direction: vertical ? Direction.Column : Direction.Row,
                align: vertical ? Align.Stretch : Align.Start,
                justify: p.Direction == ExpandDirection.Left ? Justify.End : Justify.Start,
                fontWeight: t.Weight(WeightToken.Interface),
                color: ui.IsDisabled ? t.Color(ColorToken.FgDisabled) : default(Paint)
            )
        )
        {
            if (!before)
                ui.CollapsibleHeader(
                    text: p.Header,
                    content: p.HeaderContent,
                    turn: shown,
                    direction: p.Direction,
                    onToggle: disclosure.Toggle
                );
            using (
                ui.Box(
                    direction: vertical ? Direction.Column : Direction.Row,
                    justify: before ? Justify.End : Justify.Start,
                    overflow: Overflow.Clip,
                    width: !vertical && partial ? natural.Width * shown : Length.Auto,
                    height: vertical && partial ? natural.Height * shown : Length.Auto,
                    hidden: shown <= 0f,
                    collapsed: shown <= 0f && natural != default
                )
            )
            using (
                ui.Box(
                    direction: Direction.Column,
                    align: p.Direction == ExpandDirection.Left ? Align.End : Align.Stretch,
                    shrink: 0f,
                    padding: p.ContentPadding
                        ?? (vertical ? new Thickness(34f, 8f, 8f, 8f) : new Thickness(8f)),
                    nodeRef: content
                )
            )
                ui.Children();
            if (before)
                ui.CollapsibleHeader(
                    text: p.Header,
                    content: p.HeaderContent,
                    turn: shown,
                    direction: p.Direction,
                    onToggle: disclosure.Toggle
                );
        }
    }
}

internal readonly record struct CollapsibleHeaderProps
{
    public TextValue Text { get; init; }

    public Slot Content { get; init; }

    public float Turn { get; init; }

    public ExpandDirection Direction { get; init; }

    public Callback OnToggle { get; init; }
}

internal sealed class CollapsibleHeader : Component<CollapsibleHeaderProps>
{
    protected override void Render(Ui ui, in CollapsibleHeaderProps p)
    {
        var t = ui.Theme;
        var m = t.Metrics(ComponentSize.Md);
        var radius = t.Radius(RadiusToken.Md);
        var wash = ui.Animate(
            ui.IsPressed ? t.Color(ColorToken.StatePressed)
            : ui.IsHovered ? t.Color(ColorToken.StateHover)
            : default
        );
        var arrow = ui.Animate(
            t.Color(
                ui.IsDisabled ? ColorToken.IconDisabled
                : ui.IsHovered ? ColorToken.IconHover
                : ColorToken.Icon
            )
        );
        var angle = p.Direction switch
        {
            ExpandDirection.Up => -90f,
            ExpandDirection.Left or ExpandDirection.Right => 180f,
            _ => 90f,
        };
        using (
            ui.Box(
                align: Align.Center,
                minHeight: m.Height,
                padding: m.PaddingX,
                radius: radius,
                background: wash.A == 0 ? default(Paint) : wash,
                cursor: Cursors.Hand,
                onClick: p.OnToggle
            )
        )
        {
            using (ui.Box(align: Align.Center, gap: 8f))
            {
                ui.Canvas(
                    state: new Arrow(arrow, p.Direction == ExpandDirection.Left),
                    draw: DrawArrow,
                    width: 14f,
                    height: 14f,
                    shrink: 0f,
                    transform: Transform.Rotate(p.Turn * angle),
                    pointerEvents: PointerEvents.None
                );
                ui.LabelGlyphs(
                    text: p.Text,
                    content: p.Content,
                    iconLeft: default,
                    iconRight: default,
                    iconSize: m.FontSize,
                    color: default
                );
            }
            if (ui.IsFocusVisible)
                ui.FocusRing(radius: radius);
        }
    }

    readonly record struct Arrow(Color Color, bool Left);

    static readonly CanvasDraw<Arrow> DrawArrow = static (dc, arrow, info) =>
    {
        var s = info.Size;
        var near = arrow.Left ? 0.625f : 0.375f;
        var far = arrow.Left ? 0.375f : 0.625f;
        var path = info.Path();
        path.MoveTo(new Vector2(s.Width * near, s.Height * 0.25f));
        path.LineTo(new Vector2(s.Width * far, s.Height * 0.5f));
        path.LineTo(new Vector2(s.Width * near, s.Height * 0.75f));
        dc.DrawPath(
            path,
            default,
            new Pen(
                arrow.Color,
                s.Width / 12f,
                PenLineJoin.Round,
                PenLineCap.Round,
                PenLineCap.Round
            )
        );
    };
}
