using System.Numerics;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Tests;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components.Tests;

public sealed partial class DialogTests
{
    static Callback<bool> Applied(Switches open, Reports reports) =>
        Callback<bool>.Of(
            open,
            reports,
            static (s, r, value) =>
            {
                r.Opens.Add(value);
                s.A = value;
            }
        );

    [Test]
    public async Task A_dialog_takes_focus_into_its_panel_as_it_opens_keeps_tab_inside_and_gives_focus_back_as_escape_or_its_close_button_closes_it()
    {
        var open = new Switches();
        var reports = new Reports();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Button(
                    text: "Open",
                    onClick: Callback.Of(open, static s => s.A = true),
                    id: "opener"
                );
                using (ui.Box(focusable: true, width: 40, height: 20, id: "beneath")) { }
            }
            using (ui.Dialog(open: open.A, onOpenChange: Applied(open, reports)))
            {
                ui.DialogTitle(text: "Rename");
                using (ui.Box(focusable: true, width: 40, height: 20, id: "first")) { }
                using (ui.Box(focusable: true, width: 40, height: 20, id: "second")) { }
                using (ui.DialogFooter())
                    ui.DialogClose(text: "Cancel");
            }
        });
        driver.Focus(driver.Get("opener"));

        driver.KeyDown(Key.Enter, settle: false);
        driver.Frame();
        driver.Frame();
        var layer = driver.View.Layers[0];
        var entered = OverlayProbes.Within(driver.Focused, layer);
        var stops = new List<bool>();
        for (var i = 0; i < 6; i++)
        {
            driver.Tab();
            stops.Add(OverlayProbes.Within(driver.Focused, layer));
        }
        driver.Press(Key.Escape);
        var escaped = (driver.View.Layers.Length, driver.Focused == driver.Get("opener"));
        driver.Click(driver.Get("opener"));
        driver.Click(driver.GetText("Cancel"));

        await Assert.That(entered).IsTrue();
        await Assert.That(stops).IsEquivalentTo([true, true, true, true, true, true]);
        await Assert.That(escaped).IsEqualTo((0, true));
        await Assert.That(reports.Opens).IsEquivalentTo([false, false]);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("opener"));
    }

    [Test]
    public async Task A_dialog_given_a_transition_fades_in_as_it_opens_and_stays_drawn_while_it_fades_out_as_it_closes()
    {
        var open = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Dialog(open: open.A, transition: new Transition(0.2f)))
                ui.DialogTitle(text: "Logging in");
        });

        open.A = true;
        driver.Frame();
        driver.Frame(0.1f);
        var opening = Scrim(driver).Visual.Opacity;
        driver.Settle();
        var opened = Scrim(driver).Visual.Opacity;
        open.A = false;
        driver.Frame();
        driver.Frame(0.1f);
        var closing = (driver.View.Layers.Length, Scrim(driver).Visual.Opacity);
        driver.Settle();

        await Assert.That(opening).IsNotNull().And.IsBetween(0.01f, 0.99f);
        await Assert.That(opened).IsEqualTo(1f);
        await Assert.That(closing.Item1).IsEqualTo(1);
        await Assert.That(closing.Item2).IsNotNull().And.IsBetween(0.01f, 0.99f);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    static Node Scrim(UiDriver driver) => driver.RootOf<Dialog.Scrim>();

    [Test]
    public async Task A_dialog_opening_with_a_field_that_takes_focus_itself_leaves_focus_on_that_field()
    {
        var open = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(focusable: true, width: 40, height: 20, id: "opener")) { }
            using (ui.Dialog(open: open.A))
            {
                using (ui.Box(focusable: true, width: 40, height: 20)) { }
                using (ui.Box(focusable: true, autoFocus: true, width: 40, height: 20, id: "field"))
                { }
            }
        });
        driver.Focus(driver.Get("opener"));

        open.A = true;
        driver.Settle();

        await Assert.That(driver.Focused).IsEqualTo(driver.Get("field"));
    }

    [Test]
    public async Task With_nothing_focused_keys_go_to_an_open_dialog_whose_enter_submits_and_whose_tab_focuses_its_first_stop()
    {
        var open = new Switches();
        var reports = new Reports();
        var behind = new Reports();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    direction: Direction.Column,
                    onKeyDown: Handler<KeyEvent>.Of(
                        behind,
                        static (Reports r, ref KeyEvent e) => r.Keys++
                    )
                )
            )
            {
                using (ui.Box(focusable: true, width: 40, height: 20, id: "opener")) { }
                using (ui.Dialog(open: open.A, onSubmit: reports.OnSubmit))
                    ui.Text("Saved");
            }
        });
        driver.Focus(driver.Get("opener"));
        open.A = true;
        driver.Settle();
        driver.View.ClearFocus();

        driver.KeyDown(Key.Enter, settle: false);
        var enterBehind = behind.Keys;
        driver.Type("a", settle: false);
        driver.Tab(settle: false);

        await Assert.That(reports.Submits).IsEqualTo(1);
        await Assert.That(enterBehind).IsEqualTo(0);
        await Assert.That(OverlayProbes.Within(driver.Focused, driver.View.Layers[0])).IsTrue();
    }

    [Test]
    public async Task A_press_on_a_dialogs_scrim_reaches_nothing_beneath_reports_a_close_and_leaves_focus_until_it_closes()
    {
        var open = new Switches { A = true };
        var reports = new Reports();
        var beneath = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 80,
                    height: 30,
                    onClick: Callback.Of(beneath, static c => c.Value++),
                    id: "beneath"
                )
            ) { }
            using (ui.Dialog(open: open.A, onOpenChange: reports.OnOpenChange))
            using (ui.Box(focusable: true, width: 40, height: 20, id: "field")) { }
        });
        var under = OverlayProbes.Centre(driver.Get("beneath"));
        driver.Focus(driver.Get("field"));

        driver.Click(under);
        var held = (beneath.Value, driver.Focused == driver.Get("field"));
        open.A = false;
        driver.Settle();
        driver.Click(under);

        await Assert.That(held).IsEqualTo((0, true));
        await Assert.That(reports.Opens).IsEquivalentTo([false]);
        await Assert.That(beneath.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_dialog_leaving_the_view_with_focus_inside_gives_it_back_to_its_opener_past_a_popup_inside_it_and_tells_its_field_after_it_left()
    {
        var shown = new Switches { A = true };
        var open = new Switches();
        var heard = new List<int>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(focusable: true, width: 40, height: 20, id: "opener")) { }
            if (!shown.A)
                return;
            using (ui.Dialog(open: open.A))
            {
                using (
                    ui.Box(
                        focusable: true,
                        width: 40,
                        height: 20,
                        onFocusChange: Callback<FocusChange>.Of(
                            heard,
                            static (h, change) =>
                            {
                                if (!change.Focused)
                                    h.Add(1);
                            }
                        ),
                        id: "field"
                    )
                ) { }
                using (ui.Popover(open: open.B, content: PopupField))
                    ui.Text("More");
            }
        });
        driver.Focus(driver.Get("opener"));
        open.A = true;
        driver.Settle();
        driver.Focus(driver.Get("field"));

        shown.A = false;
        driver.Frame();
        var left = (driver.View.Layers.Length, driver.Focused == driver.Get("opener"), heard.Count);

        shown.A = true;
        open.A = false;
        driver.Settle();
        driver.Focus(driver.Get("opener"));
        open.A = true;
        driver.Settle();
        driver.Focus(driver.Get("field"));
        open.B = true;
        driver.Settle();
        driver.Focus(driver.Get("popup-field"));
        shown.A = false;
        driver.Settle();

        await Assert.That(left).IsEqualTo((0, true, 1));
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("opener"));
    }

    static readonly Slot PopupField = Slot.Of(static ui =>
    {
        using (ui.Box(focusable: true, width: 40, height: 20, id: "popup-field")) { }
    });

    [Test]
    public async Task Enter_alone_or_with_ctrl_submits_a_dialog_once_unless_the_focused_field_takes_it_or_submitting_is_off_and_no_enter_leaves_it()
    {
        var model = new DialogModel();
        var reports = new Reports();
        var behind = new Reports();
        var plainText = new ObservableText("plain");
        var ownText = new ObservableText("own");
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    onKeyDown: Handler<KeyEvent>.Of(
                        behind,
                        static (Reports r, ref KeyEvent e) => r.Keys++
                    )
                )
            )
            using (
                ui.Dialog(open: true, onSubmit: reports.OnSubmit, submitDisabled: model.Disabled)
            )
            {
                ui.Input(text: plainText, id: "plain");
                ui.Input(
                    text: ownText,
                    onSubmit: Callback.Of(model, static m => m.OwnSubmits++),
                    id: "own"
                );
            }
        });

        driver.Press(Key.Enter);
        var onPanel = reports.Submits;
        driver.Click(driver.Get("own"));
        driver.Press(Key.Enter);
        var ownTook = (model.OwnSubmits, reports.Submits);
        driver.Click(driver.Get("plain"));
        driver.Press(Key.Enter);
        var plain = reports.Submits;
        driver.Press(Key.Enter, ModifierKeys.Shift);
        driver.Press(Key.Enter, ModifierKeys.Alt);
        var modified = reports.Submits;
        driver.Press(Key.Enter, ModifierKeys.Control);
        var ctrl = reports.Submits;
        model.Disabled = true;
        driver.Settle();
        driver.Press(Key.Enter);
        driver.Press(Key.Enter, ModifierKeys.Control);

        await Assert.That(onPanel).IsEqualTo(1);
        await Assert.That(ownTook).IsEqualTo((1, 1));
        await Assert.That(plain).IsEqualTo(2);
        await Assert.That(modified).IsEqualTo(2);
        await Assert.That(ctrl).IsEqualTo(3);
        await Assert.That(reports.Submits).IsEqualTo(3);
        await Assert.That(behind.Keys).IsEqualTo(0);
    }

    [Test]
    public async Task A_dialog_that_can_close_shows_a_corner_and_a_footer_close_button_and_one_that_can_submit_a_submit_button_greyed_while_off_and_red_when_destructive()
    {
        var model = new DialogModel();
        var reports = new Reports();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (
                    ui.Dialog(
                        open: true,
                        onOpenChange: model.Closable ? reports.OnOpenChange : default,
                        onSubmit: model.Submits ? reports.OnSubmit : default,
                        submitDisabled: model.Disabled
                    )
                )
                using (ui.DialogFooter())
                {
                    ui.DialogClose();
                    ui.DialogSubmit(text: "Delete", destructive: model.Destructive);
                }
            })
            .WithIcons();
        var t = Themes.Dark;

        driver.Click(driver.RootOf<CloseButton>());
        driver.Click(driver.GetText("Close"));
        driver.Click(driver.GetText("Delete"));
        driver.Unhover();
        var submit = ButtonAround(driver, "Delete");
        var primary = submit.Visual.Background;
        model.Disabled = true;
        model.Destructive = true;
        driver.Settle();
        driver.Click(driver.GetText("Delete"));
        var off = (submit.States & InputStates.Disabled) != 0;
        model.Disabled = false;
        driver.Unhover();
        var danger = submit.Visual.Background;
        model.Closable = false;
        model.Submits = false;
        driver.Settle();

        await Assert.That(reports.Opens).IsEquivalentTo([false, false]);
        await Assert.That(reports.Submits).IsEqualTo(1);
        await Assert
            .That(primary)
            .IsEqualTo(
                (Paint)t.Colors(ColorScheme.Primary, default, ComponentState.Normal).Background
            );
        await Assert.That(off).IsTrue();
        await Assert
            .That(danger)
            .IsEqualTo(
                (Paint)t.Colors(ColorScheme.Danger, default, ComponentState.Normal).Background
            );
        await Assert.That(driver.All<CloseButton>()).IsEmpty();
        await Assert.That(driver.All<Button>()).IsEmpty();
    }

    [Test]
    public async Task A_dialogs_title_body_and_footer_stand_where_todays_did_its_body_as_wide_as_asked_and_each_left_out_takes_no_room()
    {
        var model = new DialogModel { Closable = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Dialog(
                    open: true,
                    onOpenChange: model.Closable ? Callback<bool>.Of(static _ => { }) : default,
                    width: model.Wide ? 320 : Length.Auto
                )
            )
            {
                if (model.Titled)
                    ui.DialogTitle(text: "Delete preset");
                using (ui.Box(width: 200, height: 40, id: "body")) { }
                if (model.Closable)
                    using (ui.DialogFooter())
                        ui.DialogClose();
            }
        });
        var t = Themes.Dark;
        Rect Panel() => driver.Get<Dialog>() is var d ? PanelOf(d) : default;

        var body = driver.Get("body").ViewBounds;
        var title = driver.GetText("Delete preset");
        var footer = driver.RootOf<Button>().ViewBounds;
        var panel = Panel();
        var titled = (
            title.ViewBounds.Y - panel.Y,
            body.Y - title.ViewBounds.Bottom,
            footer.Y - body.Bottom,
            panel.Bottom - footer.Bottom
        );
        var accent = title.Inherit.Color;
        model.Titled = false;
        model.Closable = false;
        driver.Settle();
        body = driver.Get("body").ViewBounds;
        panel = Panel();
        var bare = (body.Y - panel.Y, panel.Bottom - body.Bottom, body.X - panel.X);
        model.Wide = true;
        driver.Settle();

        await Assert.That(titled).IsEqualTo((17f, 32f, 24f, 17f));
        await Assert.That(accent).IsEqualTo((Paint)t.Color(ColorToken.FgAccent));
        await Assert.That(bare).IsEqualTo((17f, 17f, 17f));
        var widened = await Assert.That(driver.Get("body").Parent).IsNotNull();
        await Assert.That(widened.ViewBounds.Width).IsEqualTo(320f);
    }

    [Test]
    public async Task A_footers_start_slot_shows_at_its_start_and_follows_the_state_it_reads()
    {
        var label = new Label { Text = "first" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Dialog(open: true, onOpenChange: Callback<bool>.Of(static _ => { })))
            using (ui.DialogFooter(start: Slot.Of(label, static (ui, l) => ui.Text(l.Text))))
                ui.DialogClose();
        });
        var first = driver.GetText("first").ViewBounds.X < driver.GetText("Close").ViewBounds.X;

        label.Text = "second";
        driver.Settle();

        await Assert.That(first).IsTrue();
        await Assert.That(driver.GetText("second")).IsNotNull();
    }

    [Test]
    public async Task A_dialog_taller_than_the_view_stops_16_short_of_its_edges_and_a_body_written_as_a_scroll_box_scrolls_only_down_under_a_still_title()
    {
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Dialog(open: true))
                {
                    ui.DialogTitle(text: "Licence");
                    using (
                        ui.Box(
                            direction: Direction.Column,
                            overflow: Overflow.ScrollY,
                            height: 400,
                            id: "body"
                        )
                    )
                    using (ui.Box(width: 200, height: 800)) { }
                }
            },
            size: new Size(400, 300)
        );
        var panel = PanelOf(driver.Get<Dialog>());
        var title = driver.GetText("Licence").ViewBounds;
        var body = driver.Get("body");
        var top = body.Children.GetEnumerator();
        top.MoveNext();
        var content = top.Current;
        var before = content.ViewBounds;

        driver.Wheel(body, new Vector2(0f, -3f));
        driver.Wheel(body, new Vector2(-3f, 0f));

        await Assert.That((panel.Y, panel.Bottom)).IsEqualTo((16f, 284f));
        await Assert.That(driver.GetText("Licence").ViewBounds).IsEqualTo(title);
        await Assert.That(content.ViewBounds.Y).IsLessThan(before.Y);
        await Assert.That(content.ViewBounds.X).IsEqualTo(before.X);
    }

    static Node ButtonAround(UiDriver driver, string text)
    {
        var label = driver.GetText(text);
        foreach (var button in driver.All<Button>())
        {
            if (OverlayProbes.Within(label, button))
                return OverlayProbes.BoxOf(button);
        }
        throw new InvalidOperationException($"No button says {text}.");
    }

    static Rect PanelOf(Node dialog)
    {
        var panel = OverlayProbes.FirstOf(
            dialog,
            static n => n.Kind == NodeKind.Box && n.Layout.Border.Left > 0f
        );
        return panel?.ViewBounds ?? default;
    }

    sealed partial class DialogModel : ObservableObject
    {
        [ObservableProperty]
        public partial bool Disabled { get; set; }

        [ObservableProperty]
        public partial bool Destructive { get; set; }

        [ObservableProperty]
        public partial bool Closable { get; set; } = true;

        [ObservableProperty]
        public partial bool Submits { get; set; } = true;

        [ObservableProperty]
        public partial bool Titled { get; set; } = true;

        [ObservableProperty]
        public partial bool Wide { get; set; }

        public int OwnSubmits;
    }

    sealed partial class Label : ObservableObject
    {
        [ObservableProperty]
        public partial string Text { get; set; } = "";
    }
}
