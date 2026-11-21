using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct FilterButtonProps
{
    /// <summary>Whether the filters show; null leaves it to the button, starting at <see cref="DefaultOpen"/>.</summary>
    public bool? Open { get; init; }

    public bool DefaultOpen { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    /// <summary>The filters the popup holds.</summary>
    public Slot Content { get; init; }

    /// <summary>Whether a filter is applied, which a dot over the icon shows.</summary>
    public bool Active { get; init; }

    /// <summary>The button's tooltip; "Filter" when null.</summary>
    public TextValue Tooltip { get; init; }

    /// <summary>The button's and the popup frame's rung; <see cref="ComponentSize.Sm"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>An outlined filter icon button that opens <see cref="FilterButtonProps.Content"/> in a popover below it, its
/// end edge on the button's, with a dot in the accent over the icon's corner while
/// <see cref="FilterButtonProps.Active"/>. <see cref="FilterButtonProps.Open"/> reports through
/// <see cref="FilterButtonProps.OnOpenChange"/> and is the button's own when null; a click toggles it, and Escape or
/// a press outside closes it.</summary>
public sealed class FilterButton : Component<FilterButtonProps>
{
    const float Dot = 6f;

    protected override void Render(Ui ui, in FilterButtonProps p)
    {
        var t = ui.Theme;
        var disclosure = ui.Remember(ui.State(p.DefaultOpen), static own => new Disclosure(own));
        disclosure.Open = p.Open;
        disclosure.OnOpenChange = p.OnOpenChange;
        ui.Effect(disclosure, ui.IsDisabled, Disclosure.ShutWhenDisabled);
        var size = p.Size ?? ComponentSize.Sm;
        using (
            ui.Popover(
                open: (p.Open ?? disclosure.Own.Value) && !ui.IsDisabled,
                onOpenChange: disclosure.SetOpen,
                content: p.Content,
                placement: PlacementMode.BottomEnd,
                size: size
            )
        )
        {
            using (ui.Tooltip(text: p.Tooltip.IsNull ? "Filter" : p.Tooltip))
                ui.IconButton(
                    icon: IconGlyph.Of(IconRole.Filter),
                    scheme: ColorScheme.Secondary,
                    variant: ComponentVariant.Outlined,
                    size: size,
                    onClick: disclosure.Toggle
                );
            if (p.Active)
                using (
                    ui.Box(
                        width: Dot,
                        height: Dot,
                        background: t.Color(ColorToken.BgAccent),
                        radius: Dot / 2f,
                        pointerEvents: PointerEvents.None,
                        position: Position.Absolute,
                        top: 2f,
                        right: 2f
                    )
                ) { }
        }
    }
}
