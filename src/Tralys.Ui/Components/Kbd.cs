using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct KbdProps
{
    /// <summary>What the cap says, such as "Ctrl+S"; <see cref="Chord"/>'s key when unset.</summary>
    public TextValue Text { get; init; }

    /// <summary>The key and modifiers the cap shows, as <c>Ctrl+PgUp</c> or <c>Esc</c>, while it has no
    /// <see cref="Text"/>.</summary>
    public Chord Chord { get; init; }
}

/// <summary>A key or key combination drawn as a keyboard key: one line of caption text in the strong text colour,
/// centred on a sunken cap at least 22 wide whose edge thickens along its bottom. Input passes through it.</summary>
public sealed class Kbd : Component<KbdProps>
{
    protected override void Render(Ui ui, in KbdProps p)
    {
        var t = ui.Theme;
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                minWidth: 22f,
                padding: new Thickness(6f, 1f, 6f, 1f),
                background: t.Color(ColorToken.BgSunken),
                border: new Border(new Thickness(1f, 1f, 1f, 2f), t.Color(ColorToken.Border)),
                radius: t.Radius(RadiusToken.Md),
                pointerEvents: PointerEvents.None
            )
        )
            ui.Text(
                p.Text.IsNull ? KeyLabels.Of(ui, p.Chord) : p.Text,
                color: t.Color(ColorToken.FgStrong),
                fontWeight: t.Weight(WeightToken.Interface),
                variant: TextVariant.Caption
            );
    }
}
