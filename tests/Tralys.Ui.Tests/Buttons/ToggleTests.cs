using Tralys.Ui.Components;
using Tralys.Ui.Components.Tests;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class ToggleTests
{
    static readonly IconGlyph Play = new(0xe13c, "lucide");
    static readonly IconGlyph Pause = new(0xe12e, "lucide");

    [Test]
    public async Task An_active_tooltip_shows_while_on_follows_its_text_at_once_and_shows_nothing_while_off()
    {
        var state = new Tipped { On = true, Tip = "Pause" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, padding: 60))
                ui.Toggle(icon: Play, value: state.On, activeTooltip: state.Tip, id: "toggle");
        });

        driver.Hover(driver.Get("toggle"), settle: false);
        driver.Frame(0.35f);
        var shown = OverlayProbes.ShownTip(driver);
        state.Tip = "Stop";
        driver.Rerender();
        var changed = OverlayProbes.ShownTip(driver);
        state.On = false;
        driver.Rerender();

        await Assert.That((shown, changed)).IsEqualTo(("Pause", "Stop"));
        await Assert.That(OverlayProbes.ShownTip(driver)).IsNull();
    }

    sealed class Tipped
    {
        public bool On;
        public TextValue Tip;
    }

    [Test]
    public async Task A_toggle_reports_the_flipped_value_then_clicks_and_wears_its_active_look_only_once_its_value_is_on()
    {
        var log = new Log();
        var value = false;
        using var driver = UiDriver.Render(ui =>
            ui.Toggle(
                text: "Bold",
                value: value,
                onValueChange: Callback<bool>.Of(log, static (l, on) => l.Add(on ? "on" : "off")),
                onClick: Callback.Of(log, static l => l.Add("click")),
                id: "bold"
            )
        );
        var bold = driver.Get("bold");
        var dark = Themes.Dark;

        driver.Click(bold);
        var off = (log.ToString(), bold.Visual.Background);
        log.Items.Clear();
        value = true;
        driver.Rerender();
        var on = bold.Visual.Background;
        driver.Click(bold);

        await Assert
            .That(off)
            .IsEqualTo(
                (
                    "on click",
                    (Paint)
                        dark.Colors(
                            ColorScheme.Secondary,
                            ComponentVariant.Filled,
                            ComponentState.Hover
                        ).Background
                )
            );
        await Assert
            .That(on)
            .IsEqualTo(
                (Paint)
                    dark.Colors(
                        ColorScheme.Primary,
                        ComponentVariant.Filled,
                        ComponentState.Hover
                    ).Background
            );
        await Assert.That(log.ToString()).IsEqualTo("off click");
    }

    [Test]
    public async Task An_icon_toggle_is_ghost_dims_its_lone_glyph_while_off_and_swaps_in_its_active_icon_while_on()
    {
        var on = false;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 8))
            {
                ui.Toggle(icon: Play, value: on, id: "lone");
                ui.Toggle(icon: Play, activeIcon: Pause, value: on, id: "pair");
                ui.Toggle(
                    icon: Play,
                    activeIcon: Pause,
                    emphasis: ToggleEmphasis.State,
                    value: on,
                    id: "state"
                );
                ui.Toggle(icon: Play, emphasis: ToggleEmphasis.Mode, value: on, id: "mode");
            }
        });
        var dark = Themes.Dark;
        var ghost = dark.Colors(
            ColorScheme.Secondary,
            ComponentVariant.Ghost,
            ComponentState.Normal
        );
        var dim = (Paint)dark.Color(ColorToken.Icon);
        Paint Glyph(string id) => ButtonLooks.TextColor(driver.Get(id));

        var off = (
            driver.Get("lone").Visual.Background,
            Glyph("lone"),
            Glyph("pair"),
            Glyph("state"),
            Glyph("mode")
        );
        on = true;
        driver.Rerender();

        await Assert
            .That(off)
            .IsEqualTo(
                (
                    (Paint)ghost.Background,
                    dim,
                    (Paint)ghost.Foreground,
                    dim,
                    (Paint)ghost.Foreground
                )
            );
        await Assert
            .That((driver.Get("lone").Visual.Background, Glyph("lone")))
            .IsEqualTo(((Paint)ghost.Background, (Paint)ghost.Foreground));
        await Assert
            .That(Nodes.Find(driver.Get("pair"), NodeKind.Text).Text.ToString())
            .IsEqualTo(Pause.Text.ToString());
    }

    [Test]
    public async Task A_toggle_and_an_icon_button_take_their_callers_corners_and_a_toggle_its_glyph_size()
    {
        var glyph = new IconGlyph(0xe176, "lucide");
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(align: Align.Start, gap: 8))
                {
                    ui.Toggle(
                        icon: glyph,
                        iconSize: 28,
                        radius: new CornerRadius(14),
                        size: ComponentSize.Lg,
                        id: "run"
                    );
                    ui.IconButton(icon: glyph, radius: new CornerRadius(9999), id: "round");
                }
            })
            .WithIcons();
        var run = driver.Get("run");

        await Assert
            .That((run.Visual.Radius, driver.Get("round").Visual.Radius))
            .IsEqualTo((new CornerRadius(14), new CornerRadius(9999)));
        await Assert.That(ButtonLooks.Glyph(run).Bounds.Width).IsEqualTo(28f);
    }
}
