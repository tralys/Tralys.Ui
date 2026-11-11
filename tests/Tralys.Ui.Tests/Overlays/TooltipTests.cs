using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Tests;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components.Tests;

public sealed partial class TooltipTests
{
    const float Wait = 0.35f;

    static string? Shown(UiDriver driver)
    {
        var layers = driver.View.Layers;
        if (layers.Length != 1)
            return layers.Length == 0 ? null : "several";
        return OverlayProbes.FirstOf(layers[0], static n => n.Kind == NodeKind.Text) is { } text
            ? text.Text.ToString()
            : "";
    }

    static readonly Action<Ui> Owners = static ui =>
    {
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8, padding: 60))
        {
            using (ui.Tooltip(text: "Saves the file"))
                ui.Button(text: "Save", id: "save");
            using (ui.Tooltip(text: "Opens a file"))
                ui.Button(text: "Load", id: "load");
            using (ui.Box(width: 40, height: 20, id: "blank")) { }
        }
    };

    [Test]
    public async Task A_tip_anchored_on_a_link_opens_over_it_while_the_link_is_hovered()
    {
        var link = new NodeRef();
        using var driver = UiDriver.Render(ui =>
        {
            var hovered = ui.State(false);
            using (ui.Box(direction: Direction.Column, align: Align.Start, padding: 60))
            {
                using (ui.Text())
                {
                    ui.Span("Sold ");
                    ui.Link("[Blade]", onHoverChange: hovered.Setter, nodeRef: link, id: "link");
                }
                using (ui.Tooltip(text: "A blade", open: hovered.Value, anchor: link)) { }
            }
        });

        var closed = OverlayProbes.ShownTip(driver);
        driver.Hover(driver.Get("link"));
        var tip = driver.View.Layers[0].ViewBounds;

        await Assert
            .That((closed, OverlayProbes.ShownTip(driver)))
            .IsEqualTo(((string?)null, "A blade"));
        await Assert.That(tip.Bottom).IsLessThanOrEqualTo(driver.Get("link").ViewBounds.Y);
    }

    [Test]
    public async Task A_tip_shows_once_the_mouse_rests_its_delay_hops_to_the_next_trigger_at_once_and_hides_as_it_leaves_and_within_half_a_second_the_next_shows_at_once_but_not_later()
    {
        using var driver = UiDriver.Render(Owners);
        var seen = new List<string?>();
        void Step(string to, float seconds)
        {
            driver.Hover(driver.Get(to), settle: false);
            driver.Frame(seconds);
            seen.Add(Shown(driver));
        }

        Step("save", 1f / 60f);
        driver.Frame(Wait);
        seen.Add(Shown(driver));
        Step("load", 1f / 60f);
        Step("blank", 1f / 60f);
        Step("save", 0.1f);
        Step("blank", 0.6f);
        Step("load", 1f / 60f);
        driver.Frame(Wait);
        seen.Add(Shown(driver));

        await Assert
            .That(seen)
            .IsEquivalentTo([
                null,
                "Saves the file",
                "Opens a file",
                null,
                "Saves the file",
                null,
                null,
                "Opens a file",
            ]);
    }

    [Test]
    public async Task Sweeping_onto_another_trigger_before_any_tip_shows_waits_the_whole_delay_from_there()
    {
        using var driver = UiDriver.Render(Owners);

        driver.Hover(driver.Get("save"), settle: false);
        driver.Frame(0.1f);
        driver.Hover(driver.Get("load"), settle: false);
        driver.Frame(0.1f);
        var early = Shown(driver);
        driver.Frame(0.1f);
        var still = Shown(driver);
        driver.Frame(0.15f);

        await Assert.That((early, still)).IsEqualTo(((string?)null, (string?)null));
        await Assert.That(Shown(driver)).IsEqualTo("Opens a file");
    }

    [Test]
    public async Task An_inner_trigger_takes_the_tip_from_its_outer_one_at_once_and_gives_it_back_as_the_pointer_leaves_unless_the_outer_has_nothing_to_say()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(gap: 20, align: Align.Start, padding: 60))
            {
                using (ui.Tooltip(text: "The panel's tooltip"))
                using (Panel(ui, "panel"))
                {
                    using (ui.Tooltip(text: "The button's own tooltip"))
                        ui.Button(text: "Inner", id: "inner");
                    using (ui.Tooltip(text: " "))
                    using (
                        ui.Box(
                            width: 60,
                            height: 20,
                            background: Color.FromRgb(90, 90, 90),
                            id: "blank"
                        )
                    ) { }
                }
                using (ui.Tooltip(text: " "))
                using (Panel(ui, "bare"))
                using (ui.Tooltip(text: "Second inner"))
                    ui.Button(text: "Second", id: "second");
            }
        });
        var seen = new List<string?>();
        void Step(string to, float seconds)
        {
            driver.Hover(driver.Get(to), settle: false);
            driver.Frame(seconds);
            seen.Add(Shown(driver));
        }

        Step("panel", Wait);
        Step("inner", 1f / 60f);
        Step("panel", 1f / 60f);
        Step("blank", 1f / 60f);
        Step("second", Wait);
        Step("bare", 1f / 60f);

        await Assert
            .That(seen)
            .IsEquivalentTo([
                "The panel's tooltip",
                "The button's own tooltip",
                "The panel's tooltip",
                "The panel's tooltip",
                "Second inner",
                null,
            ]);
    }

    [FramePath]
    static Scope Panel(
        Ui ui,
        string id,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Box(
            gap: 8,
            width: 300,
            height: 120,
            padding: 8,
            background: Color.FromRgb(40, 60, 60),
            id: id,
            key: key,
            file: file,
            line: line
        );

    [Test]
    public async Task A_shown_tip_takes_no_press_so_what_it_covers_takes_the_click()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, padding: 60))
            {
                using (
                    ui.Box(
                        width: 200,
                        height: 30,
                        onClick: Callback.Of(clicks, static c => c.Value++),
                        id: "covered"
                    )
                ) { }
                using (
                    ui.Tooltip(
                        text: "A tip wide and tall enough to cover what sits above its trigger"
                    )
                )
                    ui.Button(text: "Owner", id: "owner");
            }
        });
        driver.Hover(driver.Get("owner"), settle: false);
        driver.Frame(Wait);
        var shown = Shown(driver);

        driver.Click(driver.Get("covered"));

        await Assert.That(shown).IsNotNull();
        await Assert.That(clicks.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_fingers_tip_waits_out_the_delay_keeps_through_a_nudge_and_a_second_finger_and_goes_as_it_strays_past_a_tap_lifts_or_is_lost()
    {
        using var driver = UiDriver.Render(Owners);
        var seen = new List<string?>();
        void Look(float seconds)
        {
            driver.Frame(seconds);
            seen.Add(Shown(driver));
        }

        var finger = driver.Hold(driver.Get("save"), PointerKind.Touch, settle: false);
        Look(1f / 60f);
        Look(Wait);
        finger.MoveBy(new Vector2(4f, 0f), settle: false);
        Look(1f / 60f);
        var second = driver.Hold(driver.Get("load"), PointerKind.Touch, settle: false);
        Look(1f / 60f);
        finger.MoveBy(new Vector2(20f, 0f), settle: false);
        Look(1f / 60f);
        second.Release(settle: false);
        finger.Release(settle: false);
        var again = driver.Hold(driver.Get("save"), PointerKind.Touch, settle: false);
        Look(Wait);
        again.Release(settle: false);
        Look(1f / 60f);
        var lost = driver.Hold(driver.Get("save"), PointerKind.Touch, settle: false);
        Look(Wait);
        lost.LoseRelease(settle: false);
        Look(1f / 60f);

        await Assert
            .That(seen)
            .IsEquivalentTo([
                null,
                "Saves the file",
                "Saves the file",
                "Saves the file",
                null,
                "Saves the file",
                null,
                "Saves the file",
                null,
            ]);
    }

    [Test]
    public async Task A_finger_held_on_a_trigger_inside_another_shows_the_inner_tip()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Tooltip(text: "The panel's tooltip"))
            using (Panel(ui, "panel"))
            using (ui.Tooltip(text: "Sends the mail"))
                ui.Button(text: "Send", id: "send");
        });

        driver.Hold(driver.Get("send"), PointerKind.Touch, settle: false);
        driver.Frame(Wait);

        await Assert.That(Shown(driver)).IsEqualTo("Sends the mail");
    }

    [Test]
    public async Task A_disabled_trigger_shows_its_tip_under_the_mouse_and_a_finger_panning_its_list_takes_its_tip()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 40, padding: 60))
            {
                using (ui.Tooltip(text: "Nothing to save"))
                    ui.Button(text: "Save", disabled: true, id: "save");
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        width: 140,
                        height: 100,
                        id: "list"
                    )
                )
                {
                    using (ui.Tooltip(text: "A row"))
                    using (
                        ui.Box(
                            width: 120,
                            height: 40,
                            background: Color.FromRgb(40, 60, 60),
                            id: "row"
                        )
                    ) { }
                    using (ui.Box(width: 120, height: 300)) { }
                }
            }
        });
        driver.Hover(driver.Get("save"), settle: false);
        driver.Frame(Wait);
        var disabled = Shown(driver);
        driver.Unhover(settle: false);
        driver.Frame(1f);

        var finger = driver.Hold(driver.Get("row"), PointerKind.Touch, settle: false);
        driver.Frame(Wait);
        var held = Shown(driver);
        finger.MoveBy(new Vector2(0f, -30f), steps: 3, settle: false);
        driver.Frame();

        await Assert
            .That((disabled, held))
            .IsEqualTo(((string?)"Nothing to save", (string?)"A row"));
        await Assert.That(Shown(driver)).IsNull();
    }

    [Test]
    public async Task A_tap_on_a_trigger_with_nothing_to_say_leaves_a_shown_tip_up()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8, padding: 60))
            {
                using (ui.Tooltip(text: "Saves the file"))
                    ui.Button(text: "Save", id: "save");
                using (ui.Tooltip(text: " "))
                    ui.Button(text: "Blank", id: "blank");
            }
        });
        driver.Hover(driver.Get("save"), settle: false);
        driver.Frame(Wait);

        driver.Tap(driver.Get("blank"), settle: false);
        driver.Frame();

        await Assert.That(Shown(driver)).IsEqualTo("Saves the file");
    }

    [Test]
    public async Task A_fingers_tip_goes_as_the_finger_slides_off_and_lifts_or_the_window_goes_inactive_and_the_next_finger_shows_it_again()
    {
        using var driver = UiDriver.Render(Owners);

        var slid = driver.Hold(driver.Get("save"), PointerKind.Touch, settle: false);
        driver.Frame(Wait);
        slid.MoveTo(driver.Get("blank"), settle: false);
        slid.Release(settle: false);
        driver.Frame();
        var afterSlide = Shown(driver);
        var held = driver.Hold(driver.Get("save"), PointerKind.Touch, settle: false);
        driver.Frame(Wait);
        driver.WindowFocus(false, settle: false);
        driver.WindowFocus(true, settle: false);
        driver.Frame();
        var afterInactive = Shown(driver);
        held.Release(settle: false);
        driver.Hold(driver.Get("save"), PointerKind.Touch, settle: false);
        driver.Frame(Wait);

        await Assert.That((afterSlide, afterInactive)).IsEqualTo(((string?)null, (string?)null));
        await Assert.That(Shown(driver)).IsEqualTo("Saves the file");
    }

    [Test]
    public async Task A_tip_changed_while_it_waits_shows_the_change_only_once_the_delay_is_out_and_one_cleared_while_it_shows_hides_at_once()
    {
        var model = new TipModel { Text = "Pause" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(padding: 60))
            using (ui.Tooltip(text: model.Text))
                ui.Button(text: "Play", id: "play");
        });
        driver.Hover(driver.Get("play"), settle: false);
        driver.Frame(0.1f);

        model.Text = "Stop";
        driver.Frame();
        var early = Shown(driver);
        driver.Frame(0.25f);
        var late = Shown(driver);
        model.Text = null;
        driver.Frame();

        await Assert.That((early, late)).IsEqualTo(((string?)null, (string?)"Stop"));
        await Assert.That(Shown(driver)).IsNull();
    }

    [Test]
    public async Task A_worded_tip_draws_its_rungs_strong_text_padded_as_text_and_wraps_at_its_measure_while_a_rich_one_is_padded_as_a_block_at_its_own_width_in_the_themes_foreground()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    direction: Direction.Column,
                    align: Align.Start,
                    gap: 8,
                    padding: 60,
                    color: Themes.Dark.Color(ColorToken.FgMuted)
                )
            )
            {
                using (ui.Tooltip(text: Long))
                    ui.Button(text: "Md", id: "md");
                using (ui.Tooltip(text: Long, size: ComponentSize.Sm))
                    ui.Button(text: "Sm", id: "sm");
                using (ui.Tooltip(content: Rich))
                    ui.Button(text: "Rich", id: "rich");
            }
        });
        var t = Themes.Dark;
        var sizes = t.Dimensions(Density.Pointer);

        var md = Show(driver, "md");
        var mdFrame = Frame(driver);
        var mdInk = (Text(driver).Inherit.Color, Text(driver).Inherit.FontSize);
        var mdText = Text(driver).ViewBounds;
        var sm = Show(driver, "sm");
        var smFrame = Frame(driver);
        var rich = Show(driver, "rich");
        var richFrame = Frame(driver);
        var richInk = Text(driver).Env.Color;
        var padding = sizes.Size(ComponentSize.Md).Padding;
        var inset = sizes.Size(ComponentSize.Md).Inset;

        await Assert.That((md, sm, rich)).IsEqualTo((true, true, true));
        await Assert
            .That(mdInk)
            .IsEqualTo(
                ((Paint)t.Color(ColorToken.FgStrong), sizes.Size(ComponentSize.Md).FontSize)
            );
        await Assert
            .That((mdText.X - mdFrame.X, mdText.Y - mdFrame.Y))
            .IsEqualTo((1f + padding.Left, 1f + padding.Top));
        await Assert.That(mdFrame.Width).IsEqualTo(sizes.Size(ComponentSize.Md).Measure);
        await Assert.That(smFrame.Width).IsEqualTo(sizes.Size(ComponentSize.Sm).Measure);
        await Assert.That(richFrame.Width).IsGreaterThanOrEqualTo(600f + 2f * (1f + inset.Left));
        await Assert.That((richFrame.X % 1f, richFrame.Width % 1f)).IsEqualTo((0f, 0f));
        await Assert.That(richInk).IsEqualTo((Paint)t.Color(ColorToken.Fg));
    }

    const string Long =
        "A tip long enough to wrap at any rung's measure, so its frame stands exactly as wide as that measure allows";

    static readonly Slot Rich = Slot.Of(static ui =>
    {
        using (ui.Box(direction: Direction.Column, width: 600.6f))
            ui.Text("Rich content");
    });

    static bool Show(UiDriver driver, string id)
    {
        driver.Unhover(settle: false);
        driver.Frame(1f);
        driver.Hover(driver.Get(id), settle: false);
        driver.Frame(Wait);
        return driver.View.Layers.Length == 1;
    }

    static Rect Frame(UiDriver driver) =>
        OverlayProbes
            .FirstOf(
                driver.View.Layers[0],
                static n => n.Kind == NodeKind.Box && n.Layout.Border.Left > 0f
            )!
            .ViewBounds;

    static Node Text(UiDriver driver) =>
        OverlayProbes.FirstOf(driver.View.Layers[0], static n => n.Kind == NodeKind.Text)!;

    [Test]
    public async Task A_tip_opens_above_its_trigger_4_off_and_below_when_there_is_no_room_above()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 120))
            {
                using (ui.Tooltip(text: "Top of the view"))
                    ui.Button(text: "High", id: "high");
                using (ui.Tooltip(text: "Room above"))
                    ui.Button(text: "Low", id: "low");
            }
        });

        Show(driver, "low");
        var above = (Frame(driver).Bottom, driver.Get("low").ViewBounds.Y);
        Show(driver, "high");
        var below = (Frame(driver).Y, driver.Get("high").ViewBounds.Bottom);

        await Assert.That(above.Item1 + 4f).IsEqualTo(above.Item2);
        await Assert.That(below.Item1).IsEqualTo(below.Item2 + 4f);
    }

    [Test]
    public async Task A_tip_held_open_from_code_goes_with_its_trigger_and_draws_nothing_while_its_trigger_is_hidden_or_collapsed()
    {
        var model = new TipModel
        {
            Text = "Synced",
            Shown = true,
            Open = true,
        };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, padding: 60))
            using (ui.Box(hidden: model.Hidden, collapsed: model.Collapsed))
                if (model.Shown)
                    using (ui.Tooltip(text: model.Text, open: model.Open))
                        ui.Button(text: "Cloud");
        });
        bool Drawn() => driver.Draw().Lines.Any(static line => line.Contains("/Layer"));
        var drawn = new List<bool> { Drawn() };

        model.Hidden = true;
        driver.Settle();
        drawn.Add(Drawn());
        model.Hidden = false;
        driver.Settle();
        drawn.Add(Drawn());
        model.Collapsed = true;
        driver.Settle();
        drawn.Add(Drawn());
        model.Collapsed = false;
        model.Shown = false;
        driver.Settle();

        await Assert.That(drawn).IsEquivalentTo([true, false, true, false]);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    [Test]
    public async Task A_tip_inside_a_popover_goes_as_the_popover_closes()
    {
        var open = new Switches { A = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Popover(open: open.A, content: InPopover))
                ui.Text("Filters");
        });
        driver.Hover(driver.Get("inside"), settle: false);
        driver.Frame(Wait);
        var both = driver.View.Layers.Length;

        open.A = false;
        driver.Frame();

        await Assert.That(both).IsEqualTo(2);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    static readonly Slot InPopover = Slot.Of(static ui =>
    {
        using (ui.Tooltip(text: "Only online contacts"))
            ui.Button(text: "Online", id: "inside");
    });

    [Test]
    public async Task A_trigger_whose_rich_tip_showed_its_data_can_be_collected_once_it_hides_and_leaves()
    {
        var model = new TipModel { Shown = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, padding: 60))
            {
                if (model.Shown)
                    Owner(ui, model);
                using (ui.Box(width: 40, height: 20, id: "blank")) { }
            }
        });
        var data = Track(model);
        driver.Hover(driver.Get("owner"), settle: false);
        driver.Frame(Wait);
        var shown = Shown(driver);
        driver.Hover(driver.Get("blank"), settle: false);
        driver.Frame();
        model.Shown = false;
        model.Data = null;
        driver.Settle();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Assert.That(shown).IsEqualTo("Ada's data");
        await Assert.That(data.TryGetTarget(out _)).IsFalse();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference<OwnerData> Track(TipModel model)
    {
        var data = new OwnerData { Name = "Ada's data" };
        model.Data = data;
        return new WeakReference<OwnerData>(data);
    }

    [FramePath]
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Owner(
        Ui ui,
        TipModel model,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Tooltip(
                content: Slot.Of(model.Data!, static (ui, d) => ui.Text(d.Name)),
                key: key,
                file: file,
                line: line
            )
        )
            ui.Button(text: "Ada", id: "owner");
    }

    [Test]
    public async Task Keyboard_focus_moving_inside_a_trigger_shows_its_tip_at_once_and_moving_out_hides_it()
    {
        using var driver = UiDriver.Render(Owners);

        driver.Tab();
        var first = Shown(driver);
        driver.Tab();
        var second = Shown(driver);
        driver.Tab();

        await Assert
            .That((first, second))
            .IsEqualTo(((string?)"Saves the file", (string?)"Opens a file"));
        await Assert.That(Shown(driver)).IsEqualTo("Saves the file");
    }

    [Test]
    public async Task Keyboard_focus_arriving_while_the_mouse_waits_out_the_delay_shows_the_tip_at_once()
    {
        using var driver = UiDriver.Render(Owners);
        driver.Hover(driver.Get("save"), settle: false);
        driver.Frame();
        var waiting = Shown(driver);

        driver.Press(Key.Tab, settle: false);
        driver.Frame();
        driver.Frame();

        await Assert
            .That((waiting, Shown(driver)))
            .IsEqualTo(((string?)null, (string?)"Saves the file"));
    }

    sealed partial class TipModel : ObservableObject
    {
        [ObservableProperty]
        public partial string? Text { get; set; }

        [ObservableProperty]
        public partial bool Shown { get; set; }

        [ObservableProperty]
        public partial bool? Open { get; set; }

        [ObservableProperty]
        public partial bool Hidden { get; set; }

        [ObservableProperty]
        public partial bool Collapsed { get; set; }

        [ObservableProperty]
        public partial OwnerData? Data { get; set; }
    }

    sealed class OwnerData
    {
        public string Name = "";
    }
}
