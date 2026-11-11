using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct PopoverProps
{
    /// <summary>Whether the popup shows; null leaves it to the popover, starting at <see cref="DefaultOpen"/>.</summary>
    public bool? Open { get; init; }

    public bool DefaultOpen { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    /// <summary>What the popup holds.</summary>
    public Slot Content { get; init; }

    /// <summary>Where the popup opens against what is written inside the popover;
    /// <see cref="PlacementMode.BottomStart"/> by default.</summary>
    public PlacementMode? Placement { get; init; }

    /// <summary>The popup frame's rung; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }
}

/// <summary>What a <see cref="Popover"/> tells what is written inside it and inside its popup: whether the popup is
/// open, and how to toggle or close it.</summary>
public readonly record struct PopoverContext(bool Open, Callback Toggle, Callback Close);

public readonly record struct PopoverTriggerProps
{
    public bool Disabled { get; init; }
}

public readonly record struct PopoverCloseProps
{
    /// <summary>What a click runs before the popover closes.</summary>
    public Callback OnClick { get; init; }
}

/// <summary>Opens <see cref="PopoverProps.Content"/> in a floating frame against what is written inside it, its
/// trigger: a <see cref="Trigger"/>, or any control that sets <see cref="PopoverProps.Open"/> itself.
/// <see cref="PopoverProps.Open"/> reports through <see cref="PopoverProps.OnOpenChange"/> and is the popover's own when
/// null. A press outside the popup, the trigger's included, and Escape close it, and a <see cref="Close"/> inside it
/// closes it on a click; focus inside goes back to what had it as it opened.</summary>
[Children]
public sealed class Popover : Component<PopoverProps>
{
    public static readonly ContextKey<PopoverContext> Context = new("Popover");

    protected override void Render(Ui ui, in PopoverProps p)
    {
        var disclosure = ui.Remember(static () => new Disclosure());
        disclosure.Open = p.Open;
        disclosure.OnOpenChange = p.OnOpenChange;
        disclosure.Own = ui.State(p.DefaultOpen);
        var open = p.Open ?? disclosure.Own.Value;
        var anchor = ui.Ref();
        ui.Provide(Context, new PopoverContext(open, disclosure.Toggle, disclosure.Close));
        using (ui.Box(nodeRef: anchor))
            ui.Children();
        if (!open)
            return;
        using (
            ui.Positioner(
                anchor: anchor,
                placement: p.Placement,
                restoreFocus: true,
                dismissOn: Dismiss.Escape | Dismiss.OutsidePress,
                onDismiss: disclosure.Close
            )
        )
        using (ui.OverlayFrame(size: p.Size, inset: OverlayInset.Block))
            p.Content.Render(ui);
    }

    /// <summary>The plain, focusable trigger that toggles its popover: a click, Space or Enter on it reports the
    /// opposite of the popover's open state, and what is written inside it is its look.</summary>
    [Children]
    public sealed class Trigger : Component<PopoverTriggerProps>
    {
        protected override void Render(Ui ui, in PopoverTriggerProps p)
        {
            var popover = ui.Use(Context);
            var radius = ui.Theme.Radius(RadiusToken.Md);
            using (
                ui.Box(
                    radius: radius,
                    color: ui.Theme.Color(ColorToken.Fg),
                    cursor: Cursors.Hand,
                    onClick: popover.Toggle
                )
            )
            {
                using (ui.Box())
                    ui.Children();
                if (ui.IsFocusVisible)
                    ui.FocusRing(radius: radius);
            }
        }
    }

    /// <summary>A pressable inside a popover's popup whose click runs <see cref="PopoverCloseProps.OnClick"/>, then
    /// closes the popover; what is written inside it is its look.</summary>
    [Children]
    public sealed class Close : Component<PopoverCloseProps>
    {
        protected override void Render(Ui ui, in PopoverCloseProps p)
        {
            var radius = ui.Theme.Radius(RadiusToken.Md);
            using (
                ui.Box(
                    direction: Direction.Column,
                    radius: radius,
                    onClick: Callback.Of(ui.Self(p), static h => Run(h))
                )
            )
            {
                using (ui.Box(direction: Direction.Column))
                    ui.Children();
                if (ui.IsFocusVisible)
                    ui.FocusRing(radius: radius);
            }
        }

        static void Run(Host<PopoverCloseProps> h)
        {
            h.Props.OnClick.Invoke();
            h.Use(Context).Close.Invoke();
        }
    }
}
