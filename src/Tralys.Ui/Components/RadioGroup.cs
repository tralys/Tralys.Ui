using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>How a <see cref="RadioGroup"/> draws and lays out its <see cref="Radio"/>s.</summary>
public enum RadioGroupVariant : byte
{
    /// <summary>A circle beside each label, stacked 16 apart.</summary>
    Default,

    /// <summary>Equal segments on a sunken track, the picked one lit, over <see cref="RadioGroupProps.Rows"/>
    /// rows.</summary>
    Segmented,

    /// <summary>Each option's content in a frame, dimmed until it is the one picked, stacked 8 apart.</summary>
    Tile,

    /// <summary>Each option on a card, in a grid of <see cref="RadioGroupProps.Columns"/> columns.</summary>
    Card,
}

public readonly record struct RadioGroupProps
{
    /// <summary>The picked <see cref="Radio"/>'s <see cref="RadioProps.Value"/>.</summary>
    public UiKey Value { get; init; }

    public Callback<UiKey> OnValueChange { get; init; }

    public RadioGroupVariant Variant { get; init; }

    /// <summary>The way the options run; <see cref="Tralys.Ui.Direction.Column"/> by default.</summary>
    public Direction? Direction { get; init; }

    /// <summary>How many rows a side-by-side <see cref="RadioGroupVariant.Segmented"/> group shares its options
    /// across; one by default.</summary>
    public int Rows { get; init; }

    /// <summary>How many columns a <see cref="RadioGroupVariant.Card"/> group lays its options in; zero lays them in
    /// one row.</summary>
    public int Columns { get; init; }

    public ComponentSize? Size { get; init; }

    public bool Disabled { get; init; }
}

internal readonly record struct RadioGroupContext(
    UiKey Value,
    Callback<UiKey> Select,
    RadioGroupVariant Variant,
    ComponentSize Size,
    RadioGroup.Members? Members
);

/// <summary>A controlled single choice among the <see cref="Radio"/>s written inside it: it shows the one whose value
/// is <see cref="RadioGroupProps.Value"/> picked, and reports a press, Space or Enter on another through
/// <see cref="RadioGroupProps.OnValueChange"/>. Tab stops in the group once, at the picked radio; the arrow keys move
/// the focus to the radio beside the focused one in their direction, wrapping at the edges, and pick it, as Home and
/// End do the first and last radio that can take focus.</summary>
[Children]
public sealed class RadioGroup : Component<RadioGroupProps>
{
    internal static readonly ContextKey<RadioGroupContext> Context = new(
        "RadioGroup",
        new RadioGroupContext(default, default, RadioGroupVariant.Default, ComponentSize.Md, null)
    );

    protected override void Render(Ui ui, in RadioGroupProps p)
    {
        var t = ui.Theme;
        var members = ui.Remember(static () => new Members());
        members.Count = ui.State(0);
        members.Root = ui.Ref();
        var count = Math.Max(1, members.Count.Value);
        ui.Provide(
            Context,
            new RadioGroupContext(
                p.Value,
                p.OnValueChange,
                p.Variant,
                p.Size ?? ComponentSize.Md,
                members
            )
        );
        var row = p.Direction == Tralys.Ui.Direction.Row;
        switch (p.Variant)
        {
            case RadioGroupVariant.Segmented:
                var columns = row ? Rounded(count, Math.Max(1, p.Rows)) : 1;
                using (
                    ui.Box(
                        padding: 1f,
                        background: t.Color(ColorToken.BgSunken),
                        radius: t.Radius(RadiusToken.Md),
                        tabNavigation: Navigation.Once,
                        arrowNavigation: Navigation.Cycle,
                        onKeyDown: members.Edges,
                        nodeRef: members.Root
                    )
                )
                using (
                    ui.Grid(
                        columns: GridTracks.Repeat(columns, Track.Fr(1f)),
                        rows: GridTracks.Repeat(Rounded(count, columns), Track.Fr(1f)),
                        grow: 1f
                    )
                )
                    ui.Children();
                break;
            case RadioGroupVariant.Card:
                var across = p.Columns > 0 ? p.Columns : count;
                using (
                    ui.Grid(
                        columns: GridTracks.Repeat(across, Track.Fr(1f)),
                        rows: GridTracks.Repeat(Rounded(count, across), Track.Fr(1f)),
                        gap: 2f * t.Spacing,
                        tabNavigation: Navigation.Once,
                        arrowNavigation: Navigation.Cycle,
                        onKeyDown: members.Edges,
                        nodeRef: members.Root
                    )
                )
                    ui.Children();
                break;
            default:
                using (
                    ui.Box(
                        direction: row ? Tralys.Ui.Direction.Row : Tralys.Ui.Direction.Column,
                        align: Align.Start,
                        gap: p.Variant == RadioGroupVariant.Tile ? 8f : 16f,
                        tabNavigation: Navigation.Once,
                        arrowNavigation: Navigation.Cycle,
                        onKeyDown: members.Edges,
                        nodeRef: members.Root
                    )
                )
                    ui.Children();
                break;
        }
    }

    static int Rounded(int count, int per) => (count + per - 1) / per;

    [FramePath]
    internal sealed class Members
    {
        internal readonly Handler<KeyEvent> Edges;
        internal State<int> Count = null!;
        internal NodeRef Root = null!;

        internal Members() =>
            Edges = Handler<KeyEvent>.Of(this, static (m, ref e) => m.Edge(ref e));

        internal void Join() => Count.Value = Count.Peek + 1;

        internal void Leave() => Count.Value = Count.Peek - 1;

        void Edge(ref KeyEvent e)
        {
            if (
                e.Key is not (Key.Home or Key.End)
                || (e.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Gui)) != 0
                || Root.Node is not { View: { } view } group
            )
                return;
            var stop = e.Key == Key.Home ? view.FirstStop(group) : view.LastStop(group);
            if (stop is not null && view.Focus(stop, FocusCause.Arrow, visible: true))
                e.PreventDefault();
        }
    }
}
