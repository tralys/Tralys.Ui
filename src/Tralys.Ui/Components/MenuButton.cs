using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct MenuButtonProps
{
    public TextValue Text { get; init; }

    public Slot Content { get; init; }

    public IconGlyph IconLeft { get; init; }

    /// <summary>Shows a chevron after the content, turned up while the menu is open.</summary>
    public bool Chevron { get; init; }

    public ComponentSize? Size { get; init; }

    /// <summary>The scheme; <see cref="ColorScheme.Primary"/> by default.</summary>
    public ColorScheme? Scheme { get; init; }

    public ComponentVariant Variant { get; init; }

    /// <summary>Whether the menu shows; null leaves it to the button, starting at <see cref="DefaultOpen"/>.</summary>
    public bool? Open { get; init; }

    public bool DefaultOpen { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    /// <summary>The menu's rows: <c>ui.MenuItem</c>s and <c>ui.MenuSeparator</c>s.</summary>
    public Slot Menu { get; init; }

    /// <summary>Sizes the menu's rows; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? MenuSize { get; init; }

    /// <summary>Drawn above the menu's rows.</summary>
    public Slot MenuHeader { get; init; }

    /// <summary>Where the menu opens against the button; <see cref="PlacementMode.BottomStart"/> by
    /// default.</summary>
    public PlacementMode? Placement { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A button that opens a <see cref="Components.Menu"/> of <see cref="MenuButtonProps.Menu"/> against itself, square
/// as an <see cref="IconButton"/> while it shows only <see cref="MenuButtonProps.IconLeft"/>: a
/// click or Down opens it, focusing the menu, and a click on the button again, a press outside, Escape or a click on a
/// row closes it, giving the focus back to the button. <see cref="MenuButtonProps.Open"/> reports through <see cref="MenuButtonProps.OnOpenChange"/> and is the
/// button's own when null. Keys and presses inside the menu never reach the button.</summary>
public sealed class MenuButton : Component<MenuButtonProps>
{
    protected override void Render(Ui ui, in MenuButtonProps p)
    {
        var t = ui.Theme;
        var m = t.Metrics(p.Size ?? ComponentSize.Md);
        var menu = ui.Remember(ui.State(p.DefaultOpen), static own => new Disclosure(own));
        menu.Open = p.Open;
        menu.OnOpenChange = p.OnOpenChange;
        ui.Effect(menu, ui.IsDisabled, Disclosure.ShutWhenDisabled);
        var open = (p.Open ?? menu.Own.Value) && !ui.IsDisabled;
        var anchor = ui.Ref();
        var look = ui.Animated(
            t.Palette(p.Scheme ?? ColorScheme.Primary, p.Variant, ui.ComponentState)
        );
        var radius = t.Radius(RadiusToken.Md);
        var iconOnly = p.Text.IsNull && p.Content.IsEmpty && !p.Chevron && !p.IconLeft.IsEmpty;
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                gap: m.Gap,
                width: iconOnly ? m.Height : Length.Auto,
                minHeight: m.Height,
                padding: iconOnly ? default(Thickness) : ButtonParts.Inside(m.PaddingX, 1f),
                background: look.Background,
                border: new Border(1f, look.Border),
                radius: radius,
                fontSize: m.FontSize,
                fontWeight: t.Weight(WeightToken.Interface),
                opacity: ui.IsDisabled ? 0.5f : null,
                cursor: Cursors.Hand,
                onClick: menu.Toggle,
                onKeyDown: menu.OpenOnDown,
                nodeRef: anchor
            )
        )
        {
            using (ui.Box(align: Align.Center, gap: m.Gap))
            {
                ui.LabelGlyphs(
                    text: p.Text,
                    content: p.Content,
                    iconLeft: p.IconLeft,
                    iconRight: default,
                    iconSize: iconOnly ? m.IconSize : m.FontSize,
                    color: look.Foreground
                );
                if (p.Chevron)
                    ui.Chevron(color: look.Foreground, size: m.FontSize, open: open);
            }
            if (ui.IsFocusVisible)
                ui.FocusRing(radius: radius);
        }
        ui.Menu(
            open: open,
            onOpenChange: menu.SetOpen,
            anchor: anchor,
            placement: p.Placement,
            size: p.MenuSize,
            header: p.MenuHeader,
            content: p.Menu
        );
    }
}

[FramePath]
internal sealed class Disclosure
{
    internal readonly Callback Toggle;
    internal readonly Callback Close;
    internal readonly Callback<bool> SetOpen;
    internal readonly Handler<KeyEvent> OpenOnDown;
    internal readonly Handler<PointerEvent> Requested;
    internal bool? Open;
    internal Callback<bool> OnOpenChange;
    internal readonly State<bool> Own;
    internal bool Offered;

    public Disclosure(State<bool> own)
    {
        Own = own;
        Toggle = Callback.Of(this, static d => d.Set(!d.IsOpen));
        Close = Callback.Of(this, static d => d.Set(false));
        SetOpen = Callback<bool>.Of(this, static (d, open) => d.Set(open));
        OpenOnDown = Handler<KeyEvent>.Of(this, static (d, ref e) => d.Down(ref e));
        Requested = Handler<PointerEvent>.Of(this, static (d, ref e) => d.Request(ref e));
    }

    internal static readonly EffectFn<Disclosure, bool> ShutWhenDisabled = static (
        d,
        disabled,
        _
    ) =>
    {
        if (disabled)
            d.Set(false);
    };

    internal bool IsOpen => Open ?? Own.Peek;

    internal void Set(bool open)
    {
        if (IsOpen == open)
            return;
        if (Open is null)
            Own.Value = open;
        OnOpenChange.Invoke(open);
    }

    void Request(ref PointerEvent e)
    {
        if (!Offered)
            return;
        e.StopPropagation();
        Set(true);
    }

    internal void Down(ref KeyEvent e)
    {
        if (e.Key != Key.Down || e.Modifiers != ModifierKeys.None || IsOpen)
            return;
        Set(true);
        e.PreventDefault();
    }
}
