using System.Numerics;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>When the arrow keys, Home and End select the tab they move the focus to.</summary>
public enum TabActivation : byte
{
    /// <summary>As the focus arrives, by those keys or by Tab; Enter, Space and gamepad South on the focused tab pass
    /// on.</summary>
    Automatic,

    /// <summary>Only on Enter, Space, a press or a tap; the keys move the focus alone.</summary>
    Manual,
}

public readonly record struct TabsProps
{
    /// <summary>The selected tab's value; null leaves it to the tabs, starting at <see cref="DefaultValue"/>. An
    /// unkeyed value shows no tab selected, for a strip whose window shows something else; while a keyed one names no
    /// tab, the first tab that can take the focus shows selected.</summary>
    public UiKey? Value { get; init; }

    public UiKey DefaultValue { get; init; }

    public Callback<UiKey> OnValueChange { get; init; }

    public TabActivation ActivationMode { get; init; }

    /// <summary>Sizes every tab; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    public bool Disabled { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    /// <summary>Whether a tab dragged along the strip reports <see cref="OnReorder"/> as it passes a neighbour's
    /// midpoint. A finger or pen drags a tab only after holding it, so a swipe scrolls what it can.</summary>
    public bool Reorderable { get; init; }

    /// <summary>The strips a tab dragged from this one can land on, this one among them while it is shown.</summary>
    public TabDragGroup? DragGroup { get; init; }

    /// <summary>The name <see cref="DragGroup"/>'s reports give this strip.</summary>
    public UiKey GroupKey { get; init; }

    public Callback<TabReorder> OnReorder { get; init; }

    /// <summary>A tab dropped on another strip of <see cref="DragGroup"/>, which the app moves there.</summary>
    public Callback<TabMove> OnTabMove { get; init; }

    /// <summary>A tab dropped over no strip.</summary>
    public Callback<TabTearOff> OnTabTearOff { get; init; }

    /// <summary>Each move of a tab drag, after the strips have shown where it would land.</summary>
    public Callback<TabDrag> OnTabDrag { get; init; }

    /// <summary>A tab drag ending, once, however it ended and after any drop it reported.</summary>
    public Callback<UiKey> OnTabDragEnd { get; init; }
}

public readonly record struct TabsListProps
{
    /// <summary>The handle the strip's sideways scroll is read and requested through, as a scroll area's; its own when
    /// null.</summary>
    public ScrollHandle? Scroll { get; init; }

    public Callback<ScrollChange> OnScrollChange { get; init; }

    /// <summary>A wheel turn over the strip, before it scrolls by itself: a handler that steps it by a tab calls
    /// <c>PreventDefault</c>.</summary>
    public Handler<WheelEvent> OnWheel { get; init; }
}

public readonly record struct TabsTabProps
{
    public UiKey Value { get; init; }

    public TextValue Text { get; init; }

    /// <summary>What the tab shows when it has no <see cref="Text"/>.</summary>
    public Slot Content { get; init; }

    /// <summary>The selected label and underline; the theme's accent by default.</summary>
    public Color? Accent { get; init; }

    public bool Disabled { get; init; }

    public Length Width { get; init; }
}

public readonly record struct TabsPanelProps
{
    public UiKey Value { get; init; }
}

/// <summary>A moved tab: <see cref="From"/> and <see cref="To"/> count the strip's tabs in order.</summary>
public readonly record struct TabReorder(UiKey Value, int From, int To);

/// <summary>A tab dropped on the strip named <see cref="Target"/>, at <see cref="Gap"/> among its tabs.</summary>
public readonly record struct TabMove(UiKey Value, UiKey Target, int Gap);

/// <summary>A tab dropped over no strip, at <see cref="Position"/> in view units.</summary>
public readonly record struct TabTearOff(UiKey Value, Vector2 Position);

/// <summary>A tab dragged to <see cref="Position"/>, in view units, over the strip named <see cref="Over"/>, or null
/// over none.</summary>
public readonly record struct TabDrag(UiKey Value, Vector2 Position, UiKey? Over);

