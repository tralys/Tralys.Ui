using System.Numerics;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class RichTextTests
{
    static TextPayload TextOf(Node node)
    {
        if (node.Payload is not TextPayload text)
            throw new InvalidOperationException("The node holds no text.");
        return text;
    }

    [Test]
    public async Task Spans_lay_out_as_one_paragraph_that_follows_their_text()
    {
        var model = new Model { Name = "world" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (ui.Text(id: "rich"))
                {
                    ui.Span("Hello ");
                    ui.Span(model.Name);
                }
                ui.Text($"Hello {model.Name}", id: "plain");
            }
        });
        var rich = driver.Get("rich");
        var plain = driver.Get("plain");
        await Assert.That(rich.Text.ToString()).IsEqualTo("Hello world");
        await Assert.That(rich.Bounds.Width).IsEqualTo(plain.Bounds.Width);

        model.Name = "there, everyone";
        driver.Settle();

        await Assert.That(rich.Text.ToString()).IsEqualTo("Hello there, everyone");
        await Assert.That(rich.Bounds.Width).IsEqualTo(plain.Bounds.Width);
    }

    [Test]
    public async Task A_link_takes_presses_on_its_own_glyphs_only_and_enter_activates_it()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Text(id: "text"))
            {
                ui.Span("Read the ");
                ui.Link("docs", onClick: Callback.Of(clicks, static c => c.Value++), id: "link");
                ui.Span(" now");
            }
        });
        var link = driver.Get("link");

        driver.Click(link);
        await Assert.That(clicks.Value).IsEqualTo(1);
        await Assert.That(driver.Focused).IsEqualTo(link);

        driver.Press(Key.Enter);
        await Assert.That(clicks.Value).IsEqualTo(2);

        var text = driver.Get("text");
        driver.Click(text, new Vector2(2f, text.ViewBounds.Height / 2f));
        await Assert.That(clicks.Value).IsEqualTo(2);
    }

    [Test]
    public async Task A_span_turned_into_a_link_in_place_takes_its_click_in_the_link_colour()
    {
        var model = new Model();
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Text(id: "text"))
            {
                ui.Span("Read the ");
                Word(ui, model.Remember, clicks);
            }
        });

        model.Remember = true;
        driver.Settle();
        var fill = TextOf(driver.Get("text")).Buffer.Runs[^1].Style.Fill;
        driver.Click(driver.Get("word"));

        await Assert.That(fill).IsEqualTo((Paint)Themes.Dark.Color(ColorToken.FgAccent));
        await Assert.That(clicks.Value).IsEqualTo(1);
    }

    [FramePath]
    static void Word(
        Ui ui,
        bool link,
        Counter clicks,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
    {
        using (ui.Fragment(file: file, line: line))
        {
            if (link)
                ui.Link(
                    "docs",
                    onClick: Callback.Of(clicks, static c => c.Value++),
                    id: "word",
                    file: file,
                    line: line
                );
            else
                ui.Span("docs", file: file, line: line);
        }
    }

    [Test]
    public async Task Hovering_a_link_recolours_it_without_layout_and_a_disabled_one_shows_no_hand()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Link("enabled", id: "on");
                ui.Link(
                    "disabled",
                    onClick: Callback.Of(clicks, static c => c.Value++),
                    disabled: true,
                    id: "off"
                );
            }
        });
        var on = driver.Get("on");

        driver.Hover(on, settle: false);
        driver.Frame();

        await Assert.That(driver.Cursor).IsEqualTo(Cursors.Hand);
        await Assert.That(driver.Stats.Layouts).IsEqualTo(0);
        var line = await Assert.That(on.Parent).IsNotNull();
        await Assert
            .That(TextOf(line).Buffer.Runs[0].Style.Fill)
            .IsEqualTo((Paint)Themes.Dark.Color(ColorToken.FgAccentHover));

        var off = driver.Get("off");
        driver.Hover(off);
        await Assert.That(driver.Cursor).IsNotEqualTo(Cursors.Hand);
        driver.Click(off);
        await Assert.That(clicks.Value).IsEqualTo(0);
    }

    [Test]
    public async Task A_highlight_marks_each_match_ignoring_case_and_moving_it_lays_nothing_out()
    {
        var query = new Model { Name = "AN" };
        using var driver = UiDriver.Render(ui =>
            ui.Text("Banana", highlight: query.Name, id: "text")
        );
        var text = TextOf(driver.Get("text"));
        var marked = (Paint)Themes.Dark.Color(ColorToken.FgFilledWarning);

        await Assert.That(Runs(text)).IsEquivalentTo([(0, 1), (1, 2), (3, 2), (5, 1)]);
        await Assert.That(text.Buffer.Runs[1].Style.Fill).IsEqualTo(marked);
        await Assert.That(text.Buffer.Runs[3].Style.Fill).IsNotEqualTo(marked);

        query.Name = "na";
        driver.Frame();

        await Assert.That(Runs(text)).IsEquivalentTo([(0, 2), (2, 2), (4, 2)]);
        await Assert.That(driver.Stats.Layouts).IsEqualTo(0);
    }

    [Test]
    public async Task Text_and_its_spans_take_their_containers_size_and_tabular_digits_highlighted_or_not()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (
                    ui.Box(
                        direction: Direction.Column,
                        align: Align.Start,
                        fontSize: 32,
                        tabularDigits: true
                    )
                )
                {
                    using (ui.Text(id: "ones"))
                        ui.Span("111");
                    ui.Text("888", id: "eights");
                    ui.Text("111", highlight: "1", id: "marked");
                }
                ui.Text("888", fontSize: 32, tabularDigits: true, id: "sized");
                ui.Text("111", fontSize: 32, id: "proportional");
            }
        });
        var ones = driver.Get("ones").Bounds.Width;
        var eights = driver.Get("eights").Bounds.Width;

        await Assert.That(ones).IsEqualTo(eights);
        await Assert.That(driver.Get("marked").Bounds.Width).IsEqualTo(ones);
        await Assert.That(eights).IsEqualTo(driver.Get("sized").Bounds.Width);
        await Assert.That(driver.Get("proportional").Bounds.Width).IsLessThan(ones);
    }

    [Test]
    public async Task A_link_takes_a_click_on_its_last_letters_right_half_but_none_released_off_it_or_pressed_right_and_one_inside_a_pressable_clicks_alone()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    direction: Direction.Column,
                    align: Align.Start,
                    onContextMenu: Handler<PointerEvent>.Of(
                        log,
                        static (Log l, ref PointerEvent e) => l.Add("menu")
                    )
                )
            )
            {
                using (ui.Text(id: "text"))
                {
                    ui.Link("WW", onClick: Callback.Of(log, static l => l.Add("link")), id: "link");
                    ui.Span(" after");
                }
                using (ui.Box(padding: 8, onClick: Callback.Of(log, static l => l.Add("button"))))
                using (ui.Text())
                    ui.Link(
                        "inner",
                        onClick: Callback.Of(log, static l => l.Add("inner")),
                        id: "inner"
                    );
            }
        });
        var link = driver.Get("link");
        var text = driver.Get("text").ViewBounds;

        driver.Click(link, new Vector2(link.ViewBounds.Width * 0.9f, link.ViewBounds.Height / 2f));
        var held = driver.Hold(link);
        held.MoveBy(new Vector2(text.Right - 2f - held.Position.X, 0f));
        held.Release();
        driver.Click(link, MouseButton.Right);
        driver.Click(driver.Get("inner"));

        await Assert.That(log.ToString()).IsEqualTo("link menu inner");
    }

    [Test]
    public async Task A_link_moved_under_a_still_pointer_is_hovered_then_left_and_drops_its_hand_once_disabled_and_its_hover_once_the_pointer_leaves()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (ui.Text())
            {
                ui.Span(model.Name);
                ui.Link("docs", disabled: model.Remember, id: "link");
            }
        });
        driver.Hover(driver.Get("link"));
        var hand = driver.Cursor;

        model.Remember = true;
        driver.Settle();
        var disabled = driver.Cursor;
        model.Remember = false;
        model.Name = "Read the whole manual first, then the ";
        driver.Settle();
        var movedOff = LinkHovered(driver);
        model.Name = "";
        driver.Settle();
        var movedBack = LinkHovered(driver);
        driver.Unhover();

        await Assert.That(hand).IsEqualTo(Cursors.Hand);
        await Assert.That(disabled).IsNotEqualTo(Cursors.Hand);
        await Assert
            .That((movedOff, movedBack, LinkHovered(driver)))
            .IsEqualTo((false, true, false));
    }

    static bool LinkHovered(UiDriver driver) =>
        (driver.Get("link").States & InputStates.Hovered) != 0;

    [Test]
    public async Task Letter_spacing_set_on_a_container_after_layout_widens_plain_text_and_spans_after_each_character()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    direction: Direction.Column,
                    align: Align.Start,
                    fontSize: 16,
                    letterSpacing: model.Remember ? 0.5f : null
                )
            )
            {
                ui.Text("WW", id: "plain");
                using (ui.Text(id: "spans"))
                {
                    ui.Span("W");
                    ui.Span("W");
                }
            }
        });
        var plain = driver.Get("plain").Bounds.Width;
        var spans = driver.Get("spans").Bounds.Width;

        model.Remember = true;
        driver.Settle();

        await Assert.That(driver.Get("plain").Bounds.Width - plain).IsEqualTo(16f).Within(0.01f);
        await Assert.That(driver.Get("spans").Bounds.Width - spans).IsEqualTo(16f).Within(0.01f);
    }

    [Test]
    public async Task An_underline_added_to_plain_text_or_a_span_draws_without_laying_out_again()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Text("WW", underline: model.Remember);
                using (ui.Text())
                {
                    ui.Span("plain ");
                    ui.Span("WW", underline: model.Remember);
                }
            }
        });
        var before = Underlines(driver);

        model.Remember = true;
        driver.Frame();

        await Assert.That(driver.Stats.Layouts).IsEqualTo(0);
        await Assert.That((before, Underlines(driver))).IsEqualTo((0, 2));
    }

    static int Underlines(UiDriver driver)
    {
        var count = 0;
        foreach (var line in driver.Draw().Lines)
        {
            if (
                line.Contains("/Text[", StringComparison.Ordinal)
                && line.Contains(": box ", StringComparison.Ordinal)
            )
                count++;
        }
        return count;
    }

    static List<(int Start, int Length)> Runs(TextPayload text)
    {
        var runs = new List<(int, int)>();
        foreach (var run in text.Buffer.Runs)
            runs.Add((run.Start, run.Length));
        return runs;
    }
}
