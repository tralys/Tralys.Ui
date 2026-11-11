using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class PanZoomTests
{
    [Test]
    public async Task A_click_lands_where_the_zoom_draws_a_node_and_a_mouse_drag_from_it_pans_and_clicks_nothing()
    {
        var view = new PanFixture
        {
            Width = 280f,
            Height = 240f,
            DefaultZoom = 2f,
            WithButton = true,
        };
        using var driver = view.Render();
        var root = driver.Get("pz");

        driver.Click(new Vector2(root.ViewBounds.X + 140f, root.ViewBounds.Y + 120f));
        var clicked = view.Clicks;
        var mouse = driver.Hold(root);
        mouse.MoveBy(new Vector2(-30f, -20f));
        mouse.Release();

        await Assert.That(clicked).IsEqualTo(1);
        await Assert.That(view.Offset(driver)).IsEqualTo(new Vector2(30f, 20f));
        await Assert.That(view.Clicks).IsEqualTo(1);
    }

    [Test]
    public async Task Large_content_pans_to_its_boundary_margin_and_small_content_sits_centred_where_a_pan_leaves_it()
    {
        var view = new PanFixture { Margin = 20f };
        using var driver = view.Render();
        var root = driver.Get("pz");

        Pan(driver, root, new Vector2(1000f, -1000f));
        var leftDown = view.Offset(driver);
        Pan(driver, root, new Vector2(-1000f, 1000f));
        var rightUp = view.Offset(driver);
        view.ContentWidth = 100f;
        view.ContentHeight = 50f;
        view.Margin = default;
        driver.Rerender();
        var centred = view.Offset(driver);
        Pan(driver, root, new Vector2(-40f, -40f));

        await Assert.That(leftDown).IsEqualTo(new Vector2(-20f, 270f));
        await Assert.That(rightUp).IsEqualTo(new Vector2(420f, -20f));
        await Assert.That(centred).IsEqualTo(new Vector2(-50f, -50f));
        await Assert.That(view.Offset(driver)).IsEqualTo(centred);
    }

    static void Pan(UiDriver driver, Node from, Vector2 by)
    {
        var mouse = driver.Hold(from);
        mouse.MoveBy(by, steps: 50);
        mouse.Release();
    }

    [Test]
    public async Task The_wheel_zooms_by_a_tenth_a_notch_about_the_pointer_and_at_its_limit_scrolls_nothing_around_it()
    {
        var outer = new ScrollState();
        var view = new PanFixture { Around = outer };
        using var driver = view.Render();
        var root = driver.Get("pz");

        driver.Wheel(root, new Vector2(0f, 1f));
        driver.Wheel(root, new Vector2(0f, 1f));
        var twice = (view.Zooms[^1], view.Offset(driver));
        driver.Wheel(root, new Vector2(0f, -1f));
        var back = view.Zooms[^1];
        for (var i = 0; i < 12; i++)
            driver.Wheel(root, new Vector2(0f, -1f));
        var least = view.Zooms[^1];
        var reports = view.Zooms.Count;
        driver.Wheel(root, new Vector2(0f, -1f));
        driver.Wheel(root, new Vector2(0f, 0.5f));

        await Assert.That(twice.Item1).IsEqualTo(1.21f).Within(0.0001f);
        await Assert.That(twice.Item2.X).IsEqualTo(21f).Within(0.5f);
        await Assert.That(twice.Item2.Y).IsEqualTo(15.75f).Within(0.5f);
        await Assert.That(back).IsEqualTo(1.1f).Within(0.0001f);
        await Assert.That(least).IsEqualTo(0.5f);
        await Assert.That(view.Zooms.Count).IsEqualTo(reports + 1);
        await Assert.That(view.Zooms[^1]).IsEqualTo(0.5f * MathF.Sqrt(1.1f)).Within(0.0001f);
        await Assert.That(outer.Outer!.Offset).IsEqualTo(Vector2.Zero);
    }

    [Test]
    public async Task A_zoom_outside_the_range_shows_the_nearer_end_until_the_range_takes_it_in()
    {
        var view = new PanFixture { Zoom = 5f };
        using var driver = view.Render();
        var clamped = view.Shown(driver);

        view.MaxZoom = 6f;
        driver.Rerender();
        var taken = view.Shown(driver);
        view.MinZoom = 1f;
        view.MaxZoom = 0.5f;
        view.Zoom = 0.75f;
        driver.Rerender();

        await Assert.That(clamped).IsEqualTo(2f);
        await Assert.That(taken).IsEqualTo(5f);
        await Assert.That(view.Shown(driver)).IsEqualTo(1f);
        await Assert.That(view.Zooms).IsEmpty();
    }

    [Test]
    public async Task Two_fingers_zoom_about_their_midpoint_pan_with_it_and_the_one_left_pans_on_without_a_jump()
    {
        var view = new PanFixture
        {
            Width = 300f,
            Height = 200f,
            WithMarks = true,
        };
        using var driver = view.Render();
        var root = driver.Get("pz").ViewBounds;

        var left = driver.Hold(driver.Get("left"), PointerKind.Touch);
        var right = driver.Hold(driver.Get("right"), PointerKind.Touch);
        left.MoveBy(new Vector2(-20f, 0f));
        right.MoveBy(new Vector2(20f, 0f));
        var spread = (view.Shown(driver), view.Offset(driver));
        left.MoveBy(new Vector2(10f, 10f));
        right.MoveBy(new Vector2(10f, 10f));
        var together = (view.Shown(driver), view.Offset(driver));
        left.Release();
        right.MoveBy(new Vector2(-5f, 0f));

        await Assert.That(spread.Item1).IsEqualTo(2f);
        await Assert.That(spread.Item2.X).IsEqualTo(150f).Within(0.5f);
        await Assert.That(spread.Item2.Y).IsEqualTo(100f).Within(0.5f);
        await Assert.That(together.Item1).IsEqualTo(2f);
        await Assert.That(together.Item2).IsEqualTo(spread.Item2 - new Vector2(10f, 10f));
        await Assert.That(view.Offset(driver)).IsEqualTo(together.Item2 + new Vector2(5f, 0f));
        await Assert.That(root).IsEqualTo(driver.Get("pz").ViewBounds);
    }

    [Test]
    public async Task A_finger_lifted_while_panning_coasts_and_slows_where_a_mouse_stops_and_a_press_stops_a_coast()
    {
        var view = new PanFixture { ContentWidth = 4000f };
        using var driver = view.Render();
        var root = driver.Get("pz");

        var mouse = driver.Hold(root, settle: false);
        mouse.MoveBy(new Vector2(-60f, 0f), steps: 4, seconds: 0.1f, settle: false);
        mouse.Release(settle: false);
        driver.Frame(0.5f);
        var stopped = view.Offset(driver).X;
        driver.Fling(root, new Vector2(-60f, 0f), settle: false);
        var lifted = view.Offset(driver).X;
        for (var i = 0; i < 2; i++)
            driver.Frame();
        var early = view.Offset(driver).X;
        for (var i = 0; i < 6; i++)
            driver.Frame();
        var fast = view.Offset(driver).X - early;
        for (var i = 0; i < 12; i++)
            driver.Frame();
        var later = view.Offset(driver).X;
        for (var i = 0; i < 6; i++)
            driver.Frame();
        var slow = view.Offset(driver).X - later;
        driver.Tap(root, settle: false);
        var tapped = view.Offset(driver).X;
        driver.Frame(0.5f);

        await Assert.That(stopped).IsEqualTo(60f);
        await Assert.That(early).IsGreaterThan(lifted);
        await Assert.That(slow).IsGreaterThan(0f).And.IsLessThan(fast);
        await Assert.That(view.Offset(driver).X).IsEqualTo(tapped);
    }

    [Test]
    public async Task Settling_after_a_fling_waits_out_the_coast()
    {
        var view = new PanFixture { ContentWidth = 4000f };
        using var driver = view.Render();

        driver.Fling(driver.Get("pz"), new Vector2(-40f, 0f));
        var settled = view.Offset(driver).X;
        driver.Frame(5f);

        await Assert.That(settled).IsGreaterThan(40f);
        await Assert.That(view.Offset(driver).X).IsEqualTo(settled);
    }

    [Test]
    public async Task A_cancelled_finger_whose_lift_the_view_never_hears_leaves_the_next_one_to_pan_alone()
    {
        var view = new PanFixture { Outside = true };
        using var driver = view.Render();
        var root = driver.Get("pz");

        for (var i = 0; i < 2; i++)
        {
            var other = driver.Hold(driver.Get("outside"), PointerKind.Touch);
            driver.Hold(root, PointerKind.Touch).Cancel();
            other.Release();
            driver.Drag(root, new Vector2(-40f, 0f), PointerKind.Touch);
        }

        await Assert.That(view.Offset(driver)).IsEqualTo(new Vector2(80f, 0f));
        await Assert.That(view.Zooms).IsEmpty();
    }

    [Test]
    public async Task A_second_finger_beside_a_thumb_or_a_drag_inside_pans_alone()
    {
        var view = new PanFixture { WithThumb = true, WithDrag = true };
        using var driver = view.Render();
        var root = driver.Get("pz");

        var thumb = driver.Hold(driver.Get("thumb"), PointerKind.Touch);
        thumb.MoveBy(new Vector2(10f, 0f));
        var second = driver.Hold(root, PointerKind.Touch);
        second.MoveBy(new Vector2(-60f, -40f));
        thumb.MoveBy(new Vector2(10f, 0f));
        var beside = view.Offset(driver);
        driver.Frame(0.2f);
        second.Release();
        thumb.Release();
        var item = driver.Hold(driver.Get("item"), PointerKind.Touch);
        driver.Frame(0.6f);
        item.MoveBy(new Vector2(0f, 20f));
        var another = driver.Hold(root, PointerKind.Touch);
        another.MoveBy(new Vector2(-40f, 0f));
        var across = view.Offset(driver) - beside;
        var dropping = view.Dropped;
        driver.Frame(0.2f);
        another.Release();
        item.Release();

        await Assert.That(view.Dragged).IsEqualTo(beside + new Vector2(20f, 0f));
        await Assert.That(beside).IsEqualTo(new Vector2(60f, 40f));
        await Assert.That(across).IsEqualTo(new Vector2(40f, 0f));
        await Assert.That(view.Zooms).IsEmpty();
        await Assert.That(dropping).IsFalse();
        await Assert.That(view.Dropped).IsTrue();
    }

    [Test]
    public async Task With_the_focus_inside_keys_and_shoulders_zoom_about_the_middle_and_the_left_stick_pans_while_held()
    {
        var view = new PanFixture { WithButton = true };
        using var driver = view.Render();
        driver.Focus(driver.Get("button"));
        var zooms = new List<float>();

        driver.Press(Key.OemPlus, ModifierKeys.Control);
        zooms.Add(view.Shown(driver));
        driver.Press(Key.Subtract, ModifierKeys.Control);
        zooms.Add(view.Shown(driver));
        driver.Press(GamepadButton.RightShoulder);
        zooms.Add(view.Shown(driver));
        var middle = view.Offset(driver);
        driver.Press(GamepadButton.LeftShoulder);
        zooms.Add(view.Shown(driver));
        driver.Stick(GamepadStick.Left, new Vector2(1f, 0f), settle: false);
        driver.Frame(0.25f);
        driver.Frame(0.25f);
        var held = view.Offset(driver).X;
        driver.Stick(GamepadStick.Left, Vector2.Zero, settle: false);
        driver.Frame(0.5f);
        var centred = view.Offset(driver).X;
        driver.Stick(GamepadStick.Left, new Vector2(0.2f, 0f), settle: false);
        driver.Frame(0.5f);
        var tilted = view.Offset(driver).X;
        driver.Stick(GamepadStick.Left, new Vector2(1f, 0f), settle: false);
        driver.Focus(driver.Get("outside"), settle: false);
        driver.Frame(0.25f);
        driver.Frame(0.25f);

        await Assert.That(zooms).IsEquivalentTo([1.25f, 1f, 1.25f, 1f]);
        await Assert.That(middle.X).IsEqualTo(25f).Within(0.5f);
        await Assert.That(middle.Y).IsEqualTo(18.75f).Within(0.5f);
        await Assert.That(held).IsEqualTo(300f).Within(1f);
        await Assert.That(centred).IsEqualTo(held);
        await Assert.That(tilted).IsEqualTo(held);
        await Assert.That(view.Offset(driver).X).IsEqualTo(held);
    }

    [Test]
    public async Task Keyboard_focus_outside_the_view_pans_it_just_into_view_while_pointer_focus_does_not()
    {
        var view = new PanFixture { WithButton = true, WithFar = true };
        using var driver = view.Render();

        driver.Tab();
        driver.Tab();
        var inside = (driver.Focused?.Id, view.Offset(driver));
        driver.Click(driver.Get("partial"));
        var clicked = (driver.Focused?.Id, view.Offset(driver));
        driver.Tab();

        await Assert.That(inside).IsEqualTo(("button", Vector2.Zero));
        await Assert.That(clicked).IsEqualTo(("partial", Vector2.Zero));
        await Assert.That(driver.Focused?.Id).IsEqualTo("far");
        await Assert.That(view.Offset(driver)).IsEqualTo(new Vector2(340f, 170f));
    }

    [Test]
    public async Task Rows_focused_by_keys_in_a_popup_past_its_edge_leave_the_view_where_it_is()
    {
        var picked = new Picked();
        var view = new PanFixture
        {
            Width = 240f,
            Height = 60f,
            Picked = picked,
        };
        using var driver = view.Render();

        driver.Tab();
        driver.Tab();
        for (var i = 0; i < 6; i++)
            driver.Press(Key.Down);

        await Assert.That(picked.Opens).IsEquivalentTo([true]);
        await Assert.That(view.Offset(driver)).IsEqualTo(Vector2.Zero);
    }

    [Test]
    public async Task A_scroll_area_inside_takes_the_finger_and_the_wheel_it_can_scroll_and_leaves_the_rest_to_the_view()
    {
        var inner = new ScrollState();
        var view = new PanFixture { Inner = inner };
        using var driver = view.Render();
        var list = driver.Get("list");

        driver.Drag(list, new Vector2(0f, -30f), PointerKind.Touch);
        var along = (inner.Outer!.Offset, view.Offset(driver));
        driver.Drag(list, new Vector2(-30f, 0f), PointerKind.Touch);
        var across = (inner.Outer.Offset, view.Offset(driver));
        driver.Wheel(list, new Vector2(0f, -1f));

        await Assert.That(along).IsEqualTo((new Vector2(0f, 30f), Vector2.Zero));
        await Assert.That(across).IsEqualTo((new Vector2(0f, 30f), new Vector2(30f, 0f)));
        await Assert.That(inner.Outer.Offset).IsEqualTo(new Vector2(0f, 78f));
        await Assert.That(view.Zooms).IsEmpty();
    }

    [Test]
    public async Task A_finger_pans_it_before_a_scroll_area_around_it_and_a_second_finger_takes_the_first_into_a_pinch()
    {
        var outer = new ScrollState();
        var view = new PanFixture { Around = outer };
        using var driver = view.Render();
        var root = driver.Get("pz");

        var turning = driver.Hold(root, PointerKind.Touch);
        turning.MoveBy(new Vector2(-40f, 0f));
        turning.MoveBy(new Vector2(0f, -40f));
        driver.Frame(0.2f);
        turning.Release();
        var turned = (view.Offset(driver), outer.Outer!.Offset);
        driver.Drag(root, new Vector2(0f, -300f), PointerKind.Touch);
        var dragged = (view.Offset(driver), outer.Outer.Offset);
        var first = driver.Hold(root, PointerKind.Touch);
        first.MoveBy(new Vector2(-30f, 0f));
        var before = view.Offset(driver);
        var second = driver.Hold(first.Position + new Vector2(40f, 0f), PointerKind.Touch);
        var bounds = driver.Get("pz").ViewBounds;
        var middle = first.Position + new Vector2(20f, 0f) - new Vector2(bounds.X, bounds.Y);
        first.MoveBy(new Vector2(-20f, 0f));
        second.MoveBy(new Vector2(20f, 0f));
        var pinched = view.Offset(driver);
        first.Release();
        second.Release();

        await Assert.That(turned).IsEqualTo((new Vector2(40f, 40f), Vector2.Zero));
        await Assert.That(dragged).IsEqualTo((new Vector2(40f, 250f), new Vector2(0f, 90f)));
        await Assert.That(before).IsEqualTo(new Vector2(70f, 250f));
        await Assert.That(view.Shown(driver)).IsEqualTo(2f);
        await Assert.That(pinched.X).IsEqualTo((before.X + middle.X) * 2f - middle.X).Within(0.5f);
        await Assert.That(pinched.Y).IsEqualTo((before.Y + middle.Y) * 2f - middle.Y).Within(0.5f);
        await Assert.That(outer.Outer.Offset).IsEqualTo(new Vector2(0f, 90f));
    }

    [Test]
    public async Task A_mouse_drag_pans_it_before_a_drag_to_pan_scroll_area_around_it()
    {
        var outer = new ScrollState();
        var view = new PanFixture { Around = outer, DragToPan = true };
        using var driver = view.Render();

        driver.Drag(driver.Get("pz"), new Vector2(-30f, -40f));

        await Assert.That(view.Offset(driver)).IsEqualTo(new Vector2(30f, 40f));
        await Assert.That(outer.Outer!.Offset).IsEqualTo(Vector2.Zero);
    }

    [Test]
    public async Task Code_pans_it_through_the_handle_it_is_given_from_before_it_first_shows()
    {
        var view = new PanFixture { WithHandle = true, StartAt = new Vector2(30f, 20f) };
        using var driver = view.Render();
        var started = view.Offset(driver);

        view.Handle!.ScrollBy(new Vector2(-10f, 30f));
        driver.Settle();

        await Assert.That(started).IsEqualTo(new Vector2(30f, 20f));
        await Assert.That(view.Offset(driver)).IsEqualTo(new Vector2(20f, 50f));
        await Assert.That(view.Handle.Offset).IsEqualTo(new Vector2(20f, 50f));
    }

    [Test]
    public async Task Code_zooms_it_about_the_point_it_names()
    {
        var view = new PanFixture
        {
            Zoom = 1f,
            WithHandle = true,
            StartAt = new Vector2(30f, 20f),
        };
        using var driver = view.Render();

        view.Zoom = 2f;
        view.ZoomAbout = new Vector2(40f, 30f);
        driver.Rerender();

        await Assert.That(view.Offset(driver)).IsEqualTo(new Vector2(100f, 70f));
    }

    [Test]
    public async Task A_pan_or_a_zoom_lays_out_none_of_the_content()
    {
        var view = new PanFixture { WithGrid = true };
        using var driver = view.Render();
        var root = driver.Get("pz");

        var mouse = driver.Hold(root, settle: false);
        mouse.MoveBy(new Vector2(-30f, -30f), settle: false);
        driver.Frame();
        var panned = driver.Stats.Layouts;
        mouse.Release();
        driver.Wheel(root, new Vector2(0f, 1f), settle: false);
        driver.Frame();
        var zoomed = driver.Stats.Layouts;

        await Assert.That(panned).IsLessThan(PanFixture.Cells);
        await Assert.That(zoomed).IsLessThan(PanFixture.Cells);
    }
}

