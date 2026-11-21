using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;
using TUnit.Assertions.Enums;

namespace Tralys.Ui.Tests;

public sealed class ResizeHandleTests
{
    static Paint Dark(ColorToken token) => Themes.Dark.Color(token);

    [Test]
    public async Task A_drag_trades_the_width_of_the_column_before_the_handle_with_the_one_after_on_every_move_stopping_at_the_range_and_landing_on_the_drag_step()
    {
        var split = new SplitBoard { Value = 150f, Max = 240f };
        using var driver = UiDriver.Render(ui => split.Render(ui));

        var mouse = driver.Hold(driver.Get("handle"));
        mouse.MoveBy(new Vector2(50f, 0f));
        var traded = Widths(driver);
        mouse.MoveBy(new Vector2(60f, 0f));
        var stopped = Widths(driver);
        mouse.Release();
        split.DragStep = 10f;
        driver.Rerender();
        driver.Drag(driver.Get("handle"), new Vector2(-23f, 0f));

        await Assert.That(traded).IsEqualTo((200f, 100f));
        await Assert.That(stopped).IsEqualTo((240f, 60f));
        await Assert.That(Widths(driver)).IsEqualTo((220f, 80f));
    }

    [Test]
    public async Task A_handle_on_the_left_edge_of_the_column_after_it_narrows_that_column_as_it_moves_right_by_drag_or_key()
    {
        var split = new SplitBoard { Value = 100f, Edge = ResizeEdge.Left };
        using var driver = UiDriver.Render(ui => split.Render(ui));

        driver.Drag(driver.Get("handle"), new Vector2(20f, 0f));
        var dragged = driver.Get("second").Bounds.Width;
        driver.Press(Key.Right);

        await Assert.That(dragged).IsEqualTo(80f);
        await Assert.That(driver.Get("second").Bounds.Width).IsEqualTo(70f);
        await Assert.That(driver.Get("first").Bounds.Width).IsEqualTo(230f);
    }

