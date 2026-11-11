using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ScrollingAllocationTests
{
    [Test]
    public async Task Bars_shown_and_fading_back_as_the_pointer_comes_and_goes_every_hundred_frames_allocate_nothing()
    {
        var area = new AreaFixture();
        using var driver = area.Render();
        var walk = new Walk(driver.Get("area"));

        var bytes = driver.WorstFrameBytes(
            walk,
            static (d, w) =>
            {
                if (w.Tick % 200 == 0)
                    d.Hover(w.Target, settle: false);
                else if (w.Tick % 200 == 100)
                    d.Unhover(settle: false);
                w.Tick++;
                d.Frame(0.02f);
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_wheel_notch_every_twelve_frames_gliding_either_way_allocates_nothing()
    {
        var area = new AreaFixture();
        using var driver = area.Render();
        var walk = new Walk(driver.Get("content"));

        var bytes = driver.WorstFrameBytes(
            walk,
            static (d, w) =>
            {
                if (w.Tick % 12 == 0)
                    d.Wheel(w.Target, new Vector2(0f, w.Tick % 24 == 0 ? -1f : 1f), settle: false);
                w.Tick++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(area.Handle!.Offset.Y).IsGreaterThan(0f);
    }

    [Test]
    public async Task A_bars_arrows_held_in_turn_repeating_their_steps_allocate_nothing()
    {
        var area = new AreaFixture { ContentHeight = 100000f };
        using var driver = area.Render();

        var down = driver.Hold(Bars.Arrow(driver, forward: true), settle: false);
        var downBytes = driver.WorstFrameBytes(static d => d.Frame());
        var stepped = area.Handle!.Offset.Y;
        down.Release(settle: false);
        var up = driver.Hold(Bars.Arrow(driver, forward: false), settle: false);
        var upBytes = driver.WorstFrameBytes(static d => d.Frame());
        up.Release();

        await Assert.That((downBytes, upBytes)).IsEqualTo((0L, 0L));
        await Assert.That(stepped).IsGreaterThan(16f);
        await Assert.That(area.Handle.Offset.Y).IsLessThan(stepped);
    }

    [Test]
    public async Task A_bars_thumb_dragged_up_and_down_every_frame_scrolls_the_content_and_allocates_nothing()
    {
        var area = new AreaFixture { ContentHeight = 2000f };
        using var driver = area.Render();
        var held = driver.Hold(Bars.Thumb(driver));
        var moves = new Moves(held, new Vector2(0f, 2f));
        var seen = new Counter();

        var bytes = driver.WorstFrameBytes(
            (moves, area, seen),
            static (d, s) =>
            {
                s.moves.Press.MoveBy(
                    s.moves.Tick++ % 40 < 20 ? s.moves.By : -s.moves.By,
                    settle: false
                );
                d.Frame();
                if (s.area.Handle!.Offset.Y > 0f)
                    s.seen.Value++;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(seen.Value).IsGreaterThan(0);
    }

    [Test]
    public async Task A_finger_panning_an_area_of_buttons_every_frame_clicks_none_and_allocates_nothing()
    {
        var area = new AreaFixture();
        using var driver = area.Render();
        var finger = driver.Hold(driver.Get("button"), PointerKind.Touch);
        finger.MoveBy(new Vector2(0f, -10f));
        var moves = new Moves(finger, new Vector2(0f, -6f));

        var bytes = driver.WorstFrameBytes(
            moves,
            static (d, m) =>
            {
                m.Press.MoveBy(m.Tick++ % 18 < 9 ? m.By : -m.By, settle: false);
                d.Frame();
            }
        );
        finger.Release();

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(area.Clicks).IsEqualTo(0);
    }

    [Test]
    public async Task A_fling_every_thirty_frames_either_way_coasting_between_allocates_nothing()
    {
        var area = new AreaFixture { ContentHeight = 100000f };
        using var driver = area.Render();
        area.Handle!.ScrollTo(null, 50000f);
        driver.Settle();
        var walk = new Walk(driver.Get("content"));

        var bytes = driver.WorstFrameBytes(
            walk,
            static (d, w) =>
            {
                if (w.Tick % 30 == 0)
                    d.Fling(
                        w.Target,
                        new Vector2(0f, w.Tick % 60 == 0 ? -36f : 36f),
                        seconds: 0.05f,
                        settle: false
                    );
                w.Tick++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_board_dragged_to_pan_every_frame_with_its_header_following_allocates_nothing()
    {
        var follow = new FollowFixture();
        using var driver = follow.Render();
        var mouse = driver.Hold(driver.Get("board"));
        mouse.MoveBy(new Vector2(-10f, 0f));
        var moves = new Moves(mouse, new Vector2(-6f, 0f));

        var bytes = driver.WorstFrameBytes(
            moves,
            static (d, m) =>
            {
                m.Press.MoveBy(m.Tick++ % 20 < 10 ? m.By : -m.By, settle: false);
                d.Frame();
            }
        );
        mouse.Release();

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(follow.Header!.Offset.X).IsEqualTo(follow.Lead!.Offset.X);
    }

    [Test]
    public async Task A_chat_kept_at_its_end_with_both_bars_shown_as_a_line_comes_and_goes_every_frame_allocates_nothing()
    {
        var chat = new Chat();
        using var driver = chat.Render();

        var bytes = driver.WorstFrameBytes(
            chat,
            static (d, c) =>
            {
                c.First.Value++;
                c.Handle!.ScrollToEnd();
                d.Frame();
            }
        );
        var vertical = driver.All<Scrollbar>()[0].Props<ScrollbarProps>();

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(driver.All<Scrollbar>().Count).IsEqualTo(2);
        await Assert
            .That(vertical.Max)
            .IsEqualTo(chat.Handle!.Extent.Height - chat.Handle.Viewport.Height);
        await Assert.That(vertical.Value).IsEqualTo(chat.Handle.Offset.Y);
        await Assert.That(vertical.Value).IsEqualTo(vertical.Max);
        await Assert
            .That(driver.Get("last").ViewBounds.Bottom)
            .IsEqualTo(driver.Get("area").ViewBounds.Y + chat.Handle.Viewport.Height);
    }

    [Test]
    public async Task Content_outgrowing_an_area_and_fitting_again_every_frame_shows_and_hides_its_bar_and_allocates_nothing()
    {
        var growing = new Growing();
        using var driver = growing.Render();

        var bytes = driver.WorstFrameBytes(
            growing,
            static (d, g) =>
            {
                g.Tall.Value = !g.Tall.Peek;
                d.Frame();
            }
        );
        growing.Tall.Value = true;
        driver.Settle();
        var tall = driver.All<Scrollbar>().Count;
        growing.Tall.Value = false;
        driver.Settle();

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(tall).IsEqualTo(1);
        await Assert.That(driver.All<Scrollbar>()).IsEmpty();
    }

    [Test]
    public async Task A_thumb_dragged_inside_a_draggable_item_every_frame_keeps_its_drag_and_allocates_nothing()
    {
        var starts = new Counter();
        var drops = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 300,
                    height: 200,
                    background: Color.FromRgb(1, 2, 3),
                    dragData: drops,
                    onDragEnd: Callback<DragEffect>.Of(drops, static (c, _) => c.Value++)
                )
            )
            using (
                ui.Thumb(
                    onDragStart: Callback.Of(starts, static c => c.Value++),
                    width: 40,
                    height: 40,
                    id: "thumb"
                )
            ) { }
        });
        var target = driver.Get("thumb");

        var bytes = driver.WorstFrameBytes(
            target,
            static (d, t) =>
            {
                d.Drag(t, new Vector2(30f, 0f), settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(drops.Value).IsEqualTo(0);
        await Assert.That(starts.Value).IsGreaterThan(4);
    }

    [Test]
    public async Task A_pan_zoom_view_dragged_every_frame_and_wheeled_every_twenty_allocates_nothing()
    {
        var view = new PanFixture();
        using var driver = view.Render();
        var root = driver.Get("pz");
        var mouse = driver.Hold(root);
        mouse.MoveBy(new Vector2(-10f, 0f));
        var moves = new Moves(mouse, new Vector2(-6f, 0f));

        var bytes = driver.WorstFrameBytes(
            (moves, root),
            static (d, s) =>
            {
                var tick = s.moves.Tick++;
                s.moves.Press.MoveBy(tick % 40 < 20 ? s.moves.By : -s.moves.By, settle: false);
                if (tick % 20 == 0)
                    d.Wheel(s.root, new Vector2(0f, tick % 40 == 0 ? 1f : -1f), settle: false);
                d.Frame();
            }
        );
        mouse.Release();

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(view.Zooms.Count).IsGreaterThan(2);
    }

    [Test]
    public async Task A_second_finger_beside_a_thumb_in_a_pan_zoom_view_every_frame_allocates_nothing()
    {
        var view = new PanFixture
        {
            Width = 300f,
            Height = 200f,
            WithThumb = true,
        };
        using var driver = view.Render();
        var root = driver.Get("pz");
        var thumb = driver.Hold(driver.Get("thumb"), PointerKind.Touch);
        var moves = new Moves(thumb, new Vector2(4f, 0f));

        var bytes = driver.WorstFrameBytes(
            (moves, root),
            static (d, s) =>
            {
                s.moves.Press.MoveBy(
                    s.moves.Tick++ % 2 == 0 ? s.moves.By : -s.moves.By,
                    settle: false
                );
                d.Drag(
                    s.root,
                    new Vector2(0f, s.moves.Tick % 4 < 2 ? -20f : 20f),
                    PointerKind.Touch,
                    settle: false
                );
                d.Frame();
            }
        );
        thumb.Release();

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(view.Zooms).IsEmpty();
    }

    [Test]
    public async Task A_finger_pinching_beside_a_held_one_in_a_pan_zoom_view_every_frame_allocates_nothing()
    {
        var view = new PanFixture { Width = 300f, Height = 200f };
        view.Zooms.Capacity = 4096;
        using var driver = view.Render();
        var root = driver.Get("pz");
        var bounds = root.ViewBounds;
        var held = driver.Hold(new Vector2(bounds.X + 50f, bounds.Y + 100f), PointerKind.Touch);

        var bytes = driver.WorstFrameBytes(
            new Walk(root),
            static (d, s) =>
            {
                d.Drag(
                    s.Target,
                    new Vector2(s.Tick++ % 2 == 0 ? 25f : -20f, 0f),
                    PointerKind.Touch,
                    settle: false
                );
                d.Frame();
            }
        );
        held.Release();

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(view.Zooms.Count).IsGreaterThan(2);
    }
}

internal sealed class Moves(HeldPointer press, Vector2 by)
{
    public readonly HeldPointer Press = press;
    public readonly Vector2 By = by;
    public int Tick;
}

internal sealed class Walk(Node target)
{
    public readonly Node Target = target;
    public int Tick;
}

/// <summary>A scroll area 100 high over content 50 or 1000 high.</summary>
internal sealed class Growing
{
    public State<bool> Tall = null!;

    public UiDriver Render() =>
        UiDriver.Render(
            ui =>
            {
                Tall = ui.State(false);
                using (ui.ScrollArea(width: 200, height: 100))
                using (ui.Box(height: Tall.Value ? 1000 : 50, shrink: 0f)) { }
            },
            new Size(400, 300)
        );
}

/// <summary>A chat log of a hundred lines in a both-ways scroll area, its window moved on a line at a time.</summary>
internal sealed class Chat
{
    const int Shown = 30;
    static readonly string[] Lines = Make();

    public ScrollHandle? Handle;
    public State<int> First = null!;

    public UiDriver Render() =>
        UiDriver.Render(
            ui =>
            {
                Handle = ui.ScrollHandle();
                First = ui.State(0);
                var first = First.Value;
                using (
                    ui.ScrollArea(
                        scroll: Handle,
                        horizontal: ScrollbarVisibility.Visible,
                        vertical: ScrollbarVisibility.Visible,
                        width: 200,
                        height: 120,
                        id: "area"
                    )
                )
                using (ui.Box(direction: Direction.Column))
                    for (var i = first; i < first + Shown; i++)
                        ui.Text(
                            Lines[i % Lines.Length],
                            height: 20,
                            id: i == first + Shown - 1 ? "last" : null,
                            key: i
                        );
            },
            new Size(400, 300)
        );

    static string[] Make()
    {
        var lines = new string[100];
        for (var i = 0; i < lines.Length; i++)
            lines[i] = $"Line {i}";
        return lines;
    }
}
