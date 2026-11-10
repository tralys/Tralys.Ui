using Tralys.Ui.Components;
using Tralys.Ui.Components.Tests;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class FieldTests
{
    static Paint Dark(ColorToken token) => Themes.Dark.Color(token);

    [Test]
    public async Task A_field_shows_its_value_strong_or_else_its_placeholder_beside_its_label()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, width: 320))
            {
                using (ui.Field(label: "Experience", value: ui.Format($"{1200:N0} / {40000:N0}")))
                { }
                using (ui.Field(label: "Team", placeholder: "None")) { }
            }
        });

        var value = driver.GetText("1,200 / 40,000");
        await Assert
            .That((value.Inherit.Color, driver.GetText("None").Inherit.Color))
            .IsEqualTo((Dark(ColorToken.FgStrong), Dark(ColorToken.FgPlaceholder)));
        await Assert
            .That(value.ViewBounds.X)
            .IsGreaterThan(driver.GetText("Experience").ViewBounds.Right);
    }

    [Test]
    public async Task Fields_given_one_label_width_line_their_controls_up_and_one_without_a_label_starts_at_its_edge()
    {
        var blank = new ObservableText();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, padding: 10, width: 320))
            {
                using (ui.Field(label: "A", labelWidth: 120))
                    ui.Input(text: blank, id: "short");
                using (ui.Field(label: "A much longer label", labelWidth: 120))
                    ui.Input(text: blank, id: "long");
                using (ui.Field())
                    ui.Input(text: blank, id: "bare");
            }
        });

        await Assert
            .That((driver.RootOf("short").ViewBounds.X, driver.RootOf("long").ViewBounds.X))
            .IsEqualTo((130f, 130f));
        await Assert.That(driver.RootOf("bare").ViewBounds.X).IsEqualTo(10f);
    }

    [Test]
    public async Task A_vertical_field_puts_its_label_above_a_control_as_wide_as_the_field()
    {
        var blank = new ObservableText();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, width: 280))
            using (ui.Field(label: "Motto", orientation: Orientation.Vertical))
                ui.Input(text: blank, placeholder: "Say something");
        });
        var label = driver.GetText("Motto").ViewBounds;
        var control = driver.RootOf().ViewBounds;

        await Assert.That((control.X, control.Width)).IsEqualTo((0f, 280f));
        await Assert.That(control.Y).IsGreaterThanOrEqualTo(label.Bottom + 8f);
    }

    [Test]
    public async Task A_field_marks_its_label_shows_an_info_icon_and_a_line_under_its_control_in_muted_or_danger_or_no_line_at_all()
    {
        var blank = new ObservableText();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, width: 320))
                {
                    using (
                        ui.Field(
                            label: "Name",
                            required: true,
                            infoTip: "Shown to all",
                            id: "tipped"
                        )
                    )
                        ui.Input(text: blank, id: "plain");
                    using (ui.Field(label: "Motto", description: "A hint", id: "hint"))
                        ui.Input(text: blank);
                    using (
                        ui.Field(
                            label: "Project",
                            description: "A hint",
                            error: "Taken",
                            id: "error"
                        )
                    )
                        ui.Input(text: blank);
                }
            })
            .WithIcons();
        var tipped = driver.Get("tipped");
        var label = driver.GetText("Name").ViewBounds;

        await Assert.That(driver.GetText("*").Inherit.Color).IsEqualTo(Dark(ColorToken.FgDanger));
        await Assert.That(Nodes.Glyph(tipped).ViewBounds.X).IsGreaterThan(label.Right);
        await Assert.That(tipped.Bounds.Height).IsEqualTo(driver.RootOf("plain").Bounds.Height);
        await Assert
            .That((driver.GetText("A hint").Inherit.Color, driver.GetText("Taken").Inherit.Color))
            .IsEqualTo((Dark(ColorToken.FgMuted), Dark(ColorToken.FgDanger)));
        await Assert
            .That(driver.GetText("Taken").ViewBounds.Y)
            .IsGreaterThan(driver.Get("error").ViewBounds.Y + 20f);
    }

    [Test]
    public async Task A_disabled_field_greys_its_label_and_its_value()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Field(label: "Level", value: "112", disabled: true)) { }
        });

        await Assert
            .That((driver.GetText("Level").Inherit.Color, driver.GetText("112").Inherit.Color))
            .IsEqualTo((Dark(ColorToken.FgDisabled), Dark(ColorToken.FgDisabled)));
    }

    [Test]
    public async Task A_resting_pointer_on_the_label_of_a_field_with_an_info_tip_shows_the_tip()
    {
        var blank = new ObservableText();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, padding: 60))
                using (ui.Field(label: "Weekly commits", infoTip: "Counts every commit"))
                    ui.Input(text: blank);
            })
            .WithIcons();

        driver.Hover(driver.GetText("Weekly commits"), settle: false);
        driver.Frame(0.35f);

        await Assert.That(OverlayProbes.ShownTip(driver)).IsEqualTo("Counts every commit");
    }

    [Test]
    public async Task Hovering_the_label_shows_its_info_tips_content()
    {
        var blank = new ObservableText();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, padding: 60))
                using (
                    ui.Field(
                        label: "Attack",
                        infoTipContent: Slot.Of(static ui => ui.Text("Base 40 + gear 12"))
                    )
                )
                    ui.Input(text: blank);
            })
            .WithIcons();

        driver.Hover(driver.GetText("Attack"), settle: false);
        driver.Frame(0.35f);

        await Assert.That(driver.GetText("Base 40 + gear 12")).IsNotNull();
    }
}
