using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Events>("Features")]
static class EventsStories
{
    static readonly string[] CardNames = ["Ember", "Frost", "Gale", "Moss", "Slate", "Tide"];

    [FramePath]
    static void Count(State<int> count, int by) => count.Value = (count.Peek + by) % 10;

    [FramePath]
    public static void Commands(Ui ui)
    {
        var count = ui.State(0);
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 10,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "A click adds one on release; keys on the focused tile change the count; the count resets at 10"
            );
            using (ui.Box(gap: 8))
            {
                ui.ActionTile(
                    label: "Press for +1",
                    onClick: Callback.Of(count, static c => Count(c, 1))
                );
                ui.ActionTile(
                    label: "Focus me: Space +1, Backspace −1, Esc resets",
                    onKeyDown: Handler<KeyEvent>.Of(
                        count,
                        static (State<int> c, ref KeyEvent e) =>
                        {
                            switch (e.Key)
                            {
                                case Key.Space:
                                    Count(c, 1);
                                    break;
                                case Key.BackSpace:
                                    c.Value = Math.Max(0, c.Peek - 1);
                                    break;
                                case Key.Escape:
                                    c.Value = 0;
                                    break;
                                default:
                                    return;
                            }
                            e.PreventDefault();
                        }
                    )
                );
            }
            ui.Text($"Count {count.Value}", fontSize: 20);
        }
    }

    [FramePath]
    public static void Hover(Ui ui)
    {
        var t = ui.Theme;
        var field = ui.Ref();
        var typed = ui.Remember(static () => new ObservableText("Hover to focus, then type"));
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 10,
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "Hover lifts each card under the pointer; hovering the field gives it the focus"
            );
            using (ui.Box(gap: 12, margin: new Thickness(0, 8, 0, 0)))
            {
                ui.HoverLift(lift: 6);
                ui.HoverLift(lift: 12);
                ui.HoverLift(lift: 20, accent: true);
            }
            using (
                ui.Box(
                    width: 260,
                    onHoverChange: Callback<bool>.Of(
                        field,
                        static (f, on) =>
                        {
                            if (on)
                                f.Focus();
                        }
                    )
                )
            )
                ui.Input(text: typed, nodeRef: field, grow: 1);
        }
    }

    [FramePath]
    public static void DragDrop(Ui ui)
    {
        var t = ui.Theme;
        var board = ui.Remember(static () => new CardBoard());
        board.Swaps = ui.State(0);
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 10,
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "Drag a card onto another to swap them: the card under the drag lifts, and its preview stays on the pointer"
            );
            using (
                ui.Box(
                    wrap: FlexWrap.Wrap,
                    align: Align.Start,
                    width: 336,
                    background: t.Color(ColorToken.BgSunken)
                )
            )
            {
                for (var i = 0; i < board.Order.Length; i++)
                    ui.CardSlot(board: board, at: i, margin: new Thickness(8, 2, 8, 8), key: i);
            }
        }
    }

    [FramePath]
    public static void DoubleClick(Ui ui)
    {
        var count = ui.State(0);
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 10,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "A double click on the row adds one; one on the button inside it is the button's"
            );
            using (
                ui.Box(
                    gap: 8,
                    width: 320,
                    background: ui.Theme.Color(ColorToken.BgRaised),
                    onDoubleClick: Callback.Of(count, static c => Count(c, 1))
                )
            )
            {
                ui.Text(
                    $"Count {count.Value}",
                    margin: new Thickness(12, 8, 12, 8),
                    alignSelf: AlignSelf.Center
                );
                ui.Button(text: "Reset", onClick: Callback.Of(count, static c => c.Value = 0));
            }
        }
    }

    /// <summary>The cards in their slots, by index into <see cref="CardNames"/>; a swap counts in
    /// <see cref="Swaps"/>, which the slots read.</summary>
    internal sealed class CardBoard
    {
        public State<int>? Swaps { get; set; }

        public int[] Order { get; } = [0, 1, 2, 3, 4, 5];

        public string NameAt(int slot) => CardNames[Order[slot]];

        [FramePath]
        public void Swap(int from, int to)
        {
            (Order[from], Order[to]) = (Order[to], Order[from]);
            Swaps!.Value = Swaps.Peek + 1;
        }
    }
}