    [Test]
    public async Task Tab_reaches_the_handle_whose_arrows_along_its_axis_step_it_while_those_across_and_escape_pass_on()
    {
        var columns = new SplitBoard { Value = 100f, Step = 15f };
        var rows = new SplitBoard
        {
            Value = 100f,
            Step = 15f,
            Edge = ResizeEdge.Bottom,
        };
        using var across = UiDriver.Render(ui => columns.Render(ui));
        using var down = UiDriver.Render(ui => rows.Render(ui));
        across.Tab();
        down.Tab();

        var widths = new List<float>();
        var escaped = across.KeyDown(Key.Escape);
        foreach (var key in (Key[])[Key.Right, Key.Left])
        {
            across.Press(key);
            widths.Add(across.Get("first").Bounds.Width);
        }
        var up = across.KeyDown(Key.Up);
        var heights = new List<float>();
        foreach (var key in (Key[])[Key.Down, Key.Up, Key.Up])
        {
            down.Press(key);
            heights.Add(down.Get("first").Bounds.Height);
        }

        await Assert.That(across.Focused).IsSameReferenceAs(across.Get("handle"));
        await Assert.That((escaped, up)).IsEqualTo((false, false));
        await Assert.That(widths).IsEquivalentTo([115f, 100f], CollectionOrdering.Matching);
        await Assert.That(across.Get("first").Bounds.Width).IsEqualTo(100f);
        await Assert.That(heights).IsEquivalentTo([115f, 100f, 85f], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Escape_mid_drag_puts_the_width_back_for_every_column_a_later_arrow_and_the_release_move_nothing_and_nothing_commits()
    {
        var split = new SplitBoard
        {
            Value = 200f,
            Size = 606f,
            Third = true,
        };
        using var driver = UiDriver.Render(ui => split.Render(ui));

        var mouse = driver.Hold(driver.Get("handle"));
        mouse.MoveBy(new Vector2(20f, 0f));
        var dragged = Widths(driver);
        driver.Press(Key.Right);
        var arrowed = driver.Get("first").Bounds.Width;
        var escape = driver.KeyDown(Key.Escape);
        mouse.MoveBy(new Vector2(10f, 0f));
        mouse.Release();

        await Assert.That(dragged).IsEqualTo((220f, 190f));
        await Assert.That((arrowed, escape)).IsEqualTo((220f, true));
        await Assert
            .That(
                (
                    driver.Get("first").Bounds.Width,
                    driver.Get("second").Bounds.Width,
                    driver.Get("third").Bounds.Width
                )
            )
            .IsEqualTo((200f, 200f, 200f));
        await Assert.That(split.Commits).IsEqualTo(0);
    }

    [Test]
    public async Task Tabbing_away_mid_drag_puts_the_width_back()
    {
        var split = new SplitBoard { Value = 100f, Next = true };
        using var driver = UiDriver.Render(ui => split.Render(ui));

        var mouse = driver.Hold(driver.Get("handle"));
        mouse.MoveBy(new Vector2(20f, 0f));
        var dragged = driver.Get("first").Bounds.Width;
        driver.Press(Key.Tab);
        mouse.Release();

        await Assert.That(dragged).IsEqualTo(120f);
        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("next"));
        await Assert.That(driver.Get("first").Bounds.Width).IsEqualTo(100f);
    }

    [Test]
    public async Task Only_an_arrow_or_a_drag_that_moved_and_was_not_cancelled_commits_once()
    {
        var split = new SplitBoard { Value = 100f, Max = 100f };
        using var driver = UiDriver.Render(ui => split.Render(ui));
        var handle = driver.Get("handle");

        driver.Click(handle);
        driver.Press(Key.Right);
        var unmoved = split.Commits;
        var lost = driver.Hold(handle);
        lost.MoveBy(new Vector2(-20f, 0f));
        lost.LoseRelease();
        var cancelled = (split.Commits, driver.Get("first").Bounds.Width);
        driver.Drag(handle, new Vector2(-20f, 0f));
        var dragged = split.Commits;
        driver.Press(Key.Left);

        await Assert.That(unmoved).IsEqualTo(0);
        await Assert.That(cancelled).IsEqualTo((0, 100f));
        await Assert.That(dragged).IsEqualTo(1);
        await Assert.That(split.Commits).IsEqualTo(2);
        await Assert.That(split.Committed).IsEquivalentTo([80f, 70f], CollectionOrdering.Matching);
    }

    [Test]
    public async Task A_preview_moves_only_a_ghost_in_the_focus_colour_that_takes_no_hits_mid_drag_within_the_range_and_resizes_on_release()
    {
        var split = new SplitBoard
        {
            Value = 100f,
            Edge = ResizeEdge.Bottom,
            Preview = true,
            Max = 140f,
        };
        using var driver = UiDriver.Render(ui => split.Render(ui));
        var handle = driver.Get("handle");
        var top = handle.ViewBounds.Y;
        driver.Hover(handle);
        var cursor = driver.Cursor;

        var mouse = driver.Hold(handle);
        mouse.MoveBy(new Vector2(0f, 30f));
        var midDrag = driver.Get("first").Bounds.Height;
        var shade = Shade(driver);
        var shaded = (shade.ViewBounds, shade.Visual.Background);
        var hit = driver.View.HitTest(Centre(shade.ViewBounds));
        mouse.MoveBy(new Vector2(0f, 100f));
        var held = Shade(driver).ViewBounds.Y - top;
        mouse.MoveBy(new Vector2(0f, -100f));
        mouse.Release();
        var released = (driver.Get("first").Bounds.Height, driver.View.Layers.Length);
        var lost = driver.Hold(handle);
        lost.MoveBy(new Vector2(0f, 20f));
        lost.LoseRelease();

        await Assert.That(cursor).IsSameReferenceAs(Cursors.SizeNs);
        await Assert.That(midDrag).IsEqualTo(100f);
        await Assert
            .That(shaded)
            .IsEqualTo(
                (
                    new Rect(handle.ViewBounds.X, top + 30f, 400f, 6f),
                    (Paint)Themes.Dark.Color(ColorToken.BorderFocus).WithOpacity(0.5f)
                )
            );
        await Assert.That(hit).IsNotSameReferenceAs(shade);
        await Assert.That(held).IsEqualTo(40f);
        await Assert.That(released).IsEqualTo((130f, 0));
        await Assert.That(driver.Get("first").Bounds.Height).IsEqualTo(130f);
        await Assert.That(split.Commits).IsEqualTo(1);
    }

    [Test]
    public async Task A_bar_shows_its_edges_resize_cursor_and_lights_from_the_border_colour_to_its_hover_and_to_the_focus_colour_while_dragged_or_focused_by_keys_and_a_ghost_shows_only_under_the_pointer()
    {
        var split = new SplitBoard { Value = 100f };
        var ghostly = new SplitBoard { Value = 100f, Variant = ResizeHandleVariant.Ghost };
        using var driver = UiDriver.Render(ui => split.Render(ui));
        using var ghost = UiDriver.Render(ui => ghostly.Render(ui));
        var handle = driver.Get("handle");
        var faint = ghost.Get("handle").Visual.Background;
        ghost.Hover(ghost.Get("handle"));

        var rest = handle.Visual.Background;
        driver.Hover(handle);
        var hovered = (handle.Visual.Background, driver.Cursor);
        var mouse = driver.Hold(handle);
        mouse.MoveBy(new Vector2(10f, 0f));
        var dragged = handle.Visual.Background;
        mouse.Release();
        driver.Unhover();
        var released = handle.Visual.Background;
        driver.Tab(shift: true);
        driver.Tab();

        await Assert.That(rest).IsEqualTo(Dark(ColorToken.Border));
        await Assert
            .That((faint, ghost.Get("handle").Visual.Background))
            .IsEqualTo(
                (
                    (Paint)Themes.Dark.Color(ColorToken.Border).WithOpacity(0f),
                    Dark(ColorToken.Border)
                )
            );
        await Assert.That(hovered).IsEqualTo((Dark(ColorToken.BorderHover), Cursors.SizeWe));
        await Assert.That(dragged).IsEqualTo(Dark(ColorToken.BorderFocus));
        await Assert.That(released).IsEqualTo(Dark(ColorToken.Border));
        await Assert.That(handle.Visual.Background).IsEqualTo(Dark(ColorToken.BorderFocus));
    }

    [Test]
    public async Task Each_window_edge_and_corner_shows_its_resize_cursor_and_lights_a_line_along_its_edges_under_the_pointer_and_while_dragged()
    {
        var cursors = new List<Cursor>();
        using var driver = UiDriver.Render(static ui => EdgeBoard(ui, inward: false, band: 0f));
        foreach (var edge in Enum.GetValues<ResizeEdge>())
        {
            driver.Hover(driver.Get(edge.ToString()));
            cursors.Add(await Assert.That(driver.Cursor).IsNotNull());
        }
        driver.Unhover();
        var right = driver.Get(nameof(ResizeEdge.Right));
        var lit = Lit(right);
        var rest = Opacity(lit);
        driver.Hover(right);
        var hovered = (Opacity(lit), lit.Layout.Border, lit.Visual.BorderPaint);
        var mouse = driver.Hold(right);
        mouse.MoveBy(new Vector2(-100f, 0f));
        var outrun = Opacity(lit);
        mouse.Release();
        driver.Unhover();

        await Assert
            .That(cursors)
            .IsEquivalentTo(
                [
                    Cursors.SizeWe,
                    Cursors.SizeWe,
                    Cursors.SizeNs,
                    Cursors.SizeNs,
                    Cursors.SizeNwSe,
                    Cursors.SizeNeSw,
                    Cursors.SizeNeSw,
                    Cursors.SizeNwSe,
                ],
                CollectionOrdering.Matching
            );
        await Assert.That(rest).IsEqualTo(0f);
        await Assert
            .That(hovered)
            .IsEqualTo((1f, new Thickness(0f, 0f, 2f, 0f), Dark(ColorToken.BorderAccent)));
        await Assert.That(outrun).IsEqualTo(1f);
        await Assert.That(Opacity(lit)).IsEqualTo(0f);
    }

    [Test]
    public async Task A_handle_lit_inward_draws_its_line_on_the_inner_side_of_its_band_and_bottom_right_ridges_brighten_under_the_pointer()
    {
        using var driver = UiDriver.Render(static ui => EdgeBoard(ui, inward: true, band: 8f));
        var right = driver.Get(nameof(ResizeEdge.Right));
        var corner = driver.Get(nameof(ResizeEdge.BottomRight));
        var ridges = Nodes.Find(corner, NodeKind.Canvas);

        var dim = Opacity(ridges);
        driver.Hover(corner);

        await Assert.That(Lit(right).ViewBounds).IsEqualTo(Inset(right.ViewBounds, right: 6f));
        await Assert.That((dim, Opacity(ridges))).IsEqualTo((0.6f, 1f));
    }

    [Test]
    public async Task A_corner_with_a_band_takes_the_pointer_only_along_its_two_edges_and_one_without_over_all_of_it()
    {
        using var driver = UiDriver.Render(static ui =>
        {
            using (ui.Box(gap: 20, align: Align.Start))
            {
                ui.ResizeHandle(
                    edge: ResizeEdge.BottomRight,
                    variant: ResizeHandleVariant.Edge,
                    band: 8,
                    width: 32,
                    height: 32,
                    id: "band"
                );
                ui.ResizeHandle(
                    edge: ResizeEdge.BottomRight,
                    variant: ResizeHandleVariant.Edge,
                    width: 32,
                    height: 32,
                    id: "whole"
                );
            }
        });
        var band = driver.Get("band");
        var whole = driver.Get("whole");

        await Assert.That(band.IsAncestorOrSelfOf(At(driver, band, 4f, 4f))).IsFalse();
        await Assert.That(band.IsAncestorOrSelfOf(At(driver, band, 28f, 4f))).IsTrue();
        await Assert.That(band.IsAncestorOrSelfOf(At(driver, band, 4f, 28f))).IsTrue();
        await Assert.That(whole.IsAncestorOrSelfOf(At(driver, whole, 4f, 4f))).IsTrue();
    }

    static (float, float) Widths(UiDriver driver) =>
        (driver.Get("first").Bounds.Width, driver.Get("second").Bounds.Width);

    static Node Shade(UiDriver driver)
    {
        var layers = driver.View.Layers;
        return Nodes.Find(layers[^1], NodeKind.Box);
    }

    static Node Lit(Node handle)
    {
        foreach (var child in handle.Children)
        {
            if (child.Kind == NodeKind.Box)
                return child;
        }
        throw new InvalidOperationException("The handle lights no line.");
    }

    static float Opacity(Node node) => node.Visual.Opacity ?? 1f;

    static Node? At(UiDriver driver, Node node, float x, float y) =>
        driver.View.HitTest(new Vector2(node.ViewBounds.X + x, node.ViewBounds.Y + y));

    static Vector2 Centre(Rect rect) => new(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);

    static Rect Inset(Rect rect, float right) =>
        new(rect.X, rect.Y, rect.Width - right, rect.Height);

    [FramePath]
    static void EdgeBoard(
        Ui ui,
        bool inward,
        float band,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Grid(width: 240, height: 160, file: file, line: line))
            foreach (var edge in Enum.GetValues<ResizeEdge>())
            {
                var corner = edge >= ResizeEdge.TopLeft;
                var left = edge is ResizeEdge.Left or ResizeEdge.TopLeft or ResizeEdge.BottomLeft;
                var right =
                    edge is ResizeEdge.Right or ResizeEdge.TopRight or ResizeEdge.BottomRight;
                var top = edge is ResizeEdge.Top or ResizeEdge.TopLeft or ResizeEdge.TopRight;
                var bottom =
                    edge is ResizeEdge.Bottom or ResizeEdge.BottomLeft or ResizeEdge.BottomRight;
                ui.ResizeHandle(
                    edge: edge,
                    variant: ResizeHandleVariant.Edge,
                    band: band,
                    lightsInward: inward,
                    ridges: true,
                    width: left || right ? (corner ? 16 : 8) : Length.Auto,
                    height: top || bottom ? (corner ? 16 : 8) : Length.Auto,
                    alignSelf: top ? AlignSelf.Start
                        : bottom ? AlignSelf.End
                        : AlignSelf.Stretch,
                    justifySelf: left ? AlignSelf.Start
                        : right ? AlignSelf.End
                        : AlignSelf.Stretch,
                    id: Enum.GetName(edge),
                    key: (int)edge
                );
            }
    }
}

