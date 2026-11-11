using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Tests;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components.Tests;

public sealed partial class OverlayModalTests
{
    [Test]
    public async Task An_overlay_centres_its_panel_in_the_room_its_padding_leaves()
    {
        using var driver = UiDriver.Render(
            ui =>
            {
                using (
                    ui.Overlay(
                        open: true,
                        padding: new Thickness(100f, 0f, 0f, 0f),
                        position: Position.Absolute,
                        left: 0,
                        top: 0,
                        right: 0,
                        bottom: 0
                    )
                )
                using (ui.Box(width: 100, height: 50, id: "body")) { }
            },
            new Size(400, 300)
        );
        var body = driver.Get("body").ViewBounds;

        await Assert.That(body.X + body.Width / 2f).IsEqualTo(250f);
    }

    [Test]
    public async Task An_open_overlay_holds_the_focus_in_its_panel_while_a_field_beneath_asks_for_it_and_hands_it_over_as_it_closes()
    {
        var model = new OverlayModel { Open = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 300, height: 200))
            {
                if (model.Field)
                    using (
                        ui.Box(focusable: true, autoFocus: true, width: 40, height: 20, id: "field")
                    ) { }
                using (
                    ui.Overlay(
                        open: model.Open,
                        position: Position.Absolute,
                        left: 0,
                        top: 0,
                        right: 0,
                        bottom: 0
                    )
                )
                    ui.Text("Saved");
            }
        });
        var panel = Panel(driver);
        var inPanel = driver.Focused == panel;

        model.Field = true;
        driver.Settle();
        var held = driver.Focused == panel;
        model.Open = false;
        driver.Settle();

        await Assert.That(inPanel).IsTrue();
        await Assert.That(held).IsTrue();
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("field"));
    }

    [Test]
    public async Task An_open_overlay_takes_enter_and_escape_alone_closes_it_while_a_modified_escape_or_one_with_nothing_to_close_reaches_whats_behind()
    {
        var model = new OverlayModel { Open = true, Closable = true };
        var reports = new Reports();
        var behind = new List<Key>();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 300,
                    height: 200,
                    onKeyDown: Handler<KeyEvent>.Of(
                        behind,
                        static (List<Key> b, ref KeyEvent e) => b.Add(e.Key)
                    )
                )
            )
            using (
                ui.Overlay(
                    open: model.Open,
                    onOpenChange: model.Closable ? reports.OnOpenChange : default,
                    position: Position.Absolute,
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0
                )
            )
                ui.Text("Saved");
        });

        var enter = driver.KeyDown(Key.Enter);
        var escape = driver.KeyDown(Key.Escape);
        driver.KeyDown(Key.Escape, ModifierKeys.Shift);
        var closes = reports.Opens.Count;
        model.Closable = false;
        driver.Settle();
        var passed = driver.KeyDown(Key.Escape);

        await Assert.That((enter, escape, passed)).IsEqualTo((true, true, false));
        await Assert.That(closes).IsEqualTo(1);
        await Assert.That(reports.Opens).IsEquivalentTo([false]);
        await Assert.That(behind).IsEquivalentTo([Key.Escape, Key.Escape]);
    }

    [Test]
    public async Task Arrows_in_an_open_overlays_panel_move_among_its_stops_and_stop_at_its_edges()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, width: 300, height: 300))
            {
                using (ui.Box(focusable: true, width: 40, height: 20, id: "outside")) { }
                using (
                    ui.Overlay(
                        open: true,
                        position: Position.Absolute,
                        left: 0,
                        top: 40,
                        right: 0,
                        bottom: 0
                    )
                )
                using (ui.Box(gap: 8, padding: 8))
                {
                    using (ui.Box(focusable: true, width: 40, height: 20, id: "left")) { }
                    using (ui.Box(focusable: true, width: 40, height: 20, id: "right")) { }
                }
            }
        });
        driver.Focus(driver.Get("left"));

        driver.Press(Key.Right);
        var moved = driver.Focused == driver.Get("right");
        driver.Press(Key.Right);
        var atEdge = driver.Focused == driver.Get("right");
        driver.Press(Key.Up);

        await Assert.That(moved).IsTrue();
        await Assert.That(atEdge).IsTrue();
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("right"));
    }

    [Test]
    public async Task A_left_press_on_the_scrim_of_an_overlay_that_closes_on_one_closes_it_but_one_on_its_panel_a_right_press_or_one_on_an_overlay_that_doesnt_reports_nothing()
    {
        var model = new OverlayModel { Open = true, Scrim = true };
        var reports = new Reports();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 400, height: 200, id: "host"))
            using (
                ui.Overlay(
                    open: model.Open,
                    onOpenChange: reports.OnOpenChange,
                    closesOnScrimClick: model.Scrim,
                    position: Position.Absolute,
                    left: 0,
                    top: 0,
                    right: 100,
                    bottom: 0
                )
            )
            using (ui.Box(width: 60, height: 40)) { }
        });
        var scrim = driver.Get("host");

        driver.Click(Panel(driver));
        var onPanel = reports.Opens.Count;
        driver.Click(scrim, MouseButton.Right);
        var right = reports.Opens.Count;
        driver.Click(scrim);
        var left = reports.Opens.Count;
        model.Scrim = false;
        driver.Settle();
        driver.Click(scrim);

        await Assert.That((onPanel, right, left)).IsEqualTo((0, 0, 1));
        await Assert.That(reports.Opens).IsEquivalentTo([false]);
    }

    [Test]
    public async Task A_closed_overlay_draws_nothing_and_lets_a_press_through_while_an_open_one_scrims_its_area_and_centres_its_panel_16_short_of_a_small_hosts_edges()
    {
        var model = new OverlayModel();
        var beneath = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 300, height: model.Tall ? 100 : 200))
            {
                using (
                    ui.Box(
                        width: 300,
                        height: 200,
                        onClick: Callback.Of(beneath, static c => c.Value++),
                        id: "beneath"
                    )
                ) { }
                using (
                    ui.Overlay(
                        open: model.Open,
                        position: Position.Absolute,
                        left: 0,
                        top: 0,
                        right: 0,
                        bottom: 0
                    )
                )
                using (ui.Box(width: 100, height: 120)) { }
            }
        });
        var t = Themes.Dark;

        driver.Click(driver.Get("beneath"));
        var through = (beneath.Value, driver.Get<Overlay>().Children.GetEnumerator().MoveNext());
        model.Open = true;
        driver.Settle();
        var scrim = driver.RootOf<Overlay>();
        var panel = Panel(driver);
        var centred = (scrim.ViewBounds, panel.ViewBounds);
        var looks = (
            scrim.Visual.Background,
            panel.Visual.Background,
            panel.Layout.Border,
            panel.Visual.BorderPaint,
            panel.Visual.Radius
        );
        model.Tall = true;
        driver.Settle();

        await Assert.That(through).IsEqualTo((1, false));
        await Assert
            .That(centred)
            .IsEqualTo((new Rect(0, 0, 300, 200), new Rect(99, 39, 102, 122)));
        await Assert
            .That(looks)
            .IsEqualTo(
                (
                    (Paint)t.Color(ColorToken.BgScrim),
                    (Paint)t.Color(ColorToken.Bg),
                    new Thickness(1f),
                    (Paint)t.Color(ColorToken.Border),
                    t.Dimensions(Density.Pointer).Radius(RadiusToken.Md)
                )
            );
        await Assert.That(scrim.ViewBounds).IsEqualTo(new Rect(0, 0, 300, 100));
        await Assert.That(Panel(driver).ViewBounds).IsEqualTo(new Rect(99, 16, 102, 68));
    }

    [Test]
    public async Task An_overlay_that_can_close_shows_a_corner_close_button_that_reports_its_close()
    {
        var model = new OverlayModel { Closable = true };
        var reports = new Reports();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (
                    ui.Overlay(
                        open: true,
                        onOpenChange: model.Closable ? reports.OnOpenChange : default
                    )
                )
                using (ui.Box(width: 60, height: 40)) { }
            })
            .WithIcons();

        driver.Click(driver.RootOf<CloseButton>());
        model.Closable = false;
        driver.Settle();

        await Assert.That(reports.Opens).IsEquivalentTo([false]);
        await Assert.That(driver.All<CloseButton>()).IsEmpty();
    }

    static Node Panel(UiDriver driver) =>
        OverlayProbes.FirstOf(
            driver.Get<Overlay>(),
            static n => n.Kind == NodeKind.Box && n.Layout.Border.Left > 0f
        ) ?? throw new InvalidOperationException("No panel.");

    sealed partial class OverlayModel : ObservableObject
    {
        [ObservableProperty]
        public partial bool Open { get; set; }

        [ObservableProperty]
        public partial bool Closable { get; set; }

        [ObservableProperty]
        public partial bool Scrim { get; set; }

        [ObservableProperty]
        public partial bool Field { get; set; }

        [ObservableProperty]
        public partial bool Tall { get; set; }
    }
}
