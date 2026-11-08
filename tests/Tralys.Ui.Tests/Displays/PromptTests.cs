using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class PromptTests
{
    static readonly Theme Dark = Themes.Dark;

    [Test]
    public async Task An_input_glyph_follows_the_device_in_hand_the_pads_style_and_a_rebinding_and_shows_nothing_after_a_touch()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                using (ui.Box(onClick: Callback.Of(clicks, static c => c.Value++), id: "area"))
                    ui.InputGlyph(action: UiAction.Back, id: "back");
                using (ui.Box(width: 40, height: 40, id: "pad")) { }
            }
        });
        string Shown() =>
            driver.All<Kbd>().Count > 0
                ? Nodes.Find(driver.All<Kbd>()[0], NodeKind.Text).Text.ToString()
            : driver.All<GamepadGlyph>().Count > 0 ? "pad " + Label(driver.All<GamepadGlyph>()[0])
            : "";

        var keyboard = Shown();
        driver.Click(driver.Get("back"));
        driver.Press(GamepadButton.DPadDown);
        var xbox = Shown();
        driver.View.GamepadStyle = GamepadStyle.PlayStation;
        driver.Settle();
        var playStation = Shown();
        driver.View.Context.Actions[UiAction.Back] = new(Key.Q, GamepadButton.North);
        driver.Press(Key.Down);
        var rebound = Shown();
        driver.Tap(driver.Get("pad"));
        var touched = (Shown(), driver.Get("area").Bounds.Size);
        driver.Press(Key.Down);

        await Assert
            .That((keyboard, xbox, playStation, rebound))
            .IsEqualTo(("Esc", "pad B", "pad ", "Q"));
        await Assert.That(touched).IsEqualTo(("", default(Size)));
        await Assert.That(Shown()).IsEqualTo("Q");
        await Assert.That(clicks.Value).IsEqualTo(1);
    }

    [Test]
    public async Task An_action_with_no_binding_on_the_device_in_hand_or_no_action_shows_no_glyph()
    {
        var dodge = new UiAction("Dodge");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.InputGlyph(action: dodge);
                ui.InputGlyph();
            }
        });
        driver.View.Context.Actions[dodge] = new(Key.D, null);
        driver.Settle();
        var keyboard = driver.All<Kbd>().Count;
        driver.Press(GamepadButton.DPadDown);

        await Assert.That(keyboard).IsEqualTo(1);
        await Assert
            .That((driver.All<Kbd>().Count, driver.All<GamepadGlyph>().Count))
            .IsEqualTo((0, 0));
    }

    [Test]
    public async Task A_gamepad_glyph_prints_each_button_as_its_pads_style_does()
    {
        (GamepadButton, GamepadStyle)[] glyphs =
        [
            (GamepadButton.South, GamepadStyle.Xbox),
            (GamepadButton.North, GamepadStyle.Generic),
            (GamepadButton.South, GamepadStyle.Nintendo),
            (GamepadButton.East, GamepadStyle.Nintendo),
            (GamepadButton.Back, GamepadStyle.Nintendo),
            (GamepadButton.RightTrigger, GamepadStyle.Nintendo),
            (GamepadButton.South, GamepadStyle.PlayStation),
            (GamepadButton.LeftShoulder, GamepadStyle.PlayStation),
            (GamepadButton.Start, GamepadStyle.PlayStation),
            (GamepadButton.LeftTrigger, GamepadStyle.Xbox),
            (GamepadButton.DPadLeft, GamepadStyle.Xbox),
        ];
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 4))
                for (var i = 0; i < glyphs.Length; i++)
                    ui.GamepadGlyph(button: glyphs[i].Item1, style: glyphs[i].Item2, key: i);
        });
        var all = driver.All<GamepadGlyph>();
        var labels = string.Join(" ", all.Select(Label));

        await Assert.That(labels).IsEqualTo("A Y B A - ZR  L1 Options LT ");
    }

    [Test]
    public async Task A_gamepad_glyph_is_a_sunken_disc_or_pill_at_least_22_square_with_a_stroked_shape_or_a_filled_arrow_and_passes_clicks_through()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    align: Align.Start,
                    gap: 4,
                    onClick: Callback.Of(clicks, static c => c.Value++)
                )
            )
            {
                ui.GamepadGlyph(
                    button: GamepadButton.South,
                    style: GamepadStyle.PlayStation,
                    id: "cross"
                );
                ui.GamepadGlyph(button: GamepadButton.DPadUp, id: "up");
                ui.GamepadGlyph(button: GamepadButton.LeftShoulder, id: "lb");
            }
        });
        var lines = driver.Draw().Lines;
        string Of(string id) =>
            string.Join(
                "\n",
                lines.Where(l => l.Contains($"GamepadGlyph[{Index(id)}]", StringComparison.Ordinal))
            );
        static int Index(string id) =>
            id switch
            {
                "cross" => 0,
                "up" => 1,
                _ => 2,
            };
        var face = Hex(Dark.Color(ColorToken.BgSunken));
        var ink = Hex(Dark.Color(ColorToken.FgStrong));
        var lb = driver.Get("lb");
        driver.Click(driver.Get("up"));

        await Assert.That(driver.Get("cross").Bounds.Size).IsEqualTo(new Size(22f, 22f));
        await Assert.That(lb.Bounds.Width > lb.Bounds.Height && lb.Bounds.Height >= 22f).IsTrue();
        await Assert
            .That(Of("cross"))
            .Contains("ellipse")
            .And.Contains(face)
            .And.Contains("stroke");
        await Assert.That(Of("up")).Contains("ellipse").And.Contains($"fill {ink}");
        await Assert
            .That(Of("lb"))
            .Contains($"radius {Num(lb.Bounds.Height / 2f)}")
            .And.DoesNotContain("ellipse");
        await Assert.That(clicks.Value).IsEqualTo(1);
    }

    [Test]
    public async Task An_action_bar_shows_the_shown_actions_of_the_newest_scope_in_order_against_its_end_edge_and_follows_their_changes()
    {
        var bar = new BarModel();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, width: 600))
            {
                if (bar.Town)
                    using (ui.ActionScope())
                        ui.Action(UiAction.Back, onRun: default, label: "Leave town");
                using (ui.ActionScope(active: bar.Shop))
                {
                    ui.Action(UiAction.Confirm, onRun: default, label: bar.Buy);
                    ui.Action(UiAction.Back, onRun: default, label: "Close shop");
                    ui.Action(UiAction.NextTab, onRun: default, hidden: bar.HideNext);
                    if (bar.Inspect)
                        ui.Action(new UiAction("Inspect"), onRun: default);
                    if (bar.Unnamed)
                        ui.Action(default, onRun: default, label: "Nothing");
                    ui.Button(text: "Sell", action: new UiAction("Sell"));
                    ui.ActionBar(id: "bar");
                }
            }
        });
        string Labels() => string.Join(", ", driver.All<ActionPrompt>().Select(LastText));

        var none = Labels();
        bar.Town = true;
        bar.Shop = true;
        bar.Unnamed = true;
        driver.Rerender();
        var shop = Labels();
        var prompts = driver.All<ActionPrompt>().Select(First).ToArray();
        var edge = (
            driver.Get("bar").ViewBounds.Right - prompts[^1].ViewBounds.Right,
            prompts[1].ViewBounds.X - prompts[0].ViewBounds.Right
        );
        bar.Buy = "Buy all";
        bar.HideNext = false;
        bar.Inspect = true;
        driver.Rerender();
        var changed = Labels();
        bar.Shop = false;
        driver.Rerender();

        await Assert.That(none).IsEqualTo("");
        await Assert.That(shop).IsEqualTo("Buy, Close shop");
        await Assert.That(edge).IsEqualTo((0f, 24f));
        await Assert.That(changed).IsEqualTo("Buy all, Close shop, NextTab, Inspect");
        await Assert.That(Labels()).IsEqualTo("Leave town");
    }

    [Test]
    public async Task A_prompt_runs_its_action_once_when_clicked_or_tapped_nothing_when_dragged_off_and_is_never_a_tab_stop()
    {
        var log = new Log();
        var refused = new Command(() => log.Add("refused"), () => false);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.ActionScope())
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Action(
                    UiAction.Confirm,
                    onRun: Callback.Of(log, static l => l.Add("confirm")),
                    label: "Buy"
                );
                ui.Action(UiAction.Back, onRun: refused, label: "Leave");
                ui.ActionPrompt(action: UiAction.Confirm, label: "Buy", id: "buy");
                ui.ActionPrompt(action: UiAction.Back, label: "Leave", id: "leave");
                using (ui.Box(width: 40, height: 40, id: "away")) { }
                ui.Button(text: "Focus me", id: "button");
            }
        });
        var buy = driver.Get("buy");
        var label = driver.GetText("Buy");
        var glyph = First(driver.All<Kbd>()[0]);

        driver.Click(buy);
        driver.Tap(buy);
        driver.Click(driver.Get("leave"));
        var strayed = driver.Hold(buy);
        strayed.MoveTo(driver.Get("away"));
        strayed.Release();
        driver.Hover(buy);
        var hovered = (Nodes.Find(buy, NodeKind.Text).Env.Color, driver.Cursor);
        driver.Unhover();
        driver.Tab();
        driver.Tab();

        await Assert.That(log.ToString()).IsEqualTo("confirm confirm");
        await Assert.That(label.ViewBounds.X - glyph.ViewBounds.Right).IsEqualTo(8f);
        await Assert
            .That(hovered)
            .IsEqualTo(((Paint)Dark.Color(ColorToken.FgStrong), (Cursor?)Cursors.Hand));
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("button"));
    }

    static Node First(Node node)
    {
        foreach (var child in node.Children)
            return child;
        throw new InvalidOperationException("No child.");
    }

    static string Label(Node glyph)
    {
        foreach (var child in glyph.Children)
        foreach (var part in child.Children)
            if (part.Kind == NodeKind.Text)
                return part.Text.ToString();
        return "";
    }

    static string LastText(Node node)
    {
        var last = "";
        foreach (var child in node.Children)
        {
            if (child.Kind == NodeKind.Text)
                last = child.Text.ToString();
            else if (LastText(child) is { Length: > 0 } inner)
                last = inner;
        }
        return last;
    }

    static string Hex(Color c) =>
        $"#{c.R:x2}{c.G:x2}{c.B:x2}{(c.A == 255 ? "" : c.A.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))}";

    static string Num(float v) =>
        v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    sealed class BarModel
    {
        public bool Town;
        public bool Shop;
        public bool Unnamed;
        public bool HideNext = true;
        public bool Inspect;
        public string Buy = "Buy";
    }
}
