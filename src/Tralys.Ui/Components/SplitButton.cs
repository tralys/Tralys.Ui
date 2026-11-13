using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct SplitButtonProps
{
    public TextValue Text { get; init; }

    public Slot Content { get; init; }

    public IconGlyph IconLeft { get; init; }

    public ComponentSize? Size { get; init; }

    /// <summary>The scheme; <see cref="ColorScheme.Primary"/> by default.</summary>
    public ColorScheme? Scheme { get; init; }

    public ComponentVariant Variant { get; init; }

    public bool Disabled { get; init; }

    /// <summary>The primary action, which disables the primary part while one made from a command can't
    /// run.</summary>
    public Callback OnClick { get; init; }

    /// <summary>Whether the menu shows; null leaves it to the button, starting at <see cref="DefaultOpen"/>.</summary>
    public bool? Open { get; init; }

    public bool DefaultOpen { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    /// <summary>The chevron's menu's rows: <c>ui.MenuItem</c>s and <c>ui.MenuSeparator</c>s.</summary>
    public Slot Menu { get; init; }

    /// <summary>Sizes the menu's rows; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? MenuSize { get; init; }

    /// <summary>Where the menu opens against the whole button; <see cref="PlacementMode.BottomStart"/> by
    /// default.</summary>
    public PlacementMode? Placement { get; init; }
}

/// <summary>A button whose primary part runs a primary action and whose chevron opens a menu of the others: a click on
/// the primary part runs <see cref="SplitButtonProps.OnClick"/>, and one on the chevron opens or closes
/// <see cref="SplitButtonProps.Menu"/> against the whole button, running nothing.</summary>
/// <remarks>It is one tab stop with one focus ring: Enter or Space runs the primary action, and Down opens the menu.
/// Both parts wear a <see cref="Button"/>'s look for its scheme, variant and size, rounded only on their outer corners,
/// with a divider between them in the primary part's text colour, and never the skin's art.</remarks>
public sealed class SplitButton : Component<SplitButtonProps>
{
    protected override void Render(Ui ui, in SplitButtonProps p)
    {
        var t = ui.Theme;
        var size = p.Size ?? ComponentSize.Md;
        var m = t.Metrics(size);
        var scheme = p.Scheme ?? ColorScheme.Primary;
        var menu = ui.Remember(static () => new Disclosure());
        menu.Open = p.Open;
        menu.OnOpenChange = p.OnOpenChange;
        menu.Own = ui.State(p.DefaultOpen);
        var split = ui.Remember(static () => new Split());
        split.Host = ui.Self(p);
        split.Menu = menu;
        split.ChevronWidth = m.Height;
        var disabled = ui.IsDisabled;
        ui.Effect(menu, disabled, Disclosure.ShutWhenDisabled);
        var open = (p.Open ?? menu.Own.Value) && !disabled;
        var anchor = ui.Ref();
        var r = t.Radius(RadiusToken.Md);
        using (
            ui.Box(
                align: Align.Stretch,
                radius: r,
                opacity: disabled ? 0.5f : null,
                cursor: Cursors.Hand,
                onClick: split.Click,
                onPointerDown: split.Aim,
                onKeyDown: split.KeyDown,
                nodeRef: anchor
            )
        )
        {
            ui.SplitButtonPart(
                scheme: scheme,
                variant: p.Variant,
                size: size,
                radius: new CornerRadius(r.TopLeft, 0f, 0f, r.BottomLeft),
                edges: new Thickness(1f, 1f, 0f, 1f),
                text: p.Text,
                content: p.Content,
                iconLeft: p.IconLeft,
                primary: p.OnClick,
                dims: !disabled
            );
            using (
                ui.Box(
                    width: 1f,
                    background: t.Palette(scheme, p.Variant, ComponentState.Normal)
                        .Foreground.WithOpacity(0.25f)
                )
            ) { }
            ui.SplitButtonPart(
                scheme: scheme,
                variant: p.Variant,
                size: size,
                radius: new CornerRadius(0f, r.TopRight, r.BottomRight, 0f),
                edges: new Thickness(0f, 1f, 1f, 1f),
                chevron: true,
                open: open
            );
            if (ui.IsFocusVisible)
                ui.FocusRing(radius: r);
        }
        ui.Menu(
            open: open,
            onOpenChange: menu.SetOpen,
            anchor: anchor,
            placement: p.Placement,
            size: p.MenuSize,
            content: p.Menu
        );
    }

    [FramePath]
    sealed class Split
    {
        internal readonly Callback Click;
        internal readonly Handler<PointerEvent> Aim;
        internal readonly Handler<KeyEvent> KeyDown;
        internal Host<SplitButtonProps> Host;
        internal Disclosure Menu = null!;
        internal float ChevronWidth;
        bool _chevron;

        public Split()
        {
            Click = Callback.Of(this, static s => s.Clicked());
            Aim = Handler<PointerEvent>.Of(
                this,
                static (s, ref e) => s._chevron = e.Position.X >= e.Size.Width - s.ChevronWidth
            );
            KeyDown = Handler<KeyEvent>.Of(this, static (s, ref e) => s.Keyed(ref e));
        }

        void Clicked()
        {
            if (_chevron)
            {
                _chevron = false;
                Menu.Set(!Menu.IsOpen);
                return;
            }
            Host.Props.OnClick.Invoke();
        }

        void Keyed(ref KeyEvent e)
        {
            // A chevron press that never clicked mustn't turn this key's click into a toggle.
            if (e.Key is Key.Enter or Key.Space)
                _chevron = false;
            Menu.Down(ref e);
        }
    }
}

internal readonly record struct SplitButtonPartProps
{
    public ColorScheme Scheme { get; init; }

    public ComponentVariant Variant { get; init; }

    public ComponentSize Size { get; init; }

    public CornerRadius Radius { get; init; }

    public Thickness Edges { get; init; }

    public TextValue Text { get; init; }

    public Slot Content { get; init; }

    public IconGlyph IconLeft { get; init; }

    public Callback Primary { get; init; }

    public bool Chevron { get; init; }

    public bool Open { get; init; }

    public bool Dims { get; init; }
}

internal sealed class SplitButtonPart : Component<SplitButtonPartProps>
{
    protected override void Render(Ui ui, in SplitButtonPartProps p)
    {
        var t = ui.Theme;
        var m = t.Metrics(p.Size);
        ui.CanRun(p.Primary);
        var look = ui.Animated(t.Palette(p.Scheme, p.Variant, ui.ComponentState));
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                gap: m.Gap,
                width: p.Chevron ? m.Height : Length.Auto,
                minHeight: m.Height,
                padding: p.Chevron ? default(Thickness) : ButtonParts.Inside(m.PaddingX, p.Edges),
                background: look.Background,
                border: new Border(p.Edges, look.Border),
                radius: p.Radius,
                fontSize: m.FontSize,
                fontWeight: t.Weight(WeightToken.Interface),
                opacity: p.Dims && ui.IsDisabled ? 0.5f : null
            )
        )
        {
            if (p.Chevron)
                ui.Chevron(color: look.Foreground, size: m.IconSize, open: p.Open);
            else
                ui.Label(
                    text: p.Text,
                    content: p.Content,
                    iconLeft: p.IconLeft,
                    iconRight: default,
                    iconSize: m.FontSize,
                    gap: m.Gap,
                    color: look.Foreground
                );
        }
    }
}
