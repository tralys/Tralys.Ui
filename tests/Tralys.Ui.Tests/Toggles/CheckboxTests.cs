using Tralys.Ui.Components;
using Tralys.Ui.Components.Tests;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;
using static Tralys.Ui.Tests.ToggleLooks;

namespace Tralys.Ui.Tests;

public sealed class CheckboxTests
{
    [Test]
    public async Task A_click_or_space_reports_the_next_value_without_showing_it_and_a_three_state_box_fed_back_passes_through_indeterminate()
    {
        var reports = new List<bool>();
        var choices = new Choices();
        var log = new List<CheckState>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Checkbox(
                    value: false,
                    onValueChange: Callback<bool>.Of(reports, static (r, v) => r.Add(v)),
                    label: "Two",
                    id: "two"
                );
                ui.Checkbox(
                    value: choices.State,
                    onValueChange: choices.SetState,
                    label: "Three",
                    id: "three"
                );
            }
        });
        var two = driver.Get("two");
        var three = driver.Get("three");

        driver.Click(two);
        driver.Focus(two);
        driver.Press(Key.Space);
        var shown = Scale(Tick(two));
        for (var i = 0; i < 3; i++)
        {
            driver.Click(three);
            log.Add(choices.State);
        }

        await Assert.That(string.Join(" ", reports)).IsEqualTo("True True");
        await Assert.That(shown).IsEqualTo(0f);
        await Assert.That(string.Join(" ", log)).IsEqualTo("Checked Indeterminate Unchecked");
    }

    [Test]
    public async Task Plus_checks_and_minus_unchecks_a_two_state_box_but_ctrl_plus_and_a_three_state_box_pass_the_key_on()
    {
        var choices = new Choices();
        var reports = new List<CheckState>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Checkbox(value: choices.On, onValueChange: choices.SetOn, id: "two");
                ui.Checkbox(
                    value: CheckState.Indeterminate,
                    onValueChange: Callback<CheckState>.Of(reports, static (r, v) => r.Add(v)),
                    id: "three"
                );
            }
        });
        driver.Focus(driver.Get("two"));

        var plus = driver.KeyDown(Key.Add);
        driver.KeyUp(Key.Add);
        var checkedByPlus = choices.On;
        var minus = driver.KeyDown(Key.OemMinus, ModifierKeys.Shift);
        driver.KeyUp(Key.OemMinus, ModifierKeys.Shift);
        var uncheckedByMinus = !choices.On;
        var ctrlPlus = driver.KeyDown(Key.OemPlus, ModifierKeys.Control);
        driver.KeyUp(Key.OemPlus, ModifierKeys.Control);
        driver.Focus(driver.Get("three"));
        var triPlus = driver.KeyDown(Key.Add);

        await Assert
            .That((plus, checkedByPlus, minus, uncheckedByMinus))
            .IsEqualTo((true, true, true, true));
        await Assert.That((ctrlPlus, choices.On, triPlus)).IsEqualTo((false, false, false));
        await Assert.That(reports).IsEmpty();
    }

    [Test]
    public async Task Enter_on_a_focused_checkbox_or_switch_presses_the_default_button_while_south_flips_it()
    {
        var choices = new Choices();
        var confirmed = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.ActionScope())
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Checkbox(value: choices.On, onValueChange: choices.SetOn, id: "box");
                ui.Switch(value: choices.Open, onValueChange: choices.SetOpen, id: "switch");
                ui.Button(
                    text: "Save",
                    action: UiAction.Confirm,
                    onClick: Callback.Of(confirmed, static c => c.Value++)
                );
            }
        });

        driver.Focus(driver.Get("box"));
        driver.Press(Key.Enter);
        driver.Press(GamepadButton.South);
        driver.Focus(driver.Get("switch"));
        driver.Press(Key.Enter);
        driver.Press(GamepadButton.South);

        await Assert.That((choices.On, choices.Open)).IsEqualTo((true, true));
        await Assert.That(confirmed.Value).IsEqualTo(2);
    }

    [Test]
    public async Task A_box_lights_under_the_pointer_presses_while_held_fills_while_checked_squares_while_undecided_and_rings_only_when_tabbed_to()
    {
        var state = CheckState.Unchecked;
        var disabled = false;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Checkbox(value: state, label: "Box", disabled: disabled, id: "box");
                using (ui.Box(width: 40, height: 40, id: "away")) { }
            }
        });
        var box = driver.Get("box");
        var bg = Dark(ColorToken.Bg);
        (Paint, Paint) Surface() => (Edged(box).Visual.Background, Edged(box).Visual.BorderPaint);
        var label = Nodes.Find(box, NodeKind.Text);

        var rest = Surface();
        driver.Tab();
        var ringed = ButtonLooks.HasRing(box);
        driver.Hover(box);
        var lit = (Surface(), label.Inherit.Color, driver.Cursor);
        var held = driver.Hold(box);
        var pressed = (Surface(), ButtonLooks.HasRing(box));
        held.Release();
        driver.Unhover();
        var after = Surface();
        state = CheckState.Checked;
        driver.Rerender();
        var on = Surface();
        state = CheckState.Indeterminate;
        driver.Rerender();
        var undecided = (
            Surface(),
            Has(box, static n => n.Visual.Background == Dark(ColorToken.Icon)),
            Has(box, static n => n.Kind == NodeKind.Canvas)
        );
        disabled = true;
        driver.Rerender();

        await Assert.That(rest).IsEqualTo((bg, Dark(ColorToken.Border)));
        await Assert
            .That(lit)
            .IsEqualTo(
                ((bg, Dark(ColorToken.BorderHover)), Dark(ColorToken.FgStrong), Cursors.Hand)
            );
        await Assert
            .That(pressed)
            .IsEqualTo(
                (
                    (
                        (Paint)
                            Themes
                                .Dark.Color(ColorToken.StatePressed)
                                .Over(Themes.Dark.Color(ColorToken.Bg)),
                        Dark(ColorToken.BorderPressed)
                    ),
                    false
                )
            );
        await Assert.That(after).IsEqualTo(rest);
        await Assert.That(ringed).IsTrue();
        await Assert
            .That(on)
            .IsEqualTo((Dark(ColorToken.BgSelected), Dark(ColorToken.BorderSelected)));
        await Assert.That(undecided).IsEqualTo((rest, true, false));
        await Assert.That(box.Visual.Opacity ?? 1f).IsLessThan(1f);
    }

    [Test]
    public async Task A_tick_shows_in_full_on_its_first_frame_grows_in_over_a_tenth_and_shrinks_out_over_a_fifth_slowing_down()
    {
        var choices = new Choices();
        using var driver = UiDriver.Render(ui =>
        {
            if (choices.Count > 0)
                ui.Checkbox(value: choices.On, id: "box");
        });

        choices.On = true;
        choices.Count = 1;
        driver.Frame(0f);
        var first = Scale(Tick(driver.Get("box")));
        choices.On = false;
        driver.Settle();
        choices.On = true;
        driver.Frame(0f);
        driver.Frame(0.05f);
        var growing = Scale(Tick(driver.Get("box")));
        driver.Frame(0.06f);
        var grown = Scale(Tick(driver.Get("box")));
        choices.On = false;
        driver.Frame(0f);
        driver.Frame(0.15f);
        var shrinking = Scale(Tick(driver.Get("box")));
        driver.Frame(0.06f);

        await Assert.That(first).IsEqualTo(1f);
        await Assert.That(growing).IsGreaterThan(0.5f).And.IsLessThan(1f);
        await Assert.That(grown).IsEqualTo(1f);
        await Assert.That(shrinking).IsGreaterThan(0f);
        await Assert.That(Scale(Tick(driver.Get("box")))).IsEqualTo(0f);
    }

    [Test]
    public async Task A_row_recycled_for_an_unchecked_item_shows_its_box_unticked_on_its_first_frame()
    {
        var scroll = new ScrollHolder();
        using var driver = UiDriver.Render(ui =>
        {
            var handle = ui.ScrollHandle();
            scroll.Handle = handle;
            ui.VirtualList(
                items: Rows,
                itemKey: static item => item,
                row: Row,
                rowHeight: 24,
                scroll: handle,
                height: 120
            );
        });

        var checkedRows = new HashSet<Node>(driver.All<Checkbox<bool>>());

        var handle = await Assert.That(scroll.Handle).IsNotNull();
        handle.ScrollTo(null, 24f * 30f);
        driver.Settle();
        handle.ScrollToEnd();
        driver.Frame();
        var ticks = new List<float>();
        var reused = 0;
        foreach (var box in driver.All<Checkbox<bool>>())
        {
            if (!Shown(box))
                continue;
            ticks.Add(Scale(Tick(box)));
            if (checkedRows.Contains(box))
                reused++;
        }

        await Assert.That(reused).IsGreaterThan(0);
        await Assert.That(ticks).All().Satisfy(static t => t.IsEqualTo(0f));
    }

    static readonly IItemsSource<int> Rows = ItemsSource.From(Enumerable.Range(0, 60).ToArray());

    static readonly Slot<int> Row = Slot<int>.Of(
        static (ui, item, _) => ui.Checkbox(value: item < 20)
    );

    static bool Shown(Node node)
    {
        for (var n = node; n is not null; n = n.Parent)
            if (n.Place.Collapsed)
                return false;
        return true;
    }

    sealed class ScrollHolder
    {
        public ScrollHandle? Handle;
    }

    [Test]
    public async Task An_info_tip_puts_an_icon_after_the_label_whose_tip_shows_only_under_a_resting_pointer_and_goes_with_the_text()
    {
        var tip = new Tip { Text = "Saves every minute" };
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, padding: 60))
                {
                    ui.Checkbox(value: false, label: "Autosave", infoTip: tip.Text, id: "box");
                    ui.Checkbox(value: false, label: "Elsewhere", id: "elsewhere");
                }
            })
            .WithIcons();
        var icon = Nodes.Glyph(driver.Get("box"));
        var label = driver.GetText("Autosave");
        var after = icon.ViewBounds.X > label.ViewBounds.Right;

        driver.Hover(label, settle: false);
        driver.Frame(Rest);
        var onLabel = OverlayProbes.ShownTip(driver);
        driver.Hover(icon, settle: false);
        driver.Frame(Rest);
        var onIcon = OverlayProbes.ShownTip(driver);
        driver.Hover(driver.Get("elsewhere"));
        var left = OverlayProbes.ShownTip(driver);
        tip.Text = default;
        driver.Rerender();

        await Assert.That(after).IsTrue();
        await Assert.That((onLabel, onIcon, left)).IsEqualTo((null, "Saves every minute", null));
        await Assert
            .That(OverlayProbes.FirstOf(driver.Get("box"), static n => n.Component == typeof(Icon)))
            .IsNull();
    }

    const float Rest = 0.35f;

    sealed class Tip
    {
        public TextValue Text;
    }
}