/// <summary>A <see cref="PanZoom"/> over content of a set size, with optional parts inside it.</summary>
internal sealed class PanFixture
{
    public const int Cells = 40;

    public readonly List<float> Zooms = [];
    public float? Zoom;
    public Vector2? ZoomAbout;
    public float? DefaultZoom;
    public float? MinZoom;
    public float? MaxZoom;
    public Thickness Margin;
    public float Width = 200f;
    public float Height = 150f;
    public float ContentWidth = 600f;
    public float ContentHeight = 400f;
    public bool Outside;
    public bool WithButton;
    public bool WithMarks;
    public bool WithThumb;
    public bool WithDrag;
    public bool WithFar;
    public bool WithGrid;
    public bool WithHandle;
    public Vector2? StartAt;
    public ScrollHandle? Handle;
    public ScrollState? Around;
    public bool DragToPan;
    public ScrollState? Inner;
    public Picked? Picked;
    public int Clicks;
    public Vector2 Dragged;
    public bool Dropped;

    public UiDriver Render() =>
        UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                {
                    using (ui.Box(width: 40, height: 20, focusable: true, id: "outside")) { }
                    if (Around is { } around)
                    {
                        around.Outer = ui.ScrollHandle();
                        using (
                            ui.ScrollArea(
                                scroll: around.Outer,
                                dragToPan: DragToPan,
                                width: 300,
                                height: 200
                            )
                        )
                        using (ui.Box(direction: Direction.Column, height: 1000))
                            View(ui);
                    }
                    else
                        View(ui);
                }
            },
            new Size(600, 500)
        );

    [FramePath]
    void View(Ui ui, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        using var fragment = ui.Fragment(file: file, line: line);
        Handle = WithHandle ? ui.ScrollHandle() : null;
        if (Handle is not null && StartAt is { } at)
        {
            Handle.ScrollTo(at.X, at.Y);
            StartAt = null;
        }
        using (
            ui.PanZoom(
                zoom: Zoom,
                zoomAbout: ZoomAbout,
                defaultZoom: DefaultZoom,
                onZoomChange: Callback<float>.Of(Zooms, static (z, zoom) => z.Add(zoom)),
                minZoom: MinZoom,
                maxZoom: MaxZoom,
                boundaryMargin: Margin,
                scroll: Handle,
                width: Width,
                height: Height,
                id: "pz"
            )
        )
        using (
            ui.Box(align: Align.Start, width: ContentWidth, height: ContentHeight, id: "content")
        )
            Parts(ui);
    }

    [FramePath]
    void Parts(Ui ui, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        using var fragment = ui.Fragment(file: file, line: line);
        if (WithButton)
            using (
                ui.Box(
                    width: 40,
                    height: 20,
                    position: Position.Absolute,
                    left: 50,
                    top: 50,
                    onClick: Callback.Of(this, static v => v.Clicks++),
                    id: "button"
                )
            ) { }
        if (WithFar)
        {
            using (
                ui.Box(
                    width: 40,
                    height: 20,
                    position: Position.Absolute,
                    left: 180,
                    top: 60,
                    focusable: true,
                    id: "partial"
                )
            ) { }
            using (
                ui.Box(
                    width: 40,
                    height: 20,
                    position: Position.Absolute,
                    left: 500,
                    top: 300,
                    focusable: true,
                    id: "far"
                )
            ) { }
        }
        if (WithMarks)
        {
            using (
                ui.Box(
                    width: 10,
                    height: 10,
                    position: Position.Absolute,
                    left: 125,
                    top: 95,
                    id: "left"
                )
            ) { }
            using (
                ui.Box(
                    width: 10,
                    height: 10,
                    position: Position.Absolute,
                    left: 165,
                    top: 95,
                    id: "right"
                )
            ) { }
        }
        if (WithThumb)
            using (
                ui.Thumb(
                    onDragDelta: Callback<Vector2>.Of(this, static (v, by) => v.Dragged = by),
                    width: 30,
                    height: 30,
                    position: Position.Absolute,
                    left: 10,
                    top: 10,
                    id: "thumb"
                )
            ) { }
        if (WithDrag)
            using (
                ui.Box(
                    width: 30,
                    height: 30,
                    position: Position.Absolute,
                    left: 150,
                    top: 100,
                    background: Color.FromRgb(1, 2, 3),
                    dragData: this,
                    onDragEnd: Callback<DragEffect>.Of(this, static (v, _) => v.Dropped = true),
                    id: "item"
                )
            ) { }
        if (Inner is { } inner)
        {
            inner.Outer = ui.ScrollHandle();
            using (
                ui.ScrollArea(
                    scroll: inner.Outer,
                    horizontal: ScrollbarVisibility.Disabled,
                    width: 100,
                    height: 100
                )
            )
            using (ui.Box(height: 500, id: "list")) { }
        }
        if (Picked is { } picked)
            ui.Combobox(
                items: Fruits.Items,
                itemKey: Fruits.ByName,
                itemText: Fruits.Named,
                value: picked.Value,
                onValueChange: picked.OnValueChange,
                onOpenChange: picked.OnOpenChange,
                width: 200
            );
        if (WithGrid)
            for (var i = 0; i < Cells; i++)
                using (ui.Box(width: 20, height: 20, key: i)) { }
    }

    /// <summary>How far the view's left and top edges sit past the zoomed content's.</summary>
    public Vector2 Offset(UiDriver driver)
    {
        var view = driver.Get("pz").ViewBounds;
        var content = driver.Get("content").ViewBounds;
        return new Vector2(view.X - content.X, view.Y - content.Y);
    }

    /// <summary>The zoom it shows: the zoomed content's width over its own.</summary>
    public float Shown(UiDriver driver) =>
        driver.Get("content").Parent!.Parent!.ViewBounds.Width / ContentWidth;
}
