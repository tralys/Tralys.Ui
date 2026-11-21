using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class FloatingTests
{
    [Test]
    public async Task A_drag_from_the_handles_empty_area_or_from_a_button_inside_it_moves_the_surface_and_the_button_clicks_nothing()
    {
        var board = new FloatBoard { Bounds = new Rect(0f, 0f, 200f, 100f) };
        using var driver = UiDriver.Render(ui => board.Render(ui));

        driver.Drag(driver.Get("title"), new Vector2(50f, 30f));
        var fromTheBar = board.Bounds;
        driver.Drag(driver.Get("key"), new Vector2(50f, 30f));
        var fromTheButton = (board.Bounds, board.Clicks.Value);
        driver.Click(driver.Get("key"));

        await Assert.That(fromTheBar).IsEqualTo(new Rect(50f, 30f, 200f, 100f));
        await Assert.That(fromTheButton).IsEqualTo((new Rect(100f, 60f, 200f, 100f), 0));
        await Assert.That(board.Clicks.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_pinned_surface_or_a_right_button_drag_moves_nothing_and_reports_no_drag()
    {
        var board = new FloatBoard { Bounds = new Rect(0f, 0f, 200f, 100f), Pinned = true };
        using var driver = UiDriver.Render(ui => board.Render(ui));

        driver.Drag(driver.Get("title"), new Vector2(50f, 30f));
        var pinned = (board.Bounds, board.Drags.Count);
        board.Pinned = false;
        driver.Rerender();
        var right = driver.Hold(driver.Get("title"), button: MouseButton.Right);
        right.MoveBy(new Vector2(50f, 30f));
        right.Release();

        await Assert.That(pinned).IsEqualTo((new Rect(0f, 0f, 200f, 100f), 0));
        await Assert.That((board.Bounds, board.Drags.Count)).IsEqualTo((pinned.Item1, 0));
    }

    [Test]
    public async Task A_pinned_handle_refuses_its_drags_while_a_handle_inside_it_moves_the_surface()
    {
        var board = new FloatBoard
        {
            Bounds = new Rect(0f, 0f, 200f, 100f),
            HandlePinned = true,
            InnerHandle = true,
        };
        using var driver = UiDriver.Render(ui => board.Render(ui));

        var bar = driver.Hold(new Vector2(190f, 15f));
        bar.MoveBy(new Vector2(50f, 30f));
        bar.Release();
        var fromTheBar = (board.Bounds, board.Drags.Count);
        driver.Drag(driver.Get("key"), new Vector2(50f, 30f));

        await Assert.That(fromTheBar).IsEqualTo((new Rect(0f, 0f, 200f, 100f), 0));
        await Assert.That(board.Bounds).IsEqualTo(new Rect(50f, 30f, 200f, 100f));
    }

    [Test]
    public async Task A_grip_its_caller_places_on_a_corner_alone_resizes_the_surface_by_that_corner()
    {
        var board = new FloatBoard { Bounds = new Rect(20f, 20f, 200f, 100f), CornerGrip = true };
        using var driver = UiDriver.Render(ui => board.Render(ui));

        driver.Drag(driver.Get("corner"), new Vector2(30f, 20f));

        await Assert.That(board.Bounds).IsEqualTo(new Rect(20f, 20f, 230f, 120f));
        await Assert.That(board.Commits).IsEquivalentTo([new Rect(20f, 20f, 230f, 120f)]);
    }

    [Test]
    public async Task A_finger_dragging_the_handle_inside_a_panning_scroll_area_moves_the_surface_and_pans_nothing()
    {
        var board = new FloatBoard
        {
            Bounds = new Rect(100f, 100f, 200f, 100f),
            AreaWidth = 600f,
            AreaHeight = 600f,
            Scrolled = true,
        };
        using var driver = UiDriver.Render(ui => board.Render(ui));

        driver.Drag(driver.Get("title"), new Vector2(-40f, -40f), PointerKind.Touch);

        await Assert.That(board.Bounds).IsEqualTo(new Rect(60f, 60f, 200f, 100f));
        await Assert.That(board.Offset).IsEqualTo(Vector2.Zero);
    }

    [Test]
    public async Task A_moved_surface_lands_clamped_snapped_and_clamped_again_by_its_measured_size_and_moves_freely_along_an_axis_the_area_has_no_size_on()
    {
        var sized = new FloatBoard
        {
            Content = new Size(100f, 50f),
            AreaWidth = 300f,
            AreaHeight = 300f,
            Snap = 80f,
        };
        var free = new FloatBoard
        {
            Bounds = new Rect(50f, 50f, 100f, 50f),
            AreaWidth = 300f,
            AreaHeight = 0f,
        };
        var wide = new FloatBoard { Bounds = new Rect(0f, 0f, 500f, 50f), AreaWidth = 400f };
        using var first = UiDriver.Render(ui => sized.Render(ui));
        using var second = UiDriver.Render(ui => free.Render(ui));
        using var third = UiDriver.Render(ui => wide.Render(ui));

        first.Drag(first.Get("title"), new Vector2(230f, 10f));
        second.Drag(second.Get("title"), new Vector2(-80f, -500f));
        third.Drag(third.Get("title"), new Vector2(60f, 0f));

        await Assert.That(sized.Bounds).IsEqualTo(new Rect(200f, 0f, 0f, 0f));
        await Assert.That(free.Bounds).IsEqualTo(new Rect(0f, -450f, 100f, 50f));
        await Assert.That(wide.Bounds.X).IsEqualTo(0f);
    }

    [Test]
    public async Task A_surface_its_caller_keeps_in_place_stays_there_while_its_drag_still_reports_its_start_moves_and_end()
    {
        var board = new FloatBoard { Bounds = new Rect(0f, 0f, 200f, 100f), Kept = true };
        using var driver = UiDriver.Render(ui => board.Render(ui));

        driver.Drag(driver.Get("title"), new Vector2(50f, 30f));

        await Assert.That(board.Bounds).IsEqualTo(new Rect(0f, 0f, 200f, 100f));
        await Assert.That(board.Drags[0]).IsEqualTo(DragPhase.Start);
        await Assert.That(board.Drags[^1]).IsEqualTo(DragPhase.End);
        await Assert.That(board.Drags.Count(p => p == DragPhase.Move)).IsGreaterThan(0);
    }

    [Test]
    public async Task Only_a_drag_that_moved_the_surface_commits_once_as_it_ends()
    {
        var board = new FloatBoard { Bounds = new Rect(0f, 0f, 200f, 100f) };
        using var driver = UiDriver.Render(ui => board.Render(ui));

        driver.Click(driver.Get("title"));
        var clicked = board.Commits.Count;
        driver.Drag(driver.Get("title"), new Vector2(50f, 30f));

        await Assert.That(clicked).IsEqualTo(0);
        await Assert.That(board.Commits).IsEquivalentTo([new Rect(50f, 30f, 200f, 100f)]);
    }

    [Test]
    public async Task A_second_finger_on_the_handle_leaves_the_drag_to_the_first_which_ends_it_once()
    {
        var board = new FloatBoard { Bounds = new Rect(0f, 0f, 200f, 100f) };
        using var driver = UiDriver.Render(ui => board.Render(ui));

        var first = driver.Hold(driver.Get("title"), PointerKind.Touch);
        first.MoveBy(new Vector2(40f, 0f));
        var second = driver.Hold(driver.Get("title"), PointerKind.Touch);
        second.MoveBy(new Vector2(40f, 40f));
        second.Release();
        var afterTheSecond = (board.Bounds.X, board.Bounds.Y, Ends(board));
        first.MoveBy(new Vector2(20f, 0f));
        first.Release();

        await Assert.That(afterTheSecond).IsEqualTo((40f, 0f, 0));
        await Assert.That((board.Bounds.X, board.Bounds.Y, Ends(board))).IsEqualTo((60f, 0f, 1));
    }

    [Test]
    public async Task The_window_deactivating_or_the_handle_leaving_mid_drag_keeps_the_surface_where_it_got_and_commits_once_with_no_end()
    {
        var moved = new FloatBoard { Bounds = new Rect(0f, 0f, 200f, 100f) };
        var unmounted = new FloatBoard { Bounds = new Rect(0f, 0f, 200f, 100f) };
        var resized = new FloatBoard
        {
            Bounds = new Rect(0f, 0f, 200f, 100f),
            Edges = ResizeEdges.Right,
        };
        using var first = UiDriver.Render(ui => moved.Render(ui));
        using var second = UiDriver.Render(ui => unmounted.Render(ui));
        using var third = UiDriver.Render(ui => resized.Render(ui));

        var mouse = first.Hold(first.Get("title"));
        mouse.MoveBy(new Vector2(30f, 0f));
        first.WindowFocus(false);
        mouse.Release();
        var other = second.Hold(second.Get("title"));
        other.MoveBy(new Vector2(30f, 0f));
        unmounted.Handle = false;
        second.Rerender();
        other.Release();
        var edge = third.Hold(Grip(third, ResizeEdge.Right));
        edge.MoveBy(new Vector2(30f, 0f));
        third.WindowFocus(false);
        edge.Release();

        await Assert
            .That((moved.Bounds.X, moved.Commits.Count, Ends(moved)))
            .IsEqualTo((30f, 1, 0));
        await Assert.That((unmounted.Bounds.X, unmounted.Commits.Count)).IsEqualTo((30f, 1));
        await Assert.That((resized.Bounds.Width, resized.Commits.Count)).IsEqualTo((230f, 1));
    }

    [Test]
    public async Task An_edge_stops_at_the_area_the_left_one_moving_the_origin_so_the_right_edge_stays()
    {
        var right = new FloatBoard
        {
            Bounds = new Rect(600f, 0f, 100f, 100f),
            AreaWidth = 800f,
            Edges = ResizeEdges.All,
        };
        var left = new FloatBoard
        {
            Bounds = new Rect(150f, 0f, 300f, 100f),
            AreaWidth = 800f,
            Edges = ResizeEdges.All,
        };
        using var first = UiDriver.Render(ui => right.Render(ui), size: new Size(900f, 600f));
        using var second = UiDriver.Render(ui => left.Render(ui), size: new Size(900f, 600f));

        first.Drag(Grip(first, ResizeEdge.Right), new Vector2(300f, 0f));
        second.Drag(Grip(second, ResizeEdge.Left), new Vector2(-300f, 0f));

        await Assert.That(right.Bounds).IsEqualTo(new Rect(600f, 0f, 200f, 100f));
        await Assert.That(left.Bounds).IsEqualTo(new Rect(0f, 0f, 450f, 100f));
    }

    [Test]
    public async Task A_left_edge_dragged_past_the_minimum_width_stops_there_with_the_right_edge_where_it_was()
    {
        var board = new FloatBoard
        {
            Bounds = new Rect(100f, 0f, 200f, 100f),
            Edges = ResizeEdges.Left,
        };
        using var driver = UiDriver.Render(ui => board.Render(ui));

        driver.Drag(Grip(driver, ResizeEdge.Left), new Vector2(150f, 0f));

        await Assert.That(board.Bounds).IsEqualTo(new Rect(180f, 0f, 120f, 100f));
    }

    [Test]
    public async Task A_move_lands_on_its_own_grid_while_a_resize_keeps_the_snap()
    {
        var board = new FloatBoard
        {
            Content = new Size(100f, 50f),
            Edges = ResizeEdges.Bottom,
            Snap = 50f,
            MoveSnap = 0f,
        };
        using var driver = UiDriver.Render(ui => board.Render(ui));

        driver.Drag(driver.Get("title"), new Vector2(13f, 7f));
        var moved = board.Bounds;
        driver.Drag(Grip(driver, ResizeEdge.Bottom), new Vector2(0f, 30f));

        await Assert.That((moved.X, moved.Y)).IsEqualTo((13f, 7f));
        await Assert.That(board.Bounds.Height).IsEqualTo(100f);
    }

    [Test]
    public async Task A_surface_resizes_only_by_the_edges_it_names_commits_only_a_moving_resize_and_snaps_a_content_sized_height()
    {
        var board = new FloatBoard { Content = new Size(100f, 50f), Snap = 50f };
        using var driver = UiDriver.Render(ui => board.Render(ui));
        var grips = driver.All<ResizeHandle>().Count;
        board.Edges = ResizeEdges.Bottom;
        driver.Rerender();

        driver.Click(Grip(driver, ResizeEdge.Bottom));
        var clicked = board.Commits.Count;
        driver.Drag(Grip(driver, ResizeEdge.Bottom), new Vector2(0f, 30f));

        await Assert.That((grips, clicked)).IsEqualTo((0, 0));
        await Assert.That(driver.All<ResizeHandle>().Count).IsEqualTo(1);
        await Assert.That(board.Bounds).IsEqualTo(new Rect(0f, 0f, 0f, 100f));
        await Assert.That(board.Commits.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_linked_surface_follows_the_axis_a_corner_changed_more_and_sizes_its_width_from_a_bottom_edges_height()
    {
        var corner = new FloatBoard
        {
            Bounds = new Rect(500f, 0f, 217f, 200f),
            AreaWidth = 1000f,
            AreaHeight = 1000f,
            Edges = ResizeEdges.All,
            AspectRatio = 1f,
            AspectInset = new Size(17f, 0f),
        };
        var bottom = new FloatBoard
        {
            Bounds = new Rect(0f, 0f, 217f, 200f),
            AreaWidth = 1000f,
            AreaHeight = 1000f,
            Edges = ResizeEdges.All,
            AspectRatio = 1f,
            AspectInset = new Size(17f, 0f),
        };
        using var first = UiDriver.Render(ui => corner.Render(ui), size: new Size(1100f, 1100f));
        using var second = UiDriver.Render(ui => bottom.Render(ui), size: new Size(1100f, 1100f));

        first.Drag(Grip(first, ResizeEdge.BottomLeft), new Vector2(-100f, 30f));
        second.Drag(Grip(second, ResizeEdge.Bottom), new Vector2(0f, 40f));

        await Assert.That(corner.Bounds).IsEqualTo(new Rect(400f, 0f, 317f, 300f));
        await Assert.That(bottom.Bounds).IsEqualTo(new Rect(0f, 0f, 257f, 240f));
    }

    [Test]
    public async Task A_linked_surface_whose_ratio_cant_meet_its_minimum_inside_the_area_stays_inside_it()
    {
        var board = new FloatBoard
        {
            Bounds = new Rect(0f, 0f, 300f, 80f),
            AreaWidth = 300f,
            AreaHeight = 1000f,
            Edges = ResizeEdges.All,
            AspectRatio = 5f,
        };
        using var driver = UiDriver.Render(ui => board.Render(ui), size: new Size(1100f, 1100f));

        driver.Drag(Grip(driver, ResizeEdge.Right), new Vector2(-50f, 0f));

        await Assert.That(board.Bounds).IsEqualTo(new Rect(0f, 0f, 300f, 80f));
    }

    [Test]
    public async Task A_surface_of_a_set_height_keeps_its_content_to_it_so_a_taller_list_inside_scrolls()
    {
        var board = new FloatBoard
        {
            Bounds = new Rect(0f, 0f, 200f, 100f),
            ScrolledHeight = 1000f,
        };
        using var driver = UiDriver.Render(ui => board.Render(ui));

        var scrolled = driver.Get("scrolled").ViewBounds;
        driver.Wheel(driver.Get("scrolled"), new Vector2(0f, -1f));

        await Assert.That(scrolled).IsEqualTo(new Rect(0f, 30f, 200f, 70f));
        var content = await Assert.That(board.ContentScroll).IsNotNull();
        await Assert.That(content.Offset.Y).IsGreaterThan(0f);
    }

    static int Ends(FloatBoard board) => board.Drags.Count(p => p == DragPhase.End);

    static Node Grip(UiDriver driver, ResizeEdge edge)
    {
        foreach (var grip in driver.All<ResizeHandle>())
        {
            if (grip.Props<ResizeHandleProps>().Edge == edge)
                return grip;
        }
        throw new InvalidOperationException($"No {edge} grip.");
    }
}

/// <summary>A <see cref="Floating"/> surface in an area, holding its bounds in state: a title bar for a handle with a
/// button at its start, content of a fixed size, or a box scrolling content of a set height.</summary>
internal sealed class FloatBoard
{
    public Rect Bounds;
    public ResizeEdges Edges;
    public float Snap;
    public float? MoveSnap;
    public float AspectRatio;
    public Size AspectInset;
    public bool Pinned;
    public bool Kept;
    public bool Handle = true;
    public bool HandlePinned;
    public bool InnerHandle;
    public bool CornerGrip;
    public bool Scrolled;
    public float AreaWidth = 400f;
    public float AreaHeight = 300f;
    public Size Content;
    public float ScrolledHeight;
    public ScrollHandle? ContentScroll;
    public Vector2 Offset;
    public readonly Counter Clicks = new();
    public readonly List<Rect> Commits = [];
    public readonly List<DragPhase> Drags = [];
    State<Rect>? _state;
    readonly Callback<Rect> _change;
    readonly Callback<Rect> _commit;
    readonly Handler<DragEvent> _drag;
    readonly Callback _click;

    public FloatBoard()
    {
        _change = Callback<Rect>.Of(
            this,
            static (b, r) =>
            {
                if (b.Kept)
                    return;
                b.Bounds = r;
                if (b._state is not { } state)
                    throw new InvalidOperationException("The board moved before it rendered.");
                state.Value = r;
            }
        );
        _commit = Callback<Rect>.Of(this, static (b, r) => b.Commits.Add(r));
        _drag = Handler<DragEvent>.Of(this, static (b, ref e) => b.Drags.Add(e.Phase));
        _click = Callback.Of(Clicks, static c => c.Value++);
    }

    [FramePath]
    public void Render(Ui ui, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        using (ui.Fragment(file: file, line: line))
        {
            var state = _state = ui.State(Bounds);
            var scroll = ui.ScrollHandle();
            Offset = scroll.Offset;
            using (
                ui.Box(
                    direction: Direction.Column,
                    align: Align.Start,
                    overflow: Scrolled ? Overflow.Scroll : Overflow.Visible,
                    scroll: scroll,
                    width: Scrolled ? 300f : Length.Auto,
                    height: Scrolled ? 200f : Length.Auto
                )
            )
            using (
                ui.Floating(
                    bounds: state.Value,
                    onBoundsChange: _change,
                    onBoundsCommit: _commit,
                    resizeEdges: Edges,
                    snap: Snap,
                    moveSnap: MoveSnap,
                    aspectRatio: AspectRatio,
                    aspectInset: AspectInset,
                    pinned: Pinned,
                    width: AreaWidth,
                    height: AreaHeight
                )
            )
            {
                if (Handle)
                    using (ui.FloatingHandle(onDrag: _drag, pinned: HandlePinned))
                    using (ui.Box(height: 30, id: "title"))
                    {
                        if (InnerHandle)
                            using (ui.FloatingHandle())
                                ui.Button(text: "Key", onClick: _click, id: "key");
                        else
                            ui.Button(text: "Key", onClick: _click, id: "key");
                    }
                if (Content != default)
                    using (ui.Box(width: Content.Width, height: Content.Height)) { }
                if (ScrolledHeight > 0f)
                    using (
                        ui.Box(
                            overflow: Overflow.ScrollY,
                            scroll: ContentScroll = ui.ScrollHandle(),
                            grow: 1,
                            id: "scrolled"
                        )
                    )
                    using (ui.Box(height: ScrolledHeight)) { }
                if (CornerGrip)
                    ui.FloatingGrip(
                        edge: ResizeEdge.BottomRight,
                        ridges: true,
                        width: 16,
                        height: 16,
                        position: Position.Absolute,
                        right: 0,
                        bottom: 0,
                        id: "corner"
                    );
            }
        }
    }
}