internal readonly record struct TabsContext(
    TabsModel? Model,
    UiKey Selected,
    ComponentSize Size,
    bool Drags,
    TabActivation Activation
);

/// <summary>A strip of tabs over the panel of the selected one, controlled by <see cref="TabsProps.Value"/>, which
/// reports through <see cref="TabsProps.OnValueChange"/> and is its own when null. Its children are a
/// <c>ui.TabsList</c> of <c>ui.TabsTab</c>s, and a <c>ui.TabsPanel</c> per value that shows its children only while
/// that tab is selected.</summary>
/// <remarks>The list is one Tab stop, landing on the selected tab. A left press selects as it lands, and a finger or pen
/// on its tap; Enter and Space select the focused tab under <see cref="TabActivation.Manual"/>, and otherwise pass on to
/// the form or dialog around it. The arrows move the focus between tabs, wrapping, and Home and End to
/// the ends, each selecting the tab it reaches unless <see cref="TabsProps.ActivationMode"/> is
/// <see cref="TabActivation.Manual"/>; from inside a panel, Home and End select an end without moving the focus.
/// Ctrl+Tab and Ctrl+Shift+Tab select and focus the next and previous tab, wrapping. Each passes over a tab that is
/// disabled, hidden or collapsed. A removed selected tab reports the tab now in its place, or the one before it.
/// The list scrolls sideways, and a finger swipes it.</remarks>
[Children]
public sealed class Tabs : Component<TabsProps>
{
    internal static readonly ContextKey<TabsContext> Context = new("Tabs");

    protected override void Render(Ui ui, in TabsProps p)
    {
        var t = ui.Theme;
        var model = ui.Remember(static () => new TabsModel());
        model.Props = p;
        model.Own = ui.State(p.DefaultValue);
        model.Version = ui.State(0);
        model.Seam = ui.State<TabSeam>(default);
        model.Root = ui.Ref();
        model.List = ui.Ref();
        model.Join(p.DragGroup);
        ui.Effect(model, 0, static (m, _, cx) => m.View = cx.View);
        ui.Effect(model, model.Losses, static (m, _, _) => m.ReportLost());
        var selected = p.Value is { IsKeyed: false }
            ? model.ResolveNone()
            : model.Resolve(p.Value ?? model.Own.Value);
        ui.Provide(
            Context,
            new TabsContext(
                model,
                selected,
                p.Size ?? ComponentSize.Md,
                p.Reorderable || p.DragGroup is not null,
                p.ActivationMode
            )
        );
        var seam = model.Seam.Value;
        using (
            ui.Box(
                direction: Direction.Column,
                width: p.Width,
                height: p.Height,
                color: ui.IsDisabled ? t.Color(ColorToken.FgDisabled) : default(Paint),
                onKeyDown: model.KeyDown,
                nodeRef: model.Root
            )
        )
        {
            using (ui.Box(direction: Direction.Column, grow: 1))
                ui.Children();
            if (seam.Shown)
                ui.Canvas(
                    state: new SeamLook(seam, t.Color(ColorToken.BorderAccent)),
                    draw: SeamLook.Draw,
                    position: Position.Absolute,
                    left: 0f,
                    top: 0f,
                    right: 0f,
                    bottom: 0f,
                    pointerEvents: PointerEvents.None
                );
        }
    }

    readonly record struct SeamLook(TabSeam Seam, Color Color)
    {
        public static readonly CanvasDraw<SeamLook> Draw = static (dc, s, _) =>
            dc.DrawLine(
                new Vector2(s.Seam.X, s.Seam.Top),
                new Vector2(s.Seam.X, s.Seam.Top + s.Seam.Length),
                1.5f,
                s.Color
            );
    }

