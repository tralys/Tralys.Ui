using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;
using static Tralys.Ui.Tests.ToggleLooks;

namespace Tralys.Ui.Tests;

public sealed class SwitchTests
{
    [Test]
    public async Task A_controlled_switch_reports_once_and_stays_unchecked()
    {
        var reports = new List<bool>();
        await UiDriver.Run(
            ui =>
                ui.Switch(
                    value: false,
                    onValueChange: Callback<bool>.Of(reports, static (r, v) => r.Add(v))
                ),
            static async driver =>
            {
                var before = driver.Draw();
                driver.Click(driver.Get<Switch>());
                driver.Unhover();
                await Assert.That(driver.Draw()).IsEqualTo(before);
            }
        );
        await Assert.That(reports).IsEquivalentTo([true]);
    }

    [Test]
    public async Task A_press_on_a_switchs_content_flips_it()
    {
        var reports = new List<bool>();
        using var driver = UiDriver.Render(ui =>
            ui.Switch(
                value: false,
                onValueChange: Callback<bool>.Of(reports, static (r, v) => r.Add(v)),
                content: Slot.Of(static ui => ui.Text("Wireframe"))
            )
        );

        driver.Click(driver.GetText("Wireframe"));

        await Assert.That(reports).IsEquivalentTo([true]);
    }

    [Test]
    public async Task A_disabled_switch_ignores_a_press_and_dims()
    {
        var reports = new List<bool>();
        var disabled = true;
        using var driver = UiDriver.Render(ui =>
            ui.Switch(
                disabled: disabled,
                onValueChange: Callback<bool>.Of(reports, static (r, v) => r.Add(v))
            )
        );
        var dimmed = driver.Draw();

        driver.Click(driver.Get<Switch>());
        disabled = false;
        driver.Rerender();

        await Assert.That(reports).IsEmpty();
        await Assert.That(driver.Draw()).IsNotEqualTo(dimmed);
    }

    [Test]
    public async Task A_knob_lights_under_the_pointer_takes_the_pressed_colour_while_held_and_the_on_accent_while_on()
    {
        var on = false;
        using var driver = UiDriver.Render(ui => ui.Switch(value: on, label: "Wi-Fi", id: "wifi"));
        var wifi = driver.Get("wifi");

        var rest = Knob(wifi).Visual.Background;
        driver.Hover(wifi);
        var lit = Knob(wifi).Visual.Background;
        var held = driver.Hold(wifi);
        var pressed = Knob(wifi).Visual.Background;
        held.Release();
        on = true;
        driver.Rerender();

        await Assert
            .That((rest, lit, pressed))
            .IsEqualTo(
                (Dark(ColorToken.Icon), Dark(ColorToken.IconHover), Dark(ColorToken.IconPressed))
            );
        await Assert
            .That((Knob(wifi).Visual.Background, Edged(wifi).Visual.Background))
            .IsEqualTo((Dark(ColorToken.IconOnAccent), Dark(ColorToken.BgSelected)));
    }

    [Test]
    public async Task A_switch_shown_again_rests_where_its_value_puts_the_knob_and_one_flipped_while_shown_slides_it_over_a_fifth_slowing_down()
    {
        var choices = new Choices { Open = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(collapsed: !choices.Open))
                ui.Switch(value: choices.On, id: "switch");
        });
        float Knob() => ToggleLooks.Knob(driver.Get("switch")).Bounds.X;
        var off = Knob();

        choices.Open = false;
        driver.Settle();
        choices.On = true;
        driver.Frame(1f);
        choices.Open = true;
        driver.Frame(0f);
        var shown = Knob();
        choices.On = false;
        driver.Frame(0f);
        driver.Frame(0.05f);
        var early = Knob();
        driver.Frame(0.05f);
        var halfway = Knob();
        driver.Frame(0.11f);

        await Assert.That(shown - off).IsEqualTo(22f);
        await Assert.That(early).IsGreaterThan(off).And.IsLessThan(shown);
        await Assert.That(halfway).IsLessThan(off + 11f);
        await Assert.That(Knob()).IsEqualTo(off);
    }

    [Test]
    public async Task A_switch_on_the_right_puts_its_track_after_its_label_at_the_far_end()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, width: 200))
                ui.Switch(label: "Sound", side: SwitchSide.Right, id: "sound");
        });
        var sound = driver.Get("sound");

        var label = driver.GetText("Sound").ViewBounds;
        var track = Edged(sound).ViewBounds;

        await Assert.That(track.X).IsGreaterThanOrEqualTo(label.Right + 6f);
        await Assert.That(track.Right).IsEqualTo(200f);
    }

    [Test]
    public async Task A_skins_off_track_and_thumb_replace_the_switch_track_and_knob_and_its_on_track_shows_while_on()
    {
        var on = false;
        using var driver = UiDriver.Render(ui => ui.Switch(value: on, id: "switch"));
        driver.View.Context.Skin = new Skin([
            new(SkinPart.ToggleTrackOff, new SkinPartImage(Art, new Thickness(4))),
            new(SkinPart.ToggleTrackOn, new SkinPartImage(Art, new Thickness(4))),
            new(SkinPart.ToggleThumb, new SkinPartImage(Art, new Thickness(0))),
        ]);
        driver.Settle();
        var root = driver.Get("switch");
        float OnArt() => Images(root)[1].Visual.Opacity ?? 1f;

        var offShown = OnArt();
        on = true;
        driver.Rerender();

        var centre = root.ViewBounds;
        await Assert
            .That(
                driver
                    .View.HitTest(
                        new System.Numerics.Vector2(
                            centre.X + centre.Width / 2f,
                            centre.Y + centre.Height / 2f
                        )
                    )
                    ?.Kind
            )
            .IsNotEqualTo(NodeKind.Image);
        await Assert.That(Images(root).Count).IsEqualTo(3);
        await Assert.That(Has(root, static n => n.Layout.Border.Left > 0f)).IsFalse();
        await Assert.That(Has(root, static n => n.Visual.Background.IsVisible)).IsFalse();
        await Assert.That((offShown, OnArt())).IsEqualTo((0f, 1f));
    }

    static readonly TestTexture Art = new(40, 20);

    static List<Node> Images(Node root)
    {
        var found = new List<Node>();
        Collect(root, found);
        return found;

        static void Collect(Node node, List<Node> into)
        {
            foreach (var child in node.Children)
            {
                if (child.Kind == NodeKind.Image)
                    into.Add(child);
                Collect(child, into);
            }
        }
    }
}
