using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class OverlayTests
{
    static readonly Slot PopoverBody = Slot.Of(static ui => ui.Text("Only online contacts"));

    static readonly Slot SizedBody = Slot.Of(static ui =>
    {
        using (ui.Box(width: 54, height: 24)) { }
    });

    [Test]
    public async Task A_layer_root_tells_its_tier_hit_dismissal_and_whether_it_is_modal_and_other_nodes_none()
    {
        using var driver = UiDriver.Render(static ui =>
        {
            using (ui.Box(id: "owner"))
            {
                using (ui.Layer(hit: LayerHit.PassThrough, tier: LayerTier.Tooltip, id: "tip"))
                    ui.Text("tip");
                using (
                    ui.Layer(
                        dismissOn: Dismiss.Escape | Dismiss.OutsidePress,
                        modal: true,
                        id: "dialog"
                    )
                )
                    ui.Text("dialog");
            }
        });

        await Assert
            .That(driver.Get("tip").Layer)
            .IsEqualTo(new LayerInfo(LayerTier.Tooltip, LayerHit.PassThrough, Dismiss.None, false));
        await Assert
            .That(driver.Get("dialog").Layer)
            .IsEqualTo(
                new LayerInfo(
                    LayerTier.Default,
                    LayerHit.Content,
                    Dismiss.Escape | Dismiss.OutsidePress,
                    true
                )
            );
        await Assert.That(driver.Get("owner").Layer).IsNull();
    }

    [Test]
    public async Task A_dialog_takes_focus_into_its_panel_and_a_scrim_press_closes_it_giving_focus_back()
    {
        var open = new Switches();
        var reports = new List<bool>();
        var beneath = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 10, padding: 10))
            {
                using (
                    ui.Box(
                        width: 80,
                        height: 30,
                        onClick: Callback.Of(open, static s => s.A = true),
                        id: "open"
                    )
                ) { }
                using (
                    ui.Box(
                        width: 80,
                        height: 30,
                        onClick: Callback.Of(beneath, static c => c.Value++),
                        id: "beneath"
                    )
                ) { }
            }
            using (
                ui.Dialog(
                    open: open.A,
                    onOpenChange: Callback<bool>.Of(
                        open,
                        reports,
                        static (s, r, value) =>
                        {
                            r.Add(value);
                            s.A = value;
                        }
                    )
                )
            )
            {
                ui.DialogTitle(text: "Delete 3 files?");
                using (ui.DialogFooter())
                    ui.DialogClose(text: "Cancel", id: "cancel");
            }
        });
        var under = Centre(driver.Get("beneath"));

        driver.Click(driver.Get("open"));
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("cancel"));

        driver.Click(driver.Get("dialog-panel"), new Vector2(4, 4));
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("cancel"));
        await Assert.That(reports).IsEmpty();

        driver.Click(under);
        await Assert.That(reports).IsEquivalentTo([false]);
        await Assert.That(beneath.Value).IsEqualTo(0);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("open"));
    }

    [Test]
    public async Task Pressing_an_open_popovers_trigger_closes_it_and_neither_the_press_nor_its_release_goes_on()
    {
        var clicks = new Counter();
        var releases = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            var open = ui.State(false);
            using (ui.Popover(open: open.Value, onOpenChange: open.Setter, content: PopoverBody))
            {
                using (
                    ui.Box(
                        width: 60,
                        height: 20,
                        onClick: Callback.Of(
                            open,
                            clicks,
                            static (s, c) =>
                            {
                                c.Value++;
                                s.Value = !s.Value;
                            }
                        ),
                        onPointerUp: Handler<PointerEvent>.Of(
                            releases,
                            static (Counter c, ref PointerEvent e) => c.Value++
                        ),
                        id: "trigger"
                    )
                ) { }
            }
        });

        driver.Click(driver.Get("trigger"));
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);

        driver.Click(driver.Get("trigger"));
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
        await Assert.That(clicks.Value).IsEqualTo(1);
        await Assert.That(releases.Value).IsEqualTo(1);
    }

    [Test]
    public async Task Escape_closes_the_newest_dismissable_layer_one_per_press_passing_one_that_stays()
    {
        var open = new Switches
        {
            A = true,
            B = true,
            C = true,
        };
        using var driver = UiDriver.Render(ui =>
        {
            if (open.A)
            {
                using (
                    ui.Layer(
                        dismissOn: Dismiss.Escape,
                        onDismiss: Callback.Of(open, static s => s.A = false)
                    )
                ) { }
            }
            if (open.B)
            {
                using (
                    ui.Layer(
                        dismissOn: Dismiss.Escape,
                        onDismiss: Callback.Of(open, static s => s.B = false)
                    )
                ) { }
            }
            if (open.C)
            {
                using (ui.Layer()) { }
            }
        });

        driver.Press(Key.Escape);
        await Assert.That((open.A, open.B, open.C)).IsEqualTo((true, false, true));
        driver.Press(Key.Escape);
        await Assert.That((open.A, open.B, open.C)).IsEqualTo((false, false, true));
        var taken = driver.KeyDown(Key.Escape);
        driver.KeyUp(Key.Escape);
        await Assert.That(taken).IsFalse();
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
    }

    [Test]
    public async Task An_escape_inside_a_dismissable_layer_stops_there_and_inside_one_that_stays_reaches_the_owner()
    {
        var flags = new Switches { A = true };
        var heard = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    onKeyDown: Handler<KeyEvent>.Of(
                        heard,
                        static (Counter c, ref KeyEvent e) =>
                        {
                            if (e.Key == Key.Escape)
                                c.Value++;
                        }
                    )
                )
            )
            {
                using (
                    ui.Layer(
                        dismissOn: flags.A ? Dismiss.Escape : Dismiss.None,
                        onDismiss: Callback.Of(flags, static s => s.B = true)
                    )
                )
                {
                    using (ui.Box(focusable: true, width: 20, height: 20, id: "inside")) { }
                }
            }
        });
        driver.Focus(driver.Get("inside"));

        driver.Press(Key.Escape);
        await Assert.That((heard.Value, flags.B)).IsEqualTo((0, true));

        flags.A = false;
        driver.Settle();
        driver.Press(Key.Escape);
        await Assert.That(heard.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_press_inside_a_layer_opened_from_another_leaves_both_open()
    {
        var dismissals = new Counter();
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Positioner(
                    anchor: Anchor.Point(new Vector2(10, 10)),
                    placement: PlacementMode.Absolute,
                    dismissOn: Dismiss.OutsidePress,
                    onDismiss: Callback.Of(dismissals, static c => c.Value++)
                )
            )
            {
                using (ui.Box(width: 100, height: 100, background: Color.FromRgb(9, 9, 9)))
                {
                    using (
                        ui.Positioner(
                            placement: PlacementMode.RightStart,
                            dismissOn: Dismiss.OutsidePress,
                            onDismiss: Callback.Of(dismissals, static c => c.Value++)
                        )
                    )
                    {
                        using (
                            ui.Box(
                                width: 50,
                                height: 50,
                                onClick: Callback.Of(clicks, static c => c.Value++),
                                id: "inner"
                            )
                        ) { }
                    }
                }
            }
        });
        await Assert.That(driver.Get("inner").ViewBounds).IsEqualTo(new Rect(110, 10, 50, 50));

        driver.Click(driver.Get("inner"));

        await Assert.That((dismissals.Value, clicks.Value)).IsEqualTo((0, 1));
        await Assert.That(driver.View.Layers.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Layers_hit_by_tier_then_newest_first_and_a_pass_through_layer_takes_nothing()
    {
        var shown = new Switches { A = true };
        var hits = new List<string>();
        using var driver = UiDriver.Render(ui =>
        {
            if (shown.A)
            {
                using (ui.Layer(tier: LayerTier.Toast))
                {
                    using (
                        ui.Box(
                            width: 50,
                            height: 50,
                            onClick: Callback.Of(hits, static h => h.Add("toast"))
                        )
                    ) { }
                }
            }
            using (ui.Layer())
            {
                using (
                    ui.Box(
                        width: 50,
                        height: 50,
                        onClick: Callback.Of(hits, static h => h.Add("first"))
                    )
                ) { }
            }
            using (ui.Layer())
            {
                using (
                    ui.Box(
                        width: 50,
                        height: 50,
                        onClick: Callback.Of(hits, static h => h.Add("second"))
                    )
                ) { }
            }
            using (ui.Layer(tier: LayerTier.Tooltip, hit: LayerHit.PassThrough))
            {
                using (
                    ui.Box(
                        width: 50,
                        height: 50,
                        onClick: Callback.Of(hits, static h => h.Add("tip"))
                    )
                ) { }
            }
        });

        driver.Click(new Vector2(25, 25));
        shown.A = false;
        driver.Settle();
        driver.Click(new Vector2(25, 25));

        await Assert.That(hits).IsEquivalentTo(["toast", "second"]);
    }

    [Test]
    public async Task A_popover_a_hint_a_menu_and_a_combobox_at_the_views_edges_flip_and_slide_through_one_positioner()
    {
        var open = new Switches();
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(position: Position.Absolute, left: 100, top: 270))
                {
                    using (ui.Popover(open: open.A, content: SizedBody))
                    {
                        using (ui.Box(width: 60, height: 20)) { }
                    }
                }
                using (ui.Box(position: Position.Absolute, left: 380, top: 0))
                {
                    using (ui.Hint(open: open.A))
                    {
                        using (ui.Box(width: 20, height: 20)) { }
                    }
                }
                using (ui.Box(position: Position.Absolute, right: 0, top: 100))
                {
                    using (ui.Menu(open: open.A))
                    {
                        using (ui.Box(width: 30, height: 20)) { }
                    }
                }
                using (ui.Box(position: Position.Absolute, left: 200, top: 250))
                    ui.Combobox(
                        items: Fruits.Items,
                        itemKey: Fruits.ByName,
                        itemText: Fruits.Named,
                        open: open.A,
                        width: 120
                    );
            },
            size: new Size(400, 300)
        );

        open.A = true;
        driver.Frame();
        var field = Nodes.Find(driver.Get<Combobox<string>>(), NodeKind.Box).ViewBounds;
        var list = Nodes.Find(driver.Get<ComboboxPopup<string>>(), NodeKind.Box).ViewBounds;

        await Assert.That(driver.Get("popover").ViewBounds).IsEqualTo(new Rect(100, 214, 80, 50));
        await Assert.That(driver.Get("hint").ViewBounds).IsEqualTo(new Rect(360, 24, 40, 20));
        await Assert.That(driver.Get("menu").ViewBounds).IsEqualTo(new Rect(340, 120, 60, 90));
        await Assert.That(list.Bottom).IsEqualTo(field.Y);
        await Assert.That(list.X).IsEqualTo(field.X);
        await Assert.That(list.Width).IsEqualTo(field.Width);

        driver.Settle();
        driver.Frame();
        await Assert.That(driver.Stats.DrawListRebuilt).IsFalse();
    }

    [Test]
    public async Task A_positioner_that_fits_neither_side_takes_the_larger_side_shrunk_to_its_room_at_its_anchors_width()
    {
        using var driver = UiDriver.Render(
            ui =>
            {
                var anchor = ui.Ref();
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 10,
                        top: 120,
                        width: 40,
                        height: 20,
                        nodeRef: anchor
                    )
                ) { }
                using (
                    ui.Positioner(
                        anchor: anchor,
                        matchAnchorWidth: true,
                        overflow: Overflow.Clip,
                        id: "list"
                    )
                )
                {
                    using (ui.Box(width: 50, height: 150, shrink: 0)) { }
                }
            },
            size: new Size(300, 200)
        );

        await Assert.That(driver.Get("list").ViewBounds).IsEqualTo(new Rect(10, 0, 40, 120));
    }

    [Test]
    public async Task A_positioner_at_the_pointer_opens_below_right_of_where_the_press_landed()
    {
        var open = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 300,
                    height: 300,
                    onPointerDown: Handler<PointerEvent>.Of(
                        open,
                        static (Switches s, ref PointerEvent e) => s.A = true
                    ),
                    id: "surface"
                )
            ) { }
            if (open.A)
            {
                using (ui.Positioner(anchor: Anchor.Pointer, id: "menu"))
                {
                    using (ui.Box(width: 40, height: 40)) { }
                }
            }
        });

        driver.Click(driver.Get("surface"), new Vector2(120, 80));

        await Assert.That(driver.Get("menu").ViewBounds).IsEqualTo(new Rect(120, 80, 40, 40));
    }

    [Test]
    public async Task A_positioner_follows_its_anchor_and_once_the_anchor_is_clipped_out_holds_its_place_or_dismisses()
    {
        var shift = new Model();
        var dismissals = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            var anchor = ui.Ref();
            using (
                ui.Box(
                    overflow: Overflow.Clip,
                    width: 100,
                    height: 50,
                    margin: new Thickness(20, 20, 0, 0)
                )
            )
            {
                using (
                    ui.Box(
                        width: 20,
                        height: 10,
                        shrink: 0,
                        margin: new Thickness(shift.Count, 0, 0, 0),
                        nodeRef: anchor
                    )
                ) { }
            }
            using (ui.Positioner(anchor: anchor, id: "held"))
            {
                using (ui.Box(width: 30, height: 30)) { }
            }
            using (
                ui.Positioner(
                    anchor: anchor,
                    dismissOn: Dismiss.AnchorHidden,
                    onDismiss: Callback.Of(dismissals, static c => c.Value++)
                )
            )
            {
                using (ui.Box(width: 30, height: 30)) { }
            }
        });

        shift.Count = 40;
        driver.Settle();
        await Assert.That(driver.Get("held").ViewBounds).IsEqualTo(new Rect(60, 30, 30, 30));
        await Assert.That(dismissals.Value).IsEqualTo(0);

        shift.Count = 200;
        driver.Settle();
        await Assert.That(driver.Get("held").ViewBounds).IsEqualTo(new Rect(60, 30, 30, 30));
        await Assert.That(dismissals.Value).IsEqualTo(1);
    }

    [Test]
    public async Task Focus_asked_for_outside_an_open_layer_waits_and_takes_over_as_it_closes()
    {
        var open = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (ui.Box(focusable: true, width: 20, height: 20, id: "field")) { }
                using (ui.Box(focusable: true, width: 20, height: 20, id: "other")) { }
            }
            if (open.A)
            {
                using (ui.Layer())
                {
                    using (ui.Box(focusable: true, width: 20, height: 20, id: "inside")) { }
                }
            }
        });
        driver.Focus(driver.Get("field"));
        open.A = true;
        driver.Settle();
        driver.Focus(driver.Get("inside"));

        await Assert.That(driver.Focus(driver.Get("other"))).IsTrue();
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("inside"));

        open.A = false;
        driver.Settle();
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("other"));
    }

    [Test]
    public async Task A_layer_closing_after_its_opener_left_gives_focus_to_the_openers_nearest_focusable_ancestor()
    {
        var shown = new Switches { A = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(focusable: true, padding: 10, id: "opener-frame"))
            {
                if (shown.A)
                {
                    using (ui.Box(focusable: true, width: 20, height: 20, id: "opener")) { }
                }
            }
            using (ui.Box(focusable: true, padding: 10, id: "layer-frame"))
            {
                if (shown.B)
                {
                    using (ui.Layer())
                    {
                        using (ui.Box(focusable: true, width: 20, height: 20, id: "inside")) { }
                    }
                }
            }
        });
        driver.Focus(driver.Get("opener"));
        shown.B = true;
        driver.Settle();
        driver.Focus(driver.Get("inside"));

        shown.A = false;
        shown.B = false;
        driver.Settle();

        await Assert.That(driver.Focused).IsEqualTo(driver.Get("opener-frame"));
    }

    [Test]
    public async Task A_layer_opened_with_nothing_focused_closes_to_nothing_focused_and_one_focus_left_closes_leaving_it_there()
    {
        var open = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (ui.Box(focusable: true, width: 20, height: 20, id: "field")) { }
                using (ui.Box(focusable: true, width: 20, height: 20, id: "other")) { }
            }
            if (open.A)
            {
                using (ui.Layer(restoreFocus: true))
                {
                    using (
                        ui.Box(
                            focusable: true,
                            width: 20,
                            height: 20,
                            margin: new Thickness(200, 200, 0, 0),
                            id: "inside"
                        )
                    ) { }
                }
            }
        });

        open.A = true;
        driver.Settle();
        driver.Focus(driver.Get("inside"));
        open.A = false;
        driver.Settle();
        var unfocused = driver.Focused;

        driver.Focus(driver.Get("field"));
        open.A = true;
        driver.Settle();
        driver.Focus(driver.Get("inside"));
        driver.Click(driver.Get("other"));
        open.A = false;
        driver.Settle();

        await Assert.That(unfocused).IsNull();
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("other"));
    }

    [Test]
    public async Task A_layer_reopened_by_what_focus_returns_to_as_it_closes_ends_up_open_and_takes_a_press()
    {
        var open = new Switches();
        var presses = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    focusable: true,
                    width: 40,
                    height: 20,
                    onFocusChange: Callback<FocusChange>.Of(
                        open,
                        static (s, change) =>
                        {
                            if (change.Focused)
                                s.A = true;
                        }
                    ),
                    id: "field"
                )
            )
            {
                if (open.A)
                {
                    using (ui.Layer(restoreFocus: true))
                        ui.Fader(onClick: Callback.Of(presses, static c => c.Value++), id: "panel");
                }
            }
        });
        driver.Focus(driver.Get("field"));
        driver.Focus(driver.Get("panel"));

        open.A = false;
        driver.Settle();
        var reopened = (open.A, driver.View.Layers.Length, driver.Focused == driver.Get("field"));
        driver.Click(driver.Get("panel"));

        await Assert.That(reopened).IsEqualTo((true, 1, true));
        await Assert.That(presses.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_positioner_placed_left_of_its_anchor_keeps_its_gap_on_the_left()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(position: Position.Absolute, left: 100, top: 100))
            {
                using (ui.Box(width: 20, height: 20)) { }
                using (ui.Positioner(placement: PlacementMode.LeftStart, sideOffset: 8, id: "left"))
                {
                    using (ui.Box(width: 40, height: 40)) { }
                }
            }
        });

        await Assert.That(driver.Get("left").ViewBounds).IsEqualTo(new Rect(52, 100, 40, 40));
    }

    [Test]
    public async Task An_active_modal_takes_presses_within_its_bounds_and_focus_from_outside_until_it_deactivates()
    {
        var modal = new Switches();
        var beneath = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (ui.Box(focusable: true, width: 40, height: 20, id: "field")) { }
                using (ui.Box(width: 200, height: 100))
                {
                    using (
                        ui.Box(
                            position: Position.Absolute,
                            left: 0,
                            top: 0,
                            width: 50,
                            height: 30,
                            onClick: Callback.Of(beneath, static c => c.Value++),
                            id: "beneath"
                        )
                    ) { }
                    using (
                        ui.Box(
                            position: Position.Absolute,
                            left: 0,
                            top: 0,
                            right: 0,
                            bottom: 0,
                            modal: modal.A
                        )
                    )
                    {
                        using (
                            ui.Box(
                                focusable: true,
                                width: 20,
                                height: 20,
                                margin: new Thickness(100, 50, 0, 0),
                                id: "first"
                            )
                        ) { }
                    }
                }
            }
        });
        var under = Centre(driver.Get("beneath"));
        driver.Focus(driver.Get("field"));

        modal.A = true;
        driver.Frame();
        await Assert.That(driver.Focused).IsNull();
        driver.Settle();
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("first"));

        driver.Click(under);
        await Assert.That(beneath.Value).IsEqualTo(0);
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("first"));

        modal.A = false;
        driver.Settle();
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("field"));
    }

    [Test]
    public async Task Focus_leaving_both_the_owner_and_the_layer_dismisses_a_layer_set_to()
    {
        var open = new Switches { A = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (ui.Box())
                {
                    using (ui.Box(focusable: true, width: 20, height: 20, id: "trigger")) { }
                    if (open.A)
                    {
                        using (
                            ui.Layer(
                                dismissOn: Dismiss.FocusOutside,
                                onDismiss: Callback.Of(open, static s => s.A = false)
                            )
                        )
                        {
                            using (
                                ui.Box(
                                    focusable: true,
                                    width: 20,
                                    height: 20,
                                    margin: new Thickness(0, 100, 0, 0),
                                    id: "inside"
                                )
                            ) { }
                        }
                    }
                }
                using (ui.Box(focusable: true, width: 20, height: 20, id: "elsewhere")) { }
            }
        });
        driver.Focus(driver.Get("trigger"));

        driver.Click(driver.Get("inside"));
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);

        driver.Click(driver.Get("elsewhere"));
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Keys_with_nothing_focused_go_to_the_newest_modal()
    {
        var heard = new List<string>();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    onKeyDown: Handler<KeyEvent>.Of(
                        heard,
                        static (List<string> h, ref KeyEvent e) => h.Add("page")
                    )
                )
            )
            {
                using (
                    ui.Box(
                        modal: true,
                        width: 50,
                        height: 50,
                        onKeyDown: Handler<KeyEvent>.Of(
                            heard,
                            static (List<string> h, ref KeyEvent e) => h.Add("modal")
                        )
                    )
                ) { }
            }
        });

        driver.Press(Key.A);

        await Assert.That(heard).IsEquivalentTo(["modal", "page"]);
    }

    static Vector2 Centre(Node node)
    {
        var bounds = node.ViewBounds;
        return new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
    }
}

public sealed partial class Switches : ObservableObject
{
    [ObservableProperty]
    public partial bool A { get; set; }

    [ObservableProperty]
    public partial bool B { get; set; }

    [ObservableProperty]
    public partial bool C { get; set; }
}
