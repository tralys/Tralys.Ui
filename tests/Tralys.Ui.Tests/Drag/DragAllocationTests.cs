using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class DragAllocationTests
{
    [Test]
    public async Task A_handle_dragged_back_and_forth_every_frame_trades_its_columns_widths_and_allocates_nothing()
    {
        var split = new SplitBoard { Value = 150f, Max = 240f };
        using var driver = UiDriver.Render(ui => split.Render(ui));
        var mouse = driver.Hold(driver.Get("handle"));
        var sway = new Sway(mouse, driver.Get("first"), step: 12f, low: -5, high: 4);

        var bytes = driver.WorstFrameBytes(sway, static (d, s) => s.Next(d));
        mouse.Release();

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(sway.Changed).IsEqualTo(sway.Frames);
    }

    [Test]
    public async Task A_handle_dragged_and_cancelled_by_escape_every_few_frames_puts_its_width_back_and_allocates_nothing()
    {
        var split = new SplitBoard { Value = 100f };
        using var driver = UiDriver.Render(ui => split.Render(ui));
        var cycle = new Cancelling(driver.Get("handle"), driver.Get("first"));

        var bytes = driver.WorstFrameBytes(cycle, static (d, c) => c.Next(d));

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(cycle.Dragged).IsGreaterThan(0);
        await Assert.That(cycle.PutBack).IsEqualTo(cycle.Dragged);
    }

    [Test]
    public async Task A_preview_dragged_up_and_down_every_frame_follows_the_pointer_resizes_nothing_until_released_and_allocates_nothing()
    {
        var split = new SplitBoard
        {
            Value = 100f,
            Edge = ResizeEdge.Bottom,
            Preview = true,
        };
        using var driver = UiDriver.Render(ui => split.Render(ui));
        var mouse = driver.Hold(driver.Get("handle"));
        mouse.MoveBy(new Vector2(0f, -30f));
        var sway = new PreviewSway(mouse, driver);

        var bytes = driver.WorstFrameBytes(sway, static (d, s) => s.Next(d));
        mouse.Release();

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(sway.Followed).IsEqualTo(sway.Frames);
        await Assert.That(split.Commits).IsEqualTo(1);
    }

    [Test]
    public async Task A_floating_surface_moved_then_resized_through_a_snap_every_frame_commits_each_gesture_once_and_allocates_nothing()
    {
        var board = new FloatBoard
        {
            Bounds = new Rect(100f, 100f, 200f, 100f),
            AreaWidth = 800f,
            AreaHeight = 600f,
            Edges = ResizeEdges.Right,
            Snap = 10f,
        };
        using var driver = UiDriver.Render(ui => board.Render(ui), size: new Size(900f, 700f));
        var gestures = new Gestures(driver.Get("title"), driver.All<ResizeHandle>()[0]);

        var bytes = driver.WorstFrameBytes(gestures, static (d, g) => g.Next(d));

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(board.Commits.Count).IsEqualTo(2 * gestures.Frames);
    }

    [Test]
    public async Task A_stick_steered_round_a_circle_by_one_finger_every_frame_while_another_taps_a_button_keeps_its_step_and_allocates_nothing()
    {
        var steps = new Steps();
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(gap: 32, align: Align.End))
            {
                ui.TouchStick(onStepChange: steps.Report, id: "stick");
                using (
                    ui.TouchButton(
                        onClick: Callback.Of(clicks, static c => c.Value++),
                        width: 76,
                        height: 76,
                        id: "button"
                    )
                ) { }
            }
        });
        var finger = driver.Hold(driver.Get("stick"), PointerKind.Touch);
        finger.MoveBy(new Vector2(50f, 0f));
        var circle = new Circle(finger, driver.Get("button"), steps);

        var bytes = driver.WorstFrameBytes(circle, static (d, c) => c.Next(d));
        finger.Release();

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert
            .That(circle.Seen)
            .IsEquivalentTo([StickStep.Up, StickStep.Right, StickStep.Down, StickStep.Left]);
        await Assert.That(circle.Centred).IsEqualTo(0);
        await Assert.That(clicks.Value).IsEqualTo(circle.Frames);
    }

    sealed class Sway(HeldPointer pointer, Node watched, float step, int low, int high)
    {
        int _at;
        int _way = 1;
        public int Frames;
        public int Changed;

        public void Next(UiDriver driver)
        {
            if (_at + _way > high || _at + _way < low)
                _way = -_way;
            _at += _way;
            var before = watched.Bounds.Width;
            pointer.MoveBy(new Vector2(step * _way, 0f), settle: false);
            driver.Frame();
            Frames++;
            if (watched.Bounds.Width != before)
                Changed++;
        }
    }

    sealed class Cancelling(Node handle, Node watched)
    {
        int _tick;
        Vector2 _at;
        public int Dragged;
        public int PutBack;

        public void Next(UiDriver driver)
        {
            var view = driver.View;
            var now = view.Context.Now;
            switch (_tick++ % 4)
            {
                case 0:
                    var bounds = handle.ViewBounds;
                    _at = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
                    view.PointerMove(Pointer.Mouse, _at, MouseButtons.None, false, default, now);
                    view.PointerDown(Pointer.Mouse, _at, MouseButton.Left, default, now);
                    break;
                case 1:
                    view.PointerMove(
                        Pointer.Mouse,
                        _at + new Vector2(20f, 0f),
                        MouseButtons.Left,
                        true,
                        default,
                        now
                    );
                    break;
                case 2:
                    if (watched.Bounds.Width == 120f)
                        Dragged++;
                    view.KeyDown(Key.Escape, default, false);
                    view.KeyUp(Key.Escape, default);
                    break;
                default:
                    if (watched.Bounds.Width == 100f)
                        PutBack++;
                    view.PointerUp(
                        Pointer.Mouse,
                        _at + new Vector2(20f, 0f),
                        MouseButton.Left,
                        default,
                        now
                    );
                    break;
            }
            driver.Frame();
        }
    }

    sealed class PreviewSway(HeldPointer pointer, UiDriver driver)
    {
        int _at;
        int _way = 1;
        readonly Node _first = driver.Get("first");
        public int Frames;
        public int Followed;

        public void Next(UiDriver d)
        {
            if (_at + _way > 9 || _at + _way < 0)
                _way = -_way;
            _at += _way;
            pointer.MoveBy(new Vector2(0f, 6f * _way), settle: false);
            d.Frame();
            Frames++;
            var layers = d.View.Layers;
            if (layers.Length == 1 && _first.Bounds.Height == 100f)
                Followed++;
        }
    }

    sealed class Gestures(Node title, Node grip)
    {
        public int Frames;

        public void Next(UiDriver driver)
        {
            var way = Frames % 2 == 0 ? 20f : -20f;
            driver.Drag(title, new Vector2(way, 0f), settle: false);
            driver.Frame();
            driver.Drag(grip, new Vector2(way, 0f), settle: false);
            driver.Frame();
            Frames++;
        }
    }

    sealed class Circle(HeldPointer finger, Node button, Steps steps)
    {
        float _angle;
        public int Frames;
        public int Centred;
        public readonly HashSet<StickStep> Seen = [];

        public void Next(UiDriver driver)
        {
            var from = new Vector2(MathF.Cos(_angle), MathF.Sin(_angle)) * 50f;
            _angle += 0.3f;
            var to = new Vector2(MathF.Cos(_angle), MathF.Sin(_angle)) * 50f;
            finger.MoveBy(to - from, settle: false);
            driver.Tap(button, settle: false);
            driver.Frame();
            Frames++;
            Seen.Add(steps.Last);
            if (steps.Last == StickStep.None)
                Centred++;
        }
    }
}
