using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct RadioProps
{
    /// <summary>What its <see cref="RadioGroup"/> reports when this radio is picked.</summary>
    public UiKey Value { get; init; }

    public TextValue Label { get; init; }

    /// <summary>What a <see cref="RadioGroupVariant.Tile"/> or <see cref="RadioGroupVariant.Card"/> option shows, or
    /// any option without a <see cref="Label"/>.</summary>
    public Slot Content { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>One choice of the <see cref="RadioGroup"/> around it, drawn in the group's
/// <see cref="RadioGroupVariant"/>: picked while the group's value is its <see cref="RadioProps.Value"/>. A press, Space
/// or Enter picks it, and so does the focus reaching it by an arrow key; picking never unpicks. Outside a group it shows
/// unpicked and picks nothing.</summary>
public sealed class Radio : Component<RadioProps>
{
    protected override void Render(Ui ui, in RadioProps p)
    {
        var t = ui.Theme;
        var group = ui.Use(RadioGroup.Context);
        var model = ui.Remember(static () => new RadioModel());
        model.Value = p.Value;
        model.Group = group;
        model.Join(group.Members);
        var m = t.Metrics(group.Size);
        var on = group.Value.IsKeyed && group.Value == p.Value;
        var ring = ui.IsFocusVisible;
        var radius = t.Radius(RadiusToken.Md);
        switch (group.Variant)
        {
            case RadioGroupVariant.Segmented:
                var text = ui.Animate(
                    t.Color(
                        ui.IsDisabled ? ColorToken.FgDisabled
                        : on ? ColorToken.FgAccent
                        : ui.IsHovered ? ColorToken.FgHover
                        : ColorToken.FgMuted
                    )
                );
                var lit = ui.Animate(on ? t.Color(ColorToken.BgAccentMuted) : default);
                using (
                    ui.Box(
                        direction: Direction.Column,
                        minHeight: m.Height - 2f,
                        fontSize: m.FontSize,
                        fontWeight: t.Weight(WeightToken.Interface),
                        cursor: Cursors.Hand,
                        selected: on,
                        onClick: model.Pick,
                        onFocusChange: model.Focused
                    )
                )
                using (
                    ui.Box(
                        align: Align.Center,
                        justify: Justify.Center,
                        grow: 1f,
                        margin: ring ? 0f : 1f,
                        padding: m.PaddingX,
                        background: lit.A == 0 ? default(Paint) : lit,
                        border: ring ? new Border(1f, t.Color(ColorToken.BorderFocus)) : default,
                        radius: t.Radius(RadiusToken.Sm)
                    )
                )
                    ui.LabelGlyphs(
                        text: p.Label,
                        content: p.Content,
                        iconLeft: default,
                        iconRight: default,
                        iconSize: m.FontSize,
                        color: text
                    );
                break;
            case RadioGroupVariant.Tile:
                var frame = ui.Animate(
                    t.Color(
                        ring ? ColorToken.BorderFocus
                        : on ? ColorToken.BorderAccent
                        : ui.IsHovered ? ColorToken.BorderHover
                        : ColorToken.Border
                    )
                );
                var dim = ui.Animate(on ? 0f : 1f);
                var tint = ui.Animate(ui.LabelColor(accent: on));
                using (
                    ui.Box(
                        overflow: Overflow.Clip,
                        radius: radius,
                        background: t.Color(ColorToken.BgSunken),
                        color: tint,
                        fontSize: m.FontSize,
                        fontWeight: t.Weight(WeightToken.Interface),
                        opacity: ui.IsDisabled ? 0.5f : null,
                        cursor: Cursors.Hand,
                        selected: on,
                        onClick: model.Pick,
                        onFocusChange: model.Focused
                    )
                )
                {
                    ui.LabelGlyphs(
                        text: p.Label,
                        content: p.Content,
                        iconLeft: default,
                        iconRight: default,
                        iconSize: m.FontSize,
                        color: default
                    );
                    ui.Cover(background: t.Color(ColorToken.BgScrim), opacity: dim);
                    ui.Cover(radius: radius, border: new Border(on ? 2f : 1f, frame));
                }
                break;
            case RadioGroupVariant.Card:
                var edge = ui.Animate(
                    t.Color(
                        ring ? ColorToken.BorderFocus
                        : on ? ColorToken.BorderSelected
                        : ui.IsHovered ? ColorToken.BorderHover
                        : ColorToken.BorderMuted
                    )
                );
                var ink = ui.Animate(ui.LabelColor(accent: on));
                using (
                    ui.Box(
                        align: Align.Center,
                        justify: Justify.Center,
                        radius: radius,
                        background: t.Card(lit: ui.IsHovered),
                        border: new Border(1f, edge),
                        color: ink,
                        fontSize: m.FontSize,
                        fontWeight: t.Weight(WeightToken.Interface),
                        opacity: ui.IsDisabled ? 0.5f : null,
                        cursor: Cursors.Hand,
                        selected: on,
                        onClick: model.Pick,
                        onFocusChange: model.Focused
                    )
                )
                    ui.LabelGlyphs(
                        text: p.Label,
                        content: p.Content,
                        iconLeft: default,
                        iconRight: default,
                        iconSize: m.FontSize,
                        color: default
                    );
                break;
            default:
                var label = ui.Animate(ui.LabelColor(accent: on));
                using (
                    ui.Box(
                        align: Align.Center,
                        gap: ToggleParts.LabelGap,
                        color: label,
                        fontSize: m.FontSize,
                        fontWeight: t.Weight(WeightToken.Interface),
                        opacity: ui.IsDisabled ? 0.5f : null,
                        cursor: Cursors.Hand,
                        selected: on,
                        onClick: model.Pick,
                        onFocusChange: model.Focused
                    )
                )
                {
                    ui.Indicator(
                        shape: ToggleShape.Round,
                        state: on ? CheckState.Checked : CheckState.Unchecked,
                        extent: ToggleParts.Extent(m)
                    );
                    ui.LabelGlyphs(
                        text: p.Label,
                        content: p.Content,
                        iconLeft: default,
                        iconRight: default,
                        iconSize: m.FontSize,
                        color: label
                    );
                    if (ring)
                        ui.FocusRing(radius: radius);
                }
                break;
        }
    }
}

[FramePath]
internal sealed class RadioModel : IDisposable
{
    internal readonly Callback Pick;
    internal readonly Callback<FocusChange> Focused;
    internal UiKey Value;
    internal RadioGroupContext Group;
    RadioGroup.Members? _joined;

    public RadioModel()
    {
        Pick = Callback.Of(this, static m => m.Select());
        Focused = Callback<FocusChange>.Of(
            this,
            static (m, change) =>
            {
                if (change.Focused && change.Cause == FocusCause.Arrow)
                    m.Select();
            }
        );
    }

    internal void Join(RadioGroup.Members? members)
    {
        if (ReferenceEquals(_joined, members))
            return;
        _joined?.Leave();
        _joined = members;
        members?.Join();
    }

    void Select()
    {
        if (!(Group.Value.IsKeyed && Group.Value == Value))
            Group.Select.Invoke(Value);
    }

    public void Dispose() => Join(null);
}

[FramePath]
internal static class RadioLooks
{
    static readonly ConditionalWeakTable<Theme, CardFills> Fills = [];

    /// <summary>The theme's card gradient, under the hover wash while <paramref name="lit"/>.</summary>
    public static Paint Card(this UiTheme t, bool lit)
    {
        var fills = Fills.GetValue(t.Theme, static theme => new CardFills(theme));
        return lit ? fills.Lit : fills.Rest;
    }

    public static void Cover(
        this Ui ui,
        Paint background = default,
        Border border = default,
        CornerRadius radius = default,
        float opacity = 1f,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                position: Position.Absolute,
                left: 0f,
                top: 0f,
                right: 0f,
                bottom: 0f,
                background: background,
                border: border,
                radius: radius,
                opacity: opacity,
                pointerEvents: PointerEvents.None,
                key: key,
                file: file,
                line: line
            )
        ) { }
    }

    sealed class CardFills(Theme theme)
    {
        public readonly Paint Rest = Vertical(
            theme.Color(ComponentColor.CardBgTop),
            theme.Color(ComponentColor.CardBgBottom)
        );

        public readonly Paint Lit = Vertical(
            theme.Color(ColorToken.StateHover).Over(theme.Color(ComponentColor.CardBgTop)),
            theme.Color(ColorToken.StateHover).Over(theme.Color(ComponentColor.CardBgBottom))
        );

        static Paint Vertical(Color top, Color bottom) =>
            Paint.Linear(
                new GradientStops([new ColorStop(0f, top), new ColorStop(1f, bottom)]),
                Vector2.Zero,
                Vector2.UnitY
            );
    }
}
