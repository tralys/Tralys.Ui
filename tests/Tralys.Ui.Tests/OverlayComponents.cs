using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public readonly record struct DialogProps
{
    public bool Open { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    public float? Width { get; init; }
}

public readonly record struct DialogContext(Host<DialogProps> Host);

public readonly record struct TextPartProps
{
    public TextValue Text { get; init; }
}

public readonly record struct FooterProps;

/// <summary>The spec's modal dialog: a scrim and a panel in a modal layer, its parts reaching it through context.</summary>
[Children]
public sealed class Dialog : Component<DialogProps>
{
    public static readonly ContextKey<DialogContext> Context = new("Dialog", default);

    protected override void Render(Ui ui, in DialogProps p)
    {
        if (!p.Open)
            return;
        var t = ui.Theme;
        var self = ui.Self(p);
        var panel = ui.Ref();
        ui.Provide(Context, new DialogContext(self));
        using (
            ui.Layer(
                modal: true,
                restoreFocus: true,
                autoFocus: true,
                content: panel,
                onDismiss: Callback.Of(self, static h => h.Props.OnOpenChange.Invoke(false)),
                dismissOn: Dismiss.Escape | Dismiss.OutsidePress,
                align: Align.Center,
                justify: Justify.Center
            )
        )
        {
            using (
                ui.Box(
                    position: Position.Absolute,
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0,
                    background: t.Color(ColorToken.BgScrim)
                )
            ) { }
            using (
                ui.Box(
                    nodeRef: panel,
                    direction: Direction.Column,
                    gap: 12,
                    padding: 24,
                    width: p.Width ?? 420,
                    radius: t.Radius(RadiusToken.Lg),
                    background: t.Color(ColorToken.BgOverlay),
                    border: new(1, t.Color(ColorToken.BorderOverlay)),
                    shadow: new BoxShadow(
                        0,
                        8,
                        24,
                        0,
                        t.Color(ColorToken.BgShadow).WithOpacity(0.4f)
                    ),
                    id: "dialog-panel"
                )
            )
                ui.Children();
        }
    }

    public sealed class Title : Component<TextPartProps>
    {
        protected override void Render(Ui ui, in TextPartProps p) =>
            ui.Text(p.Text, variant: TextVariant.Title);
    }

    [Children]
    public sealed class Footer : Component<FooterProps>
    {
        protected override void Render(Ui ui, in FooterProps p)
        {
            using (ui.Box(justify: Justify.End, gap: 8, padding: new Thickness(0, 8, 0, 0)))
                ui.Children();
        }
    }

    public sealed class Close : Component<TextPartProps>
    {
        protected override void Render(Ui ui, in TextPartProps p)
        {
            using (
                ui.Box(
                    padding: 8,
                    onClick: Callback.Of(
                        ui.Use(Context).Host,
                        static h => h.Props.OnOpenChange.Invoke(false)
                    )
                )
            )
                ui.Text(p.Text.IsNull ? "Close" : p.Text);
        }
    }
}

public readonly record struct PopoverProps
{
    public bool Open { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    public Slot Content { get; init; }

    public PlacementMode? Placement { get; init; }
}

/// <summary>The spec's popover: the caller's children are the trigger, and the content opens in a positioner beside
/// it.</summary>
[Children]
public sealed class Popover : Component<PopoverProps>
{
    protected override void Render(Ui ui, in PopoverProps p)
    {
        var anchor = ui.Ref();
        var t = ui.Theme;
        using (ui.Box(nodeRef: anchor))
            ui.Children();
        if (!p.Open)
            return;
        using (
            ui.Positioner(
                anchor: anchor,
                placement: p.Placement,
                sideOffset: 6,
                restoreFocus: true,
                autoFocus: true,
                dismissOn: Dismiss.Escape | Dismiss.OutsidePress,
                onDismiss: Callback.Of(ui.Self(p), static h => h.Props.OnOpenChange.Invoke(false)),
                direction: Direction.Column,
                gap: 8,
                padding: 12,
                maxWidth: 320,
                background: t.Color(ColorToken.BgOverlay),
                border: new(1, t.Color(ColorToken.BorderOverlay)),
                radius: t.Radius(RadiusToken.Md),
                id: "popover"
            )
        )
            p.Content.Render(ui);
    }
}

public readonly record struct HintProps
{
    public bool Open { get; init; }

    public PlacementMode? Placement { get; init; }
}

/// <summary>A tooltip: a passive positioner above its trigger that never dismisses.</summary>
[Children]
public sealed class Hint : Component<HintProps>
{
    protected override void Render(Ui ui, in HintProps p)
    {
        var anchor = ui.Ref();
        using (ui.Box(nodeRef: anchor))
            ui.Children();
        if (!p.Open)
            return;
        using (
            ui.Positioner(
                anchor: anchor,
                placement: p.Placement ?? PlacementMode.Top,
                sideOffset: 4,
                hit: LayerHit.PassThrough,
                tier: LayerTier.Tooltip,
                id: "hint"
            )
        )
        using (ui.Box(width: 40, height: 20)) { }
    }
}

public readonly record struct MenuProps
{
    public bool Open { get; init; }

    public Callback<bool> OnOpenChange { get; init; }
}

/// <summary>A menu: a dismissable positioner below its trigger.</summary>
[Children]
public sealed class Menu : Component<MenuProps>
{
    protected override void Render(Ui ui, in MenuProps p)
    {
        var anchor = ui.Ref();
        using (ui.Box(nodeRef: anchor))
            ui.Children();
        if (!p.Open)
            return;
        using (
            ui.Positioner(
                anchor: anchor,
                dismissOn: Dismiss.Escape | Dismiss.OutsidePress,
                onDismiss: Callback.Of(ui.Self(p), static h => h.Props.OnOpenChange.Invoke(false)),
                id: "menu"
            )
        )
        using (ui.Box(width: 60, height: 90)) { }
    }
}
