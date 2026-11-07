using Tralys.Ui.Media;

namespace Tralys.Ui.Components;

public readonly record struct ImageButtonProps
{
    /// <summary>The art at rest.</summary>
    public ImageSource Source { get; init; }

    /// <summary>The art while <see cref="Active"/>; <see cref="Source"/> when unset.</summary>
    public ImageSource ActiveSource { get; init; }

    /// <summary>The art while pressed; the art it would show otherwise when unset.</summary>
    public ImageSource PressedSource { get; init; }

    public bool Active { get; init; }

    public ObjectFit Fit { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public bool Disabled { get; init; }

    public Callback OnClick { get; init; }
}

/// <summary>A button drawn by its own art: <see cref="ImageButtonProps.Source"/> at rest,
/// <see cref="ImageButtonProps.ActiveSource"/> while active and <see cref="ImageButtonProps.PressedSource"/> while
/// pressed. It takes presses anywhere in its box, so the art's transparent texels, and art still loading, press it
/// too.</summary>
public sealed class ImageButton : Component<ImageButtonProps>
{
    protected override void Render(Ui ui, in ImageButtonProps p)
    {
        ui.CanRun(p.OnClick);
        var shown = p.Source;
        if (p.Active && !p.ActiveSource.IsEmpty)
            shown = p.ActiveSource;
        if (!p.PressedSource.IsEmpty && ui.IsPressed)
            shown = p.PressedSource;
        using (
            ui.Box(
                opacity: ui.IsDisabled ? 0.5f : null,
                cursor: Cursors.Hand,
                onClick: Callback.Of(ui.Self(p), static h => h.Props.OnClick.Invoke())
            )
        )
        {
            ui.Image(
                shown,
                fit: p.Fit,
                width: p.Width,
                height: p.Height,
                pointerEvents: PointerEvents.None
            );
            if (ui.IsFocusVisible)
                ui.FocusRing(radius: default);
        }
    }
}