internal readonly record struct ActionTileProps
{
    public TextValue Label { get; init; }
    public Callback OnClick { get; init; }
    public Handler<KeyEvent> OnKeyDown { get; init; }
}

/// <summary>A focusable tile that outlines on hover, darkens while pressed and thickens its focus border.</summary>
internal sealed class ActionTile : Component<ActionTileProps>
{
    protected override void Render(Ui ui, in ActionTileProps p)
    {
        var t = ui.Theme;
        var focused = ui.IsFocused;
        using (
            ui.Box(
                padding: new Thickness(12, 8, 12, 8),
                radius: t.Radius(RadiusToken.Md),
                background: t.Color(ui.IsPressed ? ColorToken.BgSubtle : ColorToken.BgRaised),
                border: new(
                    focused ? 2 : 1,
                    t.Color(
                        focused ? ColorToken.BorderFocus
                        : ui.IsHovered ? ColorToken.BorderHover
                        : ColorToken.Border
                    )
                ),
                focusable: true,
                onClick: p.OnClick,
                onKeyDown: p.OnKeyDown
            )
        )
            ui.Text(p.Label);
    }
}

internal readonly record struct HoverLiftProps
{
    public float Lift { get; init; }
    public bool Accent { get; init; }
}

/// <summary>A card that rises by <see cref="HoverLiftProps.Lift"/> while the pointer is over its slot.</summary>
internal sealed class HoverLift : Component<HoverLiftProps>
{
    protected override void Render(Ui ui, in HoverLiftProps p)
    {
        var t = ui.Theme;
        using (ui.Box(onHoverChange: Callback<bool>.Of(static _ => { })))
        {
            using (
                ui.Box(
                    width: 96,
                    height: 64,
                    radius: t.Radius(RadiusToken.Md),
                    background: t.Color(p.Accent ? ColorToken.BgAccentMuted : ColorToken.BgRaised),
                    border: new(1, t.Color(p.Accent ? ColorToken.BorderAccent : ColorToken.Border)),
                    transform: ui.IsHovered ? Transform.Translate(0, -p.Lift) : default
                )
            ) { }
        }
    }
}

internal readonly record struct CardSlotProps
{
    public EventsStories.CardBoard? Board { get; init; }
    public int At { get; init; }
}

/// <summary>One card's slot: a drag source carrying its index, and a drop target that swaps the dragged card with
/// its own, its card lifting while a drag is over it.</summary>
internal sealed class CardSlot : Component<CardSlotProps>
{
    static readonly object[] Indices = [0, 1, 2, 3, 4, 5];

    static readonly Action<Ui, string> Ghost = static (ui, name) => Card(ui, name, true);

    protected override void Render(Ui ui, in CardSlotProps p)
    {
        var board = p.Board!;
        _ = board.Swaps!.Value;
        var name = board.NameAt(p.At);
        var self = ui.Self(p);
        using (
            ui.Box(
                direction: Direction.Column,
                width: 96,
                height: 70,
                padding: ui.IsDropTarget ? new Thickness(0, 0, 0, 6) : new Thickness(0, 6, 0, 0),
                dragData: Indices[p.At],
                dragEffects: DragEffect.Move,
                dragPreview: Slot.Of(name, Ghost),
                onDragOver: Handler<DropEvent>.Of(
                    static (ref DropEvent e) => e.Effect = DragEffect.Move
                ),
                onDrop: Handler<DropEvent>.Of(
                    self,
                    static (Host<CardSlotProps> h, ref DropEvent e) =>
                    {
                        if (e.Data is int from)
                            h.Props.Board!.Swap(from, h.Props.At);
                    }
                )
            )
        )
            Card(ui, name, false);
    }

    [FramePath]
    static void Card(
        Ui ui,
        string name,
        bool lifted,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                grow: 1,
                width: lifted ? 96 : Length.Auto,
                height: lifted ? 64 : Length.Auto,
                radius: t.Radius(RadiusToken.Md),
                background: t.Color(lifted ? ColorToken.BgAccentMuted : ColorToken.BgRaised),
                border: new(1, t.Color(lifted ? ColorToken.BorderAccent : ColorToken.Border)),
                key: key,
                file: file,
                line: line
            )
        )
            ui.Text(name);
    }
}
