using Tralys.Ui.Testing;
using Tralys.Ui.Tests;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components.Tests;

public sealed class PopoverTests
{
    static readonly Slot Body = Slot.Of(static ui =>
    {
        using (ui.Box(width: 50, height: 20, id: "body")) { }
    });

    [Test]
    public async Task A_trigger_opens_its_popup_below_it_start_aligned_and_padded_by_its_rungs_inset_shows_open_meanwhile_and_a_press_outside_closes_it()
    {
        var reports = new Reports();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 120, padding: 40))
            {
                using (ui.Popover(onOpenChange: reports.OnOpenChange, content: Body))
                using (ui.PopoverTrigger(id: "trigger"))
                    ui.OpenProbe();
                using (ui.Box(width: 40, height: 20, id: "blank")) { }
            }
        });
        var inset = Themes.Dark.Dimensions(Density.Pointer).Size(ComponentSize.Md).Inset;

        driver.Click(driver.Get("trigger"));
        var trigger = driver.Get("trigger").ViewBounds;
        var frame = driver.RootOf<Overlay.Frame>().ViewBounds;
        var body = driver.Get("body").ViewBounds;
        var opened = (driver.View.Layers.Length, driver.GetText("open") is not null);
        driver.Click(driver.Get("blank"));
        var closed = (driver.View.Layers.Length, driver.GetText("shut") is not null);
        driver.Click(driver.Get("trigger"));

        await Assert.That(opened).IsEqualTo((1, true));
        await Assert.That((frame.X, frame.Y)).IsEqualTo((trigger.X, trigger.Bottom));
        await Assert
            .That((body.X - frame.X, body.Y - frame.Y))
            .IsEqualTo((1f + inset.Left, 1f + inset.Top));
        await Assert.That(closed).IsEqualTo((0, true));
        await Assert.That(reports.Opens).IsEquivalentTo([true, false, true]);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
    }

    [Test]
    public async Task A_button_inside_an_open_popover_leaves_it_open_and_a_close_part_runs_its_click_then_closes_it()
    {
        var reports = new Reports();
        var clicks = new Clicks();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Popover(
                    defaultOpen: true,
                    onOpenChange: reports.OnOpenChange,
                    content: Slot.Of(
                        clicks,
                        static (ui, c) =>
                        {
                            ui.Button(
                                text: "Apply",
                                onClick: Callback.Of(c, static c => c.Apply++)
                            );
                            using (ui.PopoverClose(onClick: Callback.Of(c, static c => c.Done++)))
                                ui.Text("Done");
                        }
                    )
                )
            )
                ui.Text("Filters");
        });

        driver.Click(driver.GetText("Apply"));
        var kept = (driver.View.Layers.Length, reports.Opens.Count);
        driver.Click(driver.GetText("Done"));

        await Assert.That(clicks.Apply).IsEqualTo(1);
        await Assert.That(kept).IsEqualTo((1, 0));
        await Assert.That(clicks.Done).IsEqualTo(1);
        await Assert.That(reports.Opens).IsEquivalentTo([false]);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    [Test]
    public async Task A_toggle_given_as_the_trigger_opens_the_popover_through_its_own_value_and_the_popover_reports_nothing_for_its_click()
    {
        var open = new Switches();
        var toggled = new Reports();
        var reports = new Reports();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Popover(open: open.A, onOpenChange: reports.OnOpenChange, content: Body))
                ui.Toggle(
                    text: "Open",
                    value: open.A,
                    onValueChange: Callback<bool>.Of(
                        open,
                        toggled,
                        static (s, r, value) =>
                        {
                            r.Opens.Add(value);
                            s.A = value;
                        }
                    )
                );
        });

        driver.Click(driver.GetText("Open"));

        await Assert.That(toggled.Opens).IsEquivalentTo([true]);
        await Assert.That(reports.Opens).IsEmpty();
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
    }

    [Test]
    public async Task Space_on_a_trigger_clicked_open_closes_its_popover()
    {
        var reports = new Reports();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Popover(onOpenChange: reports.OnOpenChange, content: Body))
            using (ui.PopoverTrigger(id: "trigger"))
                ui.Text("Filters");
        });

        driver.Click(driver.Get("trigger"));
        var focused = driver.Focused == driver.Get("trigger");
        driver.Press(Key.Space);

        await Assert.That(focused).IsTrue();
        await Assert.That(reports.Opens).IsEquivalentTo([true, false]);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }
}

internal sealed class Clicks
{
    public int Apply;
    public int Done;
}

internal readonly record struct OpenProbeProps;

internal sealed class OpenProbe : Component<OpenProbeProps>
{
    protected override void Render(Ui ui, in OpenProbeProps p) =>
        ui.Text(ui.Use(Popover.Context).Open ? "open" : "shut");
}
