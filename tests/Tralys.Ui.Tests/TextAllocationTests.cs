using System.Numerics;
using Tralys.Ui.Fonts;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;
using TUnit.Assertions.Enums;

namespace Tralys.Ui.Tests;

public sealed class TextAllocationTests
{
    [Test]
    public async Task A_shadowed_caption_counting_up_and_its_shadow_coming_and_going_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
            ui.Text(
                $"Wave {model.Count}",
                shadow: model.Count % 8 < 4
                    ? new DropShadow(Color.FromRgb(0, 0, 0), 270f, 1f, 8f, 0.9f)
                    : null
            )
        );

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Typing_and_erasing_in_a_focused_field_allocates_nothing()
    {
        var text = new ObservableText();
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(text: text, id: "field", width: 200)
        );
        driver.Focus(driver.Get("field"));

        var bytes = driver.WorstFrameBytes(static d =>
        {
            d.Type("a", settle: false);
            d.Press(Key.BackSpace, settle: false);
            d.Frame();
        });

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Typing_and_erasing_in_a_field_that_reads_each_edit_allocates_nothing()
    {
        var read = new Counter();
        var text = new ObservableText();
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(
                text: text,
                onEdit: Callback.Of(text, read, static (t, r) => r.Value += t.Length),
                id: "field",
                width: 200
            )
        );
        driver.Focus(driver.Get("field"));

        var bytes = driver.WorstFrameBytes(static d =>
        {
            d.Type("a", settle: false);
            d.Press(Key.BackSpace, settle: false);
            d.Frame();
        });

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(read.Value).IsGreaterThan(0);
    }

    [Test]
    public async Task Moving_the_caret_across_a_scrolling_line_allocates_nothing()
    {
        var text = new ObservableText("a line of text far longer than its field");
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(text: text, id: "field", width: 60)
        );
        driver.Focus(driver.Get("field"));

        var bytes = driver.WorstFrameBytes(static d =>
        {
            d.Press(Key.End, settle: false);
            d.Press(Key.Left, ModifierKeys.Control, settle: false);
            d.Press(Key.Home, settle: false);
            d.Press(Key.Right, settle: false);
            d.Frame();
        });

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Formatting_enums_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            ui.Text(ui.Format($"{(Tone)(model.Count % 3 * 5 - 2)}"));
            ui.Text(ui.Format($"{(Sides)(model.Count % 8)}"));
        });

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task An_outlined_countdown_reformatted_every_frame_while_the_scale_flips_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(
            ui =>
            {
                var seconds = (99 - model.Count % 100) / 10f;
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                {
                    ui.Text("H", fontSize: 32);
                    ui.Text("Notice", strokeWidth: 1, stroke: Color.FromRgb(0, 0, 0));
                    ui.Text(
                        $"{seconds:0.0}s",
                        fontSize: 18,
                        strokeWidth: 2,
                        stroke: Color.FromRgb(0, 0, 0)
                    );
                }
            },
            scale: 1.25f
        );

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                d.View.Scale = d.View.Scale == 1.5f ? 1f : 1.5f;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_blinking_caret_allocates_nothing()
    {
        var text = new ObservableText("abc");
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(text: text, id: "field", width: 100)
        );
        driver.Focus(driver.Get("field"));
        driver.Press(Key.End);

        var bytes = driver.WorstFrameBytes(static d => d.Frame(0.3f));

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_selection_dragged_every_frame_allocates_nothing()
    {
        var text = new ObservableText("hello world");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(padding: 60))
                ui.EditableText(text: text, id: "field", width: 120);
        });
        var field = driver.Get("field");

        var bytes = driver.WorstFrameBytes(
            field,
            static (d, f) =>
            {
                d.Drag(f, new Vector2(-50f, 0f), settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Rich_and_highlighted_text_written_every_frame_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                using (ui.Text())
                {
                    ui.Span("Count: ");
                    ui.Span(ui.Format($"{model.Count}"), underline: true);
                    ui.Link("more");
                }
                ui.Text($"item {model.Count}", highlight: "1");
            }
        });

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count = (m.Count + 7) % 1000;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Rich_text_under_a_theme_switching_every_ten_frames_takes_its_colours_and_allocates_nothing()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Text(id: "rich"))
            {
                ui.Span("plain ");
                ui.Span("muted ", color: ui.Theme.Color(ColorToken.FgMuted));
                ui.Span("bold ", fontWeight: FontWeight.Bold);
                ui.Span("fixed ", color: Fixed);
                ui.Link("more");
            }
        });

        var bytes = driver.WorstFrameBytes(
            new Counter(),
            static (d, frames) =>
            {
                if (++frames.Value % 10 == 0)
                    d.View.Context.Theme =
                        d.View.Context.Theme == Themes.Dark ? Themes.Light : Themes.Dark;
                d.Frame();
            }
        );
        driver.View.Context.Theme = Themes.Dark;
        driver.Frame();
        driver.View.Context.Theme = Themes.Light;
        driver.Frame();
        var fills = new List<Paint>();
        foreach (var run in ((TextPayload)driver.Get("rich").Payload!).Buffer.Runs)
            fills.Add(run.Style.Fill);
        var light = Themes.Light;

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert
            .That(fills)
            .IsEquivalentTo(
                new Paint[]
                {
                    light.Color(ColorToken.Fg),
                    light.Color(ColorToken.FgMuted),
                    light.Color(ColorToken.Fg),
                    Fixed,
                    light.Color(ColorToken.FgAccent),
                },
                CollectionOrdering.Matching
            );
    }

    static readonly Color Fixed = Color.FromRgb(220, 120, 40);

    const string Paragraph = "a line far longer than the sixteen glyphs a text first holds";

    [Test]
    public Task A_long_text_mounted_after_a_short_one_takes_a_long_texts_buffers_and_allocates_nothing()
    {
        var model = new Model();
        return UiDriver.Run(
            ui =>
            {
                using (ui.Box(direction: Direction.Column))
                {
                    switch (model.Count)
                    {
                        case 1:
                            ui.Text("short", key: "a");
                            ui.Text("short", key: "b");
                            break;
                        case 3:
                            ui.Text("short", key: "c");
                            ui.Text(Paragraph, key: "d");
                            break;
                        case 5:
                            ui.Text("short", key: "e");
                            ui.Text(Paragraph, key: "f");
                            break;
                    }
                }
            },
            async driver =>
            {
                for (var count = 1; count < 5; count++)
                {
                    model.Count = count;
                    driver.Frame();
                }

                var bytes = AllocationGate.WorstFrameBytes(
                    () =>
                    {
                        model.Count = 5;
                        driver.Frame();
                    },
                    frames: 1,
                    windows: 1,
                    warmup: 0
                );

                await Assert.That(bytes).IsEqualTo(0L);
                await Assert.That(driver.GetText(Paragraph).IsMounted).IsTrue();
            }
        );
    }

    [Test]
    public async Task Hovering_a_link_on_and_off_allocates_nothing()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Text())
            {
                ui.Span("Read the ");
                ui.Link("docs", id: "link");
            }
        });
        var link = driver.Get("link");

        var bytes = driver.WorstFrameBytes(
            link,
            static (d, l) =>
            {
                d.Hover(l, settle: false);
                d.Unhover(settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_link_the_run_before_it_moves_on_and_off_a_still_pointer_lights_takes_its_clicks_and_allocates_nothing()
    {
        var run = new LinkRun();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Text())
            {
                ui.Span(run.Model.Count % 4 < 2 ? "" : "WWWW");
                ui.Link("WW", onClick: Callback.Of(run, static r => r.Clicks++), id: "link");
            }
        });
        run.Link = driver.Get("link");
        driver.Hover(run.Link);

        var bytes = driver.WorstFrameBytes(
            run,
            static (d, r) =>
            {
                r.Model.Count++;
                d.Frame();
                var under = r.Model.Count % 4 < 2;
                if (((r.Link!.States & InputStates.Hovered) != 0) != under)
                    r.Wrong++;
                if (r.Model.Count % 4 == 0)
                    d.Click(r.Link, settle: false);
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(run.Wrong).IsEqualTo(0);
        await Assert.That(run.Clicks).IsGreaterThan(0);
    }

    [Test]
    public async Task A_ring_of_wrapping_chat_lines_each_rewritten_and_moved_to_the_bottom_in_turn_allocates_nothing()
    {
        var chat = new ChatLog();
        using var driver = UiDriver.Render(ui => ui.ChatRing(chat: chat), scale: 1.25f);

        var bytes = AllocationGate.WorstFrameBytes(
            () =>
            {
                chat.Next();
                driver.Frame();
            },
            warmup: 200
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    sealed class LinkRun
    {
        public readonly Model Model = new();
        public Node? Link;
        public int Clicks;
        public int Wrong;
    }
}

public sealed partial class ChatLog : Tralys.Ui.Mvvm.ObservableObject
{
    public const int Lines = 8;

    public readonly int[] Stamps = new int[Lines];
    public readonly int[] Senders = new int[Lines];
    public readonly int[] Bodies = new int[Lines];

    [Tralys.Ui.Mvvm.ObservableProperty]
    public partial int Tick { get; set; }

    public void Next()
    {
        var line = Tick % Lines;
        Stamps[line] = Tick;
        Senders[line] = Tick % 3;
        Bodies[line] = Tick % 3;
        Tick++;
    }
}

public readonly record struct ChatRingProps
{
    public ChatLog? Chat { get; init; }
}

/// <summary>A 280-wide column of the chat's wrapping lines, oldest first: a grey stamp, a gold sender, a grey colon and
/// the body, short, long enough to wrap, or CJK.</summary>
public sealed class ChatRing : Component<ChatRingProps>
{
    static readonly Color Grey = Color.FromRgb(140, 140, 140);
    static readonly Color Gold = Color.FromRgb(230, 180, 40);
    static readonly string[] Senders = ["ada", "grace", "linus"];
    static readonly string[] Bodies =
    [
        "ok",
        "this line runs on long enough that it has to wrap across the column at least once",
        "今日は良い天気ですね、散歩に行きましょう",
    ];

    protected override void Render(Ui ui, in ChatRingProps p)
    {
        var chat = p.Chat!;
        var tick = chat.Tick;
        using (ui.Box(direction: Direction.Column, width: 280))
        {
            for (var i = 0; i < ChatLog.Lines; i++)
            {
                var line = (tick + i) % ChatLog.Lines;
                using (ui.Text(fontSize: 14, wrap: true, key: line))
                {
                    ui.Span(ui.Format($"[{chat.Stamps[line]:00000}] "), color: Grey);
                    ui.Span(Senders[chat.Senders[line]], color: Gold);
                    ui.Span(": ", color: Grey);
                    ui.Span(Bodies[chat.Bodies[line]]);
                }
            }
        }
    }
}