/// <summary>A grid of two columns, or rows, with a <see cref="ResizeHandle"/> between them that sizes the first, or,
/// on a left or top edge, the second; a third shares what is left with the second.</summary>
internal sealed class SplitBoard
{
    public float Value;
    public ResizeEdge Edge = ResizeEdge.Right;
    public bool Preview;
    public float? Max;
    public float? Step;
    public float? DragStep;
    public float Size = 306f;
    public bool Third;
    public bool Next;
    public ResizeHandleVariant Variant;
    public int Commits;
    public readonly List<float> Committed = [];
    State<float>? _state;
    readonly Callback<float> _change;
    readonly Callback<float> _commit;

    public SplitBoard()
    {
        _change = Callback<float>.Of(
            this,
            static (b, v) =>
            {
                if (b._state is not { } state)
                    throw new InvalidOperationException("The board changed before it rendered.");
                state.Value = v;
            }
        );
        _commit = Callback<float>.Of(
            this,
            static (b, v) =>
            {
                b.Commits++;
                b.Committed.Add(v);
            }
        );
    }

    [FramePath]
    public void Render(Ui ui, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        using (ui.Fragment(file: file, line: line))
        {
            var state = _state = ui.State(Value);
            var across = Edge is ResizeEdge.Left or ResizeEdge.Right;
            var after = Edge is ResizeEdge.Left or ResizeEdge.Top;
            var sized = Track.Px(state.Value);
            var tracks =
                after ? new GridTracks(Track.Fr(1f), Track.Px(6f), sized)
                : Third ? new GridTracks(sized, Track.Px(6f), Track.Fr(1f), Track.Fr(1f))
                : new GridTracks(sized, Track.Px(6f), Track.Fr(1f));
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (
                    ui.Grid(
                        columns: across ? tracks : default,
                        rows: across ? default : tracks,
                        width: across ? Size : 400f,
                        height: across ? 100f : Size
                    )
                )
                {
                    using (ui.Box(id: "first")) { }
                    ui.ResizeHandle(
                        edge: Edge,
                        value: state.Value,
                        max: Max,
                        step: Step,
                        dragStep: DragStep,
                        preview: Preview,
                        variant: Variant,
                        onValueChange: _change,
                        onValueCommit: _commit,
                        id: "handle"
                    );
                    using (ui.Box(id: "second")) { }
                    if (Third)
                        using (ui.Box(id: "third")) { }
                }
                if (Next)
                    ui.Button(text: "Next", id: "next");
            }
        }
    }
}
