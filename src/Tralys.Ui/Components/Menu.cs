using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct MenuProps
{
    /// <summary>Whether the menu shows.</summary>
    public bool Open { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    /// <summary>What the menu opens against: the node it is written in by default, a point inside a node
    /// (<see cref="Anchor.Within"/>), a point in the view, or <see cref="Anchor.Pointer"/>.</summary>
    public Anchor Anchor { get; init; }

    /// <summary>Where the menu opens against its anchor; <see cref="PlacementMode.BottomStart"/> by default.</summary>
    public PlacementMode? Placement { get; init; }

    /// <summary>Sizes every row, its submenus' included; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    /// <summary>Drawn above the rows, which scroll beneath it: whose menu it is, or where it was opened.</summary>
    public Slot Header { get; init; }

    /// <summary>The rows: <c>ui.MenuItem</c>s and <c>ui.MenuSeparator</c>s.</summary>
    public Slot Content { get; init; }

    /// <summary>The tallest its rows stand, and its submenus' rows, before they scroll: 400 by default.</summary>
    public float? MaxHeight { get; init; }
}

public readonly record struct MenuItemProps
{
    public TextValue Text { get; init; }

    /// <summary>What the row shows when it has no <see cref="Text"/>.</summary>
    public Slot Content { get; init; }

    /// <summary>Drawn before the text, in the icon column every row of its menu keeps once one row has an icon or a
    /// check.</summary>
    public IconGlyph Icon { get; init; }

    /// <summary>The shortcut drawn muted at the row's end, in place of its edit command's; it binds no key.</summary>
    public TextValue Shortcut { get; init; }

    /// <summary>Whether a click reports the opposite of <see cref="Checked"/> through
    /// <see cref="OnCheckedChange"/>.</summary>
    public bool Checkable { get; init; }

    /// <summary>Whether the row shows its check, in its menu's selected-row look.</summary>
    public bool Checked { get; init; }

    public Callback<bool> OnCheckedChange { get; init; }

    /// <summary>Keeps the menu open when the row is clicked.</summary>
    public bool StaysOpen { get; init; }

    /// <summary>Marks a row that removes or ends something, drawn in the danger colour.</summary>
    public bool Destructive { get; init; }

    public bool Disabled { get; init; }

    /// <summary>Run on a click; the row is disabled while one made from a command can't run.</summary>
    public Callback OnClick { get; init; }

    /// <summary>The rows of the submenu the row opens beside it, or on a menu bar the dropdown it opens below it.</summary>
    public Slot Items { get; init; }
}

public readonly record struct MenuSeparatorProps;

internal readonly record struct MenuContext(
    MenuLevel? Level,
    ComponentSize Size,
    bool ReservesIcon,
    bool InBar,
    float MaxHeight
);

/// <summary>A popup menu of <see cref="Item"/> rows, shown while <see cref="MenuProps.Open"/> against
/// <see cref="MenuProps.Anchor"/>. It takes the focus as it opens, itself and no row; a press outside it or Escape
/// reports <see cref="MenuProps.OnOpenChange"/> false, and so does a click on a row that doesn't stay open, the focus
/// going back to where it was.</summary>
/// <remarks>Up and Down move the focus between the rows that can take it, wrapping round, and Home and End to the ends;
/// Right, Enter or Space on a row with items opens its submenu on its first row, and Left or Escape shuts the submenu
/// the focus is in. Enter or Space clicks the focused row. Resting the pointer on a row focuses it, and after 400 ms
/// opens its submenu, shutting any other its menu has open. Presses on its frame stop at it, and its rows scroll past
/// <see cref="MenuProps.MaxHeight"/>, under a bar down their right edge.</remarks>
public sealed class Menu : Component<MenuProps>
{
    internal static readonly ContextKey<MenuContext> Context = new(
        "Menu",
        new MenuContext(null, ComponentSize.Md, false, false, MenuLook.MaxRowsHeight)
    );

    protected override void Render(Ui ui, in MenuProps p)
    {
        if (!p.Open)
            return;
        var self = ui.Self(p);
        using (
            ui.Positioner(
                anchor: p.Anchor,
                placement: p.Placement,
                sideOffset: MenuLook.Gap,
                restoreFocus: true,
                autoFocus: true,
                dismissOn: Dismiss.Escape | Dismiss.OutsidePress,
                onDismiss: Callback.Of(self, static h => h.Props.OnOpenChange.Invoke(false)),
                direction: Direction.Column
            )
        )
            ui.MenuPanel(
                onClose: Callback.Of(self, static h => h.Props.OnOpenChange.Invoke(false)),
                size: p.Size ?? ComponentSize.Md,
                caption: p.Header,
                content: p.Content,
                maxHeight: p.MaxHeight ?? MenuLook.MaxRowsHeight
            );
    }

    /// <summary>A row of a menu: an icon or check, its text, its shortcut and, with <see cref="MenuItemProps.Items"/>,
    /// an arrow to the submenu it opens beside it. A click runs a row with no items, flipping its check while
    /// checkable, then its <see cref="MenuItemProps.OnClick"/>, and closes its menu unless it stays open; a click on a
    /// row with items opens its submenu. On a <see cref="Menubar"/> it is a header, whose items open in a dropdown
    /// below it.</summary>
    public sealed class Item : Component<MenuItemProps>
    {
        protected override void Render(Ui ui, in MenuItemProps p)
        {
            var c = ui.Use(Context);
            var t = ui.Theme;
            var m = t.Metrics(c.Size);
            var entry = ui.Remember(c.Level, static level => MenuEntry.Rent(level));
            entry.Host = ui.Self(p);
            entry.Ref = ui.Ref();
            entry.Update(
                hasItems: !p.Items.IsEmpty,
                wantsIcon: !p.Icon.IsEmpty || p.Checkable || p.Checked
            );
            ui.CanRun(p.OnClick);
            var disabled = ui.IsDisabled;
            var open = entry.IsOpen;
            if (c.InBar)
            {
                Header(ui, entry, p, m, open, disabled);
                return;
            }

            var lit = !disabled && (ui.IsFocused || open);
            var radius = t.Radius(RadiusToken.Md);
            var color =
                disabled ? t.Color(ColorToken.FgDisabled)
                : p.Checked ? t.Color(ColorToken.FgAccent)
                : p.Destructive ? t.Color(ColorToken.FgDanger)
                : t.Color(ColorToken.Fg);
            var background =
                p.Checked ? t.Color(ComponentColor.RowBgSelected)
                : lit ? t.Color(ColorToken.StateHover)
                : default(Paint);
            var shortcut = p.Shortcut.IsNull ? MenuLook.Shortcut(ui, p.OnClick) : p.Shortcut;
            using (
                ui.Box(
                    align: Align.Center,
                    minHeight: m.Height,
                    padding: m.PaddingX,
                    radius: radius,
                    background: background,
                    color: color,
                    fontSize: m.FontSize,
                    fontWeight: t.Weight(WeightToken.Interface),
                    cursor: Cursors.Hand,
                    onClick: entry.Click,
                    pressMode: PressMode.ReleaseOver,
                    onPointerMove: entry.PointerMove,
                    onHoverChange: entry.HoverChange,
                    nodeRef: entry.Ref
                )
            )
            {
                if (c.ReservesIcon || !p.Icon.IsEmpty || p.Checked)
                    using (
                        ui.Box(
                            align: Align.Center,
                            justify: Justify.Center,
                            width: m.IconSize,
                            margin: new Thickness(0f, 0f, 8f, 0f),
                            shrink: 0f
                        )
                    )
                    {
                        if (p.Checked)
                            ui.Icon(glyph: IconGlyph.Of(IconRole.Check), size: m.IconSize);
                        else if (!p.Icon.IsEmpty)
                            ui.Icon(glyph: p.Icon);
                    }
                using (ui.Box(align: Align.Center, grow: 1))
                {
                    if (!p.Text.IsNull)
                        ui.Text(p.Text);
                    else
                        p.Content.Render(ui);
                }
                if (!shortcut.IsNull)
                    ui.Text(
                        shortcut,
                        color: t.Color(disabled ? ColorToken.FgDisabled : ColorToken.FgMuted),
                        margin: new Thickness(16f, 0f, 0f, 0f)
                    );
                if (entry.HasItems)
                    ui.Icon(
                        glyph: IconGlyph.Of(IconRole.ChevronRight),
                        size: MathF.Max(0f, m.IconSize - 4f),
                        color: t.Color(disabled ? ColorToken.IconDisabled : ColorToken.Icon),
                        margin: new Thickness(8f, 0f, 0f, 0f)
                    );
            }
            if (open && entry.HasItems && !disabled)
                using (
                    ui.Positioner(
                        anchor: entry.Ref,
                        placement: PlacementMode.RightStart,
                        restoreFocus: true,
                        dismissOn: Dismiss.Escape,
                        onDismiss: entry.CloseSubmenu,
                        direction: Direction.Column
                    )
                )
                    ui.MenuPanel(
                        parent: c.Level,
                        header: entry,
                        onClose: c.Level?.CloseAll ?? default,
                        size: c.Size,
                        content: p.Items,
                        focusFirst: entry.FocusFirstOnOpen,
                        maxHeight: c.MaxHeight
                    );
        }

        [FramePath]
        static void Header(
            Ui ui,
            MenuEntry entry,
            in MenuItemProps p,
            ComponentMetrics m,
            bool open,
            bool disabled,
            [System.Runtime.CompilerServices.CallerFilePath] string file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
        )
        {
            var t = ui.Theme;
            var lit = !disabled && (ui.IsFocused || ui.IsHovered);
            using (
                ui.Box(
                    align: Align.Center,
                    minHeight: m.Height,
                    padding: m.Padding,
                    radius: t.Radius(RadiusToken.Md),
                    background: open ? t.Color(ColorToken.StatePressed)
                        : lit ? t.Color(ColorToken.StateHover)
                        : default(Paint),
                    color: t.Color(disabled ? ColorToken.FgDisabled : ColorToken.Fg),
                    fontSize: m.FontSize,
                    fontWeight: t.Weight(WeightToken.Interface),
                    cursor: Cursors.Hand,
                    onClick: entry.HasItems ? default : entry.Click,
                    onPointerDown: entry.HasItems ? entry.HeaderPress : default,
                    onPointerMove: entry.PointerMove,
                    onHoverChange: entry.HoverChange,
                    focusable: true,
                    nodeRef: entry.Ref,
                    file: file,
                    line: line
                )
            )
            {
                if (!p.Text.IsNull)
                    ui.Text(p.Text);
                else
                    p.Content.Render(ui);
            }
        }
    }

    /// <summary>A 1-unit rule between groups of a menu's rows, with 6 clear above and below.</summary>
    public sealed class Separator : Component<MenuSeparatorProps>
    {
        protected override void Render(Ui ui, in MenuSeparatorProps p)
        {
            using (ui.Box(direction: Direction.Column, padding: new Thickness(0f, 6f, 0f, 6f)))
            using (ui.Box(height: 1f, background: ui.Theme.Color(ColorToken.Border))) { }
        }
    }
}

public readonly record struct MenubarProps
{
    /// <summary>Sizes every header and dropdown row; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A menu bar: its <c>ui.MenuItem</c> headers lie in a row, and each with items opens them in a dropdown below
/// it. A press on a header opens or shuts its dropdown, which takes the focus, and while one is open the pointer moving
/// onto another header switches to it at once. Left and Right move along the bar, wrapping, and from inside a dropdown
/// switch to the next one; Down, Enter or Space opens the focused header's dropdown on its first row, and Escape shuts
/// it, the focus going to its header. A click on a row with no items shuts the dropdown, and the focus goes back to
/// where it was before the dropdown opened, which its rows' edit commands act on.</summary>
[Children]
public sealed class Menubar : Component<MenubarProps>
{
    protected override void Render(Ui ui, in MenubarProps p)
    {
        var size = p.Size ?? ComponentSize.Md;
        var bar = ui.Remember(static () => MenuLevel.Rent());
        bar.Bind(null, null, default, isBar: true);
        bar.OpenRow = ui.RefState<MenuEntry>();
        var closed = ui.State(0);
        bar.Closed = closed;
        bar.Panel = ui.Ref();
        ui.Effect(bar, closed.Value, static (b, _, _) => b.DropdownClosed());
        ui.Effect(
            bar,
            ui.IsDisabled,
            static (b, disabled, _) =>
            {
                if (disabled)
                    b.CloseAll.Invoke();
            }
        );
        ui.Provide(
            Menu.Context,
            new MenuContext(bar, size, false, InBar: true, MenuLook.MaxRowsHeight)
        );
        using (ui.Box(onKeyDown: bar.KeyDown, nodeRef: bar.Panel))
            ui.Children();
        if (bar.OpenRow.Value is not { HasItems: true } open || ui.IsDisabled)
            return;
        using (
            ui.Positioner(
                anchor: open.Ref,
                restoreFocus: true,
                dismissOn: Dismiss.Escape | Dismiss.OutsidePress,
                dismissExempt: bar.Panel,
                onDismiss: bar.CloseAll,
                direction: Direction.Column
            )
        )
            ui.MenuPanel(
                parent: bar,
                header: open,
                onClose: bar.CloseAll,
                size: size,
                content: open.Host.Props.Items,
                focusFirst: open.FocusFirstOnOpen,
                takesFocus: true,
                maxHeight: MenuLook.MaxRowsHeight,
                key: UiKey.Of(open)
            );
    }
}

public readonly record struct ContextMenuProps
{
    /// <summary>The rows the menu shows; with none, a context request goes on to the context menu around this
    /// one.</summary>
    public Slot Menu { get; init; }

    /// <summary>Whether the menu shows; null leaves it to the context menu, starting at
    /// <see cref="DefaultOpen"/>.</summary>
    public bool? Open { get; init; }

    public bool DefaultOpen { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    /// <summary>Sizes every row; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    /// <summary>Drawn above the rows.</summary>
    public Slot Header { get; init; }
}

/// <summary>The area written inside it opens <see cref="ContextMenuProps.Menu"/> at the pointer on a right-click, or a
/// touch or pen held still and lifted, reporting <see cref="ContextMenuProps.OnOpenChange"/>; the menu is the context
/// menu's own while <see cref="ContextMenuProps.Open"/> is null. A held lift that opens it clicks nothing beneath.</summary>
[Children]
public sealed class ContextMenu : Component<ContextMenuProps>
{
    protected override void Render(Ui ui, in ContextMenuProps p)
    {
        var menu = ui.Remember(static () => new Disclosure());
        menu.Open = p.Open;
        menu.OnOpenChange = p.OnOpenChange;
        menu.Own = ui.State(p.DefaultOpen);
        menu.Offered = !p.Menu.IsEmpty;
        var open = (p.Open ?? menu.Own.Value) && !p.Menu.IsEmpty;
        using (ui.Box(direction: Direction.Column, onContextMenu: menu.Requested))
            ui.Children();
        ui.Menu(
            open: open,
            onOpenChange: menu.SetOpen,
            anchor: Anchor.Pointer,
            placement: PlacementMode.Pointer,
            size: p.Size,
            header: p.Header,
            content: p.Menu
        );
    }
}

internal readonly record struct MenuPanelProps
{
    public MenuLevel? Parent { get; init; }

    public MenuEntry? Header { get; init; }

    public Callback OnClose { get; init; }

    public ComponentSize Size { get; init; }

    public Slot Caption { get; init; }

    public Slot Content { get; init; }

    public bool FocusFirst { get; init; }

    public bool TakesFocus { get; init; }

    public float MaxHeight { get; init; }
}

internal sealed class MenuPanel : Component<MenuPanelProps>
{
    protected override void Render(Ui ui, in MenuPanelProps p)
    {
        var level = ui.Remember(static () => MenuLevel.Rent());
        level.Bind(p.Parent, p.Header, p.OnClose, isBar: false);
        level.OpenRow = ui.RefState<MenuEntry>();
        var reserve = ui.State(false);
        level.Reserve = reserve;
        level.Panel = ui.Ref();
        level.FocusFirst = p.FocusFirst;
        level.TakesFocus = p.TakesFocus;
        var scroll = ui.ScrollHandle();
        ui.Effect(level, 0, static (l, _, cx) => l.Shown(cx.View));
        ui.Provide(
            Menu.Context,
            new MenuContext(level, p.Size, reserve.Value, InBar: false, p.MaxHeight)
        );
        using (
            ui.Box(
                direction: Direction.Column,
                minWidth: MenuLook.MinWidth,
                focusable: true,
                onKeyDown: level.KeyDown,
                onPointerDown: MenuLook.StopPress,
                nodeRef: level.Panel
            )
        )
        using (ui.OverlayFrame(size: p.Size))
        {
            p.Caption.Render(ui);
            using (ui.Box(direction: Direction.Column, grow: 1, minHeight: 0f))
            {
                using (
                    ui.Box(
                        direction: Direction.Column,
                        grow: 1,
                        padding: new Thickness(
                            0f,
                            0f,
                            ListBarParts.Space(scroll, ScrollbarVisibility.Auto),
                            0f
                        ),
                        overflow: Overflow.ScrollY,
                        scroll: scroll,
                        maxHeight: p.MaxHeight,
                        panning: Panning.VerticalOnly
                    )
                )
                    p.Content.Render(ui);
                ui.ListBar(scroll: scroll, visibility: ScrollbarVisibility.Auto);
            }
        }
    }
}

[FramePath]
internal static class MenuLook
{
    public const float Gap = 4f;
    public const float MinWidth = 160f;
    public const float MaxRowsHeight = 400f;

    public static readonly Handler<PointerEvent> StopPress = Handler<PointerEvent>.Of(
        static (ref PointerEvent e) => e.StopPropagation()
    );

    public static TextValue Shortcut(Ui ui, Callback onClick)
    {
        if (!onClick.IsGated)
            return default;
        for (var edit = EditCommand.Cut; edit <= EditCommand.Delete; edit++)
        {
            if (onClick == ui.Command(edit))
                return KeyLabels.Of(ui, GestureOf(edit));
        }
        return default;
    }

    static Chord GestureOf(EditCommand command) =>
        command switch
        {
            EditCommand.Cut => Chord.Of(Key.X, ModifierKeys.Control),
            EditCommand.Copy => Chord.Of(Key.C, ModifierKeys.Control),
            EditCommand.Paste => Chord.Of(Key.V, ModifierKeys.Control),
            EditCommand.Undo => Chord.Of(Key.Z, ModifierKeys.Control),
            EditCommand.Redo => Chord.Of(Key.Y, ModifierKeys.Control),
            EditCommand.SelectAll => Chord.Of(Key.A, ModifierKeys.Control),
            _ => Chord.Of(Key.Delete, ModifierKeys.None),
        };
}