    /// <summary>The row of a <see cref="Tabs"/>' tabs: one Tab stop, scrolling sideways when they overflow it.</summary>
    [Children]
    public sealed class List : Component<TabsListProps>
    {
        protected override void Render(Ui ui, in TabsListProps p)
        {
            var c = ui.Use(Context);
            using (ui.Box())
            using (
                ui.Box(
                    grow: 1,
                    overflow: Overflow.ScrollX,
                    panning: Panning.HorizontalOnly,
                    tabNavigation: Navigation.Once,
                    scroll: p.Scroll,
                    onScrollChange: p.OnScrollChange,
                    onWheel: p.OnWheel,
                    nodeRef: c.Model?.List
                )
            )
                ui.Children();
        }
    }

    /// <summary>A tab: its label lit while hovered, and in the accent over an underline while selected, or on the
    /// skin's <see cref="SkinPart.TabActive"/> art when there is some; a disabled tab's label is greyed out.</summary>
    public sealed class Tab : Component<TabsTabProps>
    {
        protected override void Render(Ui ui, in TabsTabProps p)
        {
            var c = ui.Use(Context);
            var t = ui.Theme;
            var m = t.Metrics(c.Size);
            var tab = ui.Remember(c.Model, static model => new TabEntry(model));
            tab.Value = p.Value;
            tab.Ref = ui.Ref();
            var selected = p.Value.IsKeyed && c.Selected == p.Value;
            var hovered = ui.IsHovered;
            var activeArt = selected ? t.Skin(SkinPart.TabActive) : null;
            var hoverArt = hovered && !selected ? t.Skin(SkinPart.TabHover) : null;
            var label =
                ui.IsDisabled ? t.Color(ColorToken.FgDisabled)
                : selected && activeArt is not null ? t.Color(ColorToken.FgOnAccent)
                : selected ? p.Accent ?? t.Color(ColorToken.FgAccent)
                : hovered ? t.Color(ColorToken.FgHover)
                : t.Color(ColorToken.Fg);
            using (
                ui.Box(
                    align: Align.Center,
                    justify: Justify.Center,
                    width: p.Width,
                    minHeight: m.Height,
                    padding: m.PaddingX,
                    color: ui.Animate(label),
                    fontSize: m.FontSize,
                    fontWeight: t.Weight(WeightToken.Interface),
                    cursor: Cursors.Hand,
                    selected: selected,
                    onClick: tab.Click,
                    pressKeys: c.Activation == TabActivation.Manual
                        ? PressKeys.EnterAndSpace
                        : PressKeys.None,
                    onPointerDown: tab.PointerDown,
                    onKeyDown: tab.KeyDown,
                    onFocusChange: tab.FocusChange,
                    onDrag: c.Drags ? tab.Drag : default,
                    nodeRef: tab.Ref
                )
            )
            {
                if (activeArt is { } active)
                    ui.Art(art: active);
                if (hoverArt is { } hover)
                    ui.Art(art: hover);
                if (!p.Text.IsNull)
                    ui.Text(p.Text);
                else
                    p.Content.Render(ui);
                if (selected)
                    using (
                        ui.Box(
                            height: 2f,
                            background: p.Accent ?? t.Color(ColorToken.BorderAccent),
                            pointerEvents: PointerEvents.None,
                            position: Position.Absolute,
                            left: 0f,
                            right: 0f,
                            bottom: 0f
                        )
                    ) { }
                if (ui.IsFocusVisible)
                    ui.FocusRing(radius: t.Radius(RadiusToken.Sm), inset: 0f);
            }
        }
    }

    /// <summary>The content of the tab whose value it shares, shown only while that tab is selected.</summary>
    [Children]
    public sealed class Panel : Component<TabsPanelProps>
    {
        protected override void Render(Ui ui, in TabsPanelProps p)
        {
            var c = ui.Use(Context);
            if (!p.Value.IsKeyed || c.Selected != p.Value)
                return;
            using (ui.Box(direction: Direction.Column, arrowNavigation: Navigation.Contained))
                ui.Children();
        }
    }
}

internal readonly record struct TabSeam(bool Shown, float X, float Top, float Length);
