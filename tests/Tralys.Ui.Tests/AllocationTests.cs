using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class AllocationTests
{
    [Test]
    public async Task A_rerender_laid_out_and_drawn_allocates_nothing()
    {
        var models = new Model[20];
        for (var i = 0; i < models.Length; i++)
            models[i] = new Model { Name = "row" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, gap: 2))
            {
                for (var i = 0; i < models.Length; i++)
                {
                    using (ui.Box(gap: 4, key: i))
                    {
                        ui.Label(model: models[i]);
                        ui.CountLabel(model: models[i]);
                    }
                }
            }
        });

        var bytes = driver.WorstFrameBytes(
            models,
            static (d, m) =>
            {
                m[0].Count++;
                m[1].Count += 1000;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task An_effect_hanging_on_formatted_text_that_changes_each_frame_allocates_nothing()
    {
        var model = new Model();
        var runs = new Model();
        using var driver = UiDriver.Render(ui =>
            ui.Effect(runs, ui.Format($"n{model.Count}"), static (r, _, _) => r.Count++)
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
    public async Task Mounting_a_component_that_takes_its_host_again_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            if ((model.Count & 1) == 0)
                ui.Flip(value: true);
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
    public async Task Hiding_and_showing_a_component_that_reads_whether_it_shows_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(hidden: (model.Count & 1) == 1))
                ui.ShownProbe(name: "allocation");
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
    public async Task Focus_moving_between_nodes_inside_a_component_that_reads_it_allocates_nothing()
    {
        using var driver = UiDriver.Render(ui => ui.FocusedProbe(name: "allocation"));

        var bytes = driver.WorstFrameBytes(
            driver,
            static (d, _) =>
            {
                d.Tab();
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_custom_layout_hiding_and_placing_its_children_in_turn_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (
                ui.Layout(
                    algorithm: CustomLayoutTests.FirstThatFits.Instance,
                    state: 0,
                    width: (model.Count & 1) == 0 ? 300 : 100
                )
            )
            {
                using (ui.Box(width: 200, height: 20, focusable: true)) { }
                using (ui.Box(width: 50, height: 20, focusable: true)) { }
            }
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
    public async Task Fading_a_layer_and_a_single_primitive_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            var opacity = 0.2f + model.Count % 7 * 0.1f;
            using (ui.Box(gap: 4))
            {
                using (ui.Box(opacity: opacity, background: Color.FromRgb(90, 90, 90)))
                    ui.Text("layered");
                using (ui.Box(width: 20, height: 20, opacity: opacity))
                using (ui.Box(width: 10, height: 10, background: Color.FromRgb(90, 90, 90))) { }
            }
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
    public async Task A_clipping_box_and_a_gradient_box_resized_every_frame_at_a_fractional_scale_allocate_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(
            ui =>
            {
                var n = model.Count;
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                {
                    using (
                        ui.Box(
                            width: 120 + n % 16,
                            height: 40,
                            radius: 8,
                            overflow: Overflow.Clip,
                            background: Color.FromRgb(40, 40, 40)
                        )
                    )
                    using (ui.Box(width: 200, height: 40, background: Color.FromRgb(90, 90, 90)))
                    { }
                    using (
                        ui.Box(
                            background: Ramp,
                            border: new Border(2, Color.FromRgb(200, 200, 200)),
                            radius: 6,
                            padding: 3,
                            shadow: new BoxShadow(0, 2, 6, 0, Color.FromArgb(128, 0, 0, 0)),
                            id: "framed"
                        )
                    )
                    using (
                        ui.Box(
                            width: 220 + n % 40,
                            height: 12,
                            background: Color.FromRgb(60, 160, 90)
                        )
                    ) { }
                }
            },
            scale: 1.25f
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
        await Assert
            .That(driver.Get("framed").Bounds.Width)
            .IsEqualTo(230f + model.Count % 40)
            .Within(0.5f);
    }

    static readonly Paint Ramp = Paint.Linear(
        new GradientStops([
            new ColorStop(0f, Color.FromRgb(30, 60, 160)),
            new ColorStop(1f, Color.FromRgb(160, 60, 30)),
        ]),
        new Vector2(0f, 0.5f),
        new Vector2(1f, 0.5f)
    );

    [Test]
    public async Task Posts_from_a_worker_and_the_ui_thread_every_frame_run_on_the_ui_thread_in_the_next_update_and_allocate_nothing()
    {
        using var driver = UiDriver.Render(static ui => ui.Text("posts"));
        using var posts = new Posts(driver.View.Context.Dispatcher);
        var worker = new Thread(posts.Work) { IsBackground = true };
        worker.Start();

        long bytes;
        try
        {
            bytes = driver.WorstFrameBytes(posts, static (d, p) => p.Frame(d));
        }
        finally
        {
            posts.Stop();
            worker.Join();
        }

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(posts.Misplaced).IsEqualTo(0);
        await Assert.That(posts.WorkerRuns).IsEqualTo(posts.Frames);
        await Assert.That(posts.UiRuns).IsEqualTo(posts.Frames);
    }

    [Test]
    public async Task A_click_that_sets_state_allocates_nothing()
    {
        using var driver = UiDriver.Render(ui => ui.Ticker(id: "ticker"));
        var ticker = driver.Get("ticker");

        var bytes = driver.WorstFrameBytes(
            ticker,
            static (d, t) =>
            {
                d.Click(t, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_new_node_allocates_only_itself()
    {
        _ = new Node(NodeKind.Image);
        _ = RuntimeHelpers.GetUninitializedObject(typeof(Node));

        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = RuntimeHelpers.GetUninitializedObject(typeof(Node));
        var shell = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        _ = new Node(NodeKind.Box);
        var box = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        _ = new Node(NodeKind.Image);
        var image = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That((box, image)).IsEqualTo((shell, shell));
    }

    [Test]
    public async Task A_box_turned_a_quarter_further_every_frame_relays_out_what_follows_and_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start))
            {
                using (
                    ui.Box(
                        width: 60,
                        height: 20,
                        quarterTurns: model.Count % 4,
                        background: Color.FromRgb(200, 60, 40)
                    )
                ) { }
                using (
                    ui.Box(
                        width: 20,
                        height: 20,
                        background: Color.FromRgb(60, 60, 200),
                        id: "after"
                    )
                ) { }
            }
        });
        var turns = new Turns(model, driver.Get("after"));

        var bytes = driver.WorstFrameBytes(
            turns,
            static (d, t) =>
            {
                t.Model.Count++;
                d.Frame();
                if (t.After.ViewBounds.X != (t.Model.Count % 2 == 0 ? 60 : 20))
                    t.Wrong++;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(turns.Wrong).IsEqualTo(0);
    }

    [Test]
    public async Task A_bar_redrawn_every_frame_from_a_gradient_row_another_bar_holds_is_patched_in_place_and_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, gap: 4))
            {
                ui.Canvas(
                    state: new Bar(model, Toggles: false),
                    draw: Bar.Draw,
                    width: 100,
                    height: 10
                );
                ui.Canvas(
                    state: new Bar(model, Toggles: true),
                    draw: Bar.Draw,
                    width: 100,
                    height: 10
                );
            }
        });
        driver.View.VerifiesFrames = true;
        var frames = new Patches(model);

        var bytes = driver.WorstFrameBytes(
            frames,
            static (d, f) =>
            {
                f.Model.Count++;
                d.Frame();
                if (d.Stats.DrawListRebuilt || d.Stats.Paints != 1)
                    f.Wrong++;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(frames.Wrong).IsEqualTo(0);
        await Assert.That(driver.View.FrameDifference).IsNull();
    }

    sealed class Turns(Model model, Node after)
    {
        public readonly Model Model = model;
        public readonly Node After = after;
        public int Wrong;
    }

    sealed class Patches(Model model)
    {
        public readonly Model Model = model;
        public int Wrong;
    }

    [Test]
    public async Task Changing_nullable_style_and_placement_values_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui => ui.Drift(model: model));

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count = (m.Count + 1) % 4;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_z_index_flipped_every_frame_lifts_the_earlier_box_over_the_overlap_while_raised_and_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 200, height: 200))
            {
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 0,
                        top: 0,
                        width: 100,
                        height: 100,
                        zIndex: model.Count % 2,
                        background: Color.FromRgb(200, 60, 60),
                        id: "earlier"
                    )
                ) { }
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 50,
                        top: 50,
                        width: 100,
                        height: 100,
                        background: Color.FromRgb(60, 60, 200),
                        id: "later"
                    )
                ) { }
            }
        });
        var hits = new Overlap(model, driver.Get("earlier"), driver.Get("later"));

        var bytes = driver.WorstFrameBytes(
            hits,
            static (d, h) =>
            {
                h.Model.Count++;
                d.Frame();
                var raised = h.Model.Count % 2 == 1;
                if (d.View.HitTest(new Vector2(75, 75)) != (raised ? h.Earlier : h.Later))
                    h.Wrong++;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(hits.Wrong).IsEqualTo(0);
    }

    [Test]
    public async Task A_clock_reader_rendering_again_as_each_second_turns_allocates_nothing()
    {
        var clock = new ManualClock(
            new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.FromHours(-5)),
            ManualClock.Eastern
        );
        var probe = new ClockProbe();
        using var driver = UiDriver.Render(ui =>
            ui.ClockFace(granularity: TimeSpan.FromSeconds(1), probe: probe)
        );
        driver.View.Context.Time = clock;
        driver.Settle();
        var renders = probe.Renders;

        var bytes = driver.WorstFrameBytes(
            clock,
            static (d, c) =>
            {
                c.Advance(TimeSpan.FromMilliseconds(250));
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(probe.Renders).IsGreaterThan(renders);
    }

    [Test]
    public async Task A_canvas_recorded_again_from_a_tracked_read_allocates_nothing()
    {
        var model = new Model();
        var draws = new Counter();
        using var driver = UiDriver.Render(ui =>
            ui.Canvas(state: new Gauge(model, draws, 0), draw: Gauge.Draw, width: 40, height: 10)
        );

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count = (m.Count + 1) % 40;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_canvas_stroking_dashing_clipping_shadowing_and_painting_images_every_frame_allocates_nothing()
    {
        var model = new Model();
        var sketch = new Sketch(model);
        using var driver = UiDriver.Render(ui =>
            ui.Canvas(state: sketch, draw: Sketch.Draw, width: 300, height: 200)
        );

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count = (m.Count + 1) % 40;
                d.Frame();
            }
        );
        var list = new DrawList(new GradientRampAtlas());
        driver.View.Render(list);

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(list.Layers.Length).IsEqualTo(3);
    }

    [Test]
    public async Task A_size_class_flipping_every_frame_renders_its_reader_and_tells_its_listener_each_time_and_allocates_nothing()
    {
        using var driver = SizeClassTests.Mount(null, 1000, 560);
        var missed = new Counter();
        var told = new Counter();
        driver.View.SizeClassChanged += (_, _) => told.Value++;

        var bytes = driver.WorstFrameBytes(
            (Missed: missed, Told: told),
            static (d, s) =>
            {
                var before = s.Told.Value;
                d.View.Size = new Size(1000, d.View.Size.Height == 400f ? 560 : 400);
                d.Frame();
                s.Missed.Value +=
                    d.Stats.RendersOf<ClassReader>() == 1 && s.Told.Value == before + 1 ? 0 : 1;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(missed.Value).IsEqualTo(0);
    }

    [Test]
    public async Task An_image_moving_to_another_array_layer_every_frame_rebuilds_the_frame_and_allocates_nothing()
    {
        var texture = new MovingLayer(new TestTextureArray(24, 24, layers: 2));
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.Image(texture);
        });
        var missed = new Counter();

        var bytes = driver.WorstFrameBytes(
            (Texture: texture, Missed: missed),
            static (d, s) =>
            {
                s.Texture.Layer ^= 1;
                d.Frame();
                s.Missed.Value += d.Stats.DrawListRebuilt ? 0 : 1;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(missed.Value).IsEqualTo(0);
    }

    [Test]
    public async Task A_custom_layout_rescaling_and_spreading_its_children_over_a_new_height_every_frame_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            var count = model.Count;
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (
                ui.Layout(
                    algorithm: CustomLayoutTests.Spread.Instance,
                    state: 1f + count % 3 * 0.5f,
                    height: 150 + count % 50
                )
            )
            {
                using (ui.Box(width: 20, height: 10)) { }
                using (ui.Box(width: 30, height: 20, id: "last")) { }
            }
        });
        var moves = new Moves(model, driver.Get("last"));

        var bytes = driver.WorstFrameBytes(
            moves,
            static (d, m) =>
            {
                var at = m.Last.ViewBounds.Y;
                m.Model.Count++;
                d.Frame();
                m.Stayed += m.Last.ViewBounds.Y == at ? 1 : 0;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(moves.Stayed).IsEqualTo(0);
    }

    sealed class Moves(Model model, Node last)
    {
        public Model Model => model;

        public Node Last => last;

        public int Stayed;
    }

    sealed class Overlap(Model model, Node earlier, Node later)
    {
        public Model Model => model;

        public Node Earlier => earlier;

        public Node Later => later;

        public int Wrong;
    }

    sealed class Posts(Dispatcher dispatcher) : IDisposable
    {
        readonly SemaphoreSlim _go = new(0);
        readonly SemaphoreSlim _posted = new(0);
        readonly int _uiThread = Environment.CurrentManagedThreadId;
        volatile bool _stopping;

        public int Frames;
        public int WorkerRuns;
        public int UiRuns;
        public int Misplaced;

        public void Frame(UiDriver driver)
        {
            _go.Release();
            _posted.Wait();
            dispatcher.Post((this, Frames), static s => s.Item1.RanFromUi(s.Item2));
            if (WorkerRuns != Frames || UiRuns != Frames)
                Misplaced++;
            Frames++;
            driver.Frame();
            if (WorkerRuns != Frames || UiRuns != Frames)
                Misplaced++;
        }

        public void Work()
        {
            while (true)
            {
                _go.Wait();
                if (_stopping)
                    return;
                dispatcher.Post(this, static p => p.RanFromWorker());
                _posted.Release();
            }
        }

        public void Stop()
        {
            _stopping = true;
            _go.Release();
        }

        void RanFromWorker()
        {
            if (Environment.CurrentManagedThreadId != _uiThread)
                Misplaced++;
            WorkerRuns++;
        }

        void RanFromUi(int frame)
        {
            if (Environment.CurrentManagedThreadId != _uiThread || frame != UiRuns)
                Misplaced++;
            UiRuns++;
        }

        public void Dispose()
        {
            _go.Dispose();
            _posted.Dispose();
        }
    }
}

internal sealed class Sketch(Model model)
{
    static readonly Color Ink = Color.FromRgb(230, 230, 230);
    static readonly Color Fill = Color.FromRgb(200, 80, 40);
    static readonly Color Shade = Color.FromArgb(140, 0, 0, 0);
    static readonly DashArray Dashes = new(3f, 2f);
    static readonly TestTexture Art = new(48, 24);
    static readonly Paint Glow = Paint.Radial(
        new GradientStops([
            new ColorStop(0f, Color.FromRgb(240, 200, 60)),
            new ColorStop(1f, Color.FromRgb(40, 60, 200)),
        ]),
        new Vector2(0.5f, 0.5f),
        new Vector2(0.5f, 0.5f)
    );
    static readonly Paint Pattern = Paint.FromImage(new PaintImage(Art));

    readonly PathBuilder _ring = new();
    readonly PathBuilder _route = new();
    readonly PathBuilder _oval = new();
    readonly PathBuilder _notched = new();

    Model Model => model;

    public static readonly CanvasDraw<Sketch> Draw = static (dc, s, info) =>
    {
        var n = s.Model.Count;
        var k = n % 8;

        var ring = s._ring;
        ring.Clear();
        ring.AddEllipse(new Rect(10, 10, 60 + k, 60));
        ring.AddRectangle(new Rect(30, 30, 20, 20));
        dc.DrawPath(ring, Fill, new Pen(Ink, 1 + n % 4, PenLineJoin.Round));

        var route = s._route;
        route.Clear();
        route.MoveTo(new Vector2(90, 10));
        route.LineTo(new Vector2(140, 40 + k));
        route.LineTo(new Vector2(190, 10));
        route.LineTo(new Vector2(240, 40));
        dc.DrawPath(
            route,
            default,
            new Pen(Ink, 2, PenLineJoin.Round, PenLineCap.Round, PenLineCap.Round)
            {
                DashArray = Dashes,
                DashOffset = n % 5,
                DashCap = PenLineCap.Round,
            }
        );

        var oval = s._oval;
        oval.Clear();
        oval.AddEllipse(new Rect(10, 80, 60, 40));
        dc.PushClip(oval);
        dc.DrawRectangle(Fill, new Rect(10, 80, 20 + k * 5, 40));
        dc.Pop();

        var notched = s._notched;
        notched.Clear();
        notched.MoveTo(new Vector2(80, 80));
        notched.LineTo(new Vector2(140, 80));
        notched.LineTo(new Vector2(140, 90));
        notched.ArcTo(new Vector2(140, 110), new Vector2(10, 10), 0, false, false);
        notched.LineTo(new Vector2(140, 120));
        notched.LineTo(new Vector2(80, 120));
        notched.Close();
        dc.PushClip(notched);
        dc.DrawRectangle(Glow, new Rect(80, 80, 20 + k * 5, 40));
        dc.Pop();

        dc.PushClip(new Rect(150, 80, 60, 40), new CornerRadius(8));
        dc.DrawRectangle(Fill, new Rect(150, 80, 20 + k * 5, 40));
        dc.Pop();

        var box = new Rect(220, 80, 60, 40);
        var radius = new CornerRadius(2 + k);
        dc.DrawBoxShadow(box, radius, new BoxShadow(2, 3, 6, 1, Shade));
        dc.DrawBox(box, radius, new Thickness(1), Fill, Ink);
        dc.DrawBoxShadow(box, radius, new BoxShadow(0, 1, 4, 0, Shade, IsInset: true));

        dc.DrawImage(
            Art,
            new Rect(10, 130, 60 + k * 4, 50),
            new Rect(0, 0, 48, 24),
            new NineSlice(new Thickness(8), 1f, SliceFill.Tile)
        );
        dc.DrawRectangle(Glow, new Rect(150, 130, 40 + k * 4, 50));
        dc.DrawRectangle(Pattern, new Rect(220, 130, 40 + k * 4, 50));
    };
}

public readonly record struct DriftProps
{
    public required Model Model { get; init; }
}

public sealed class Drift : Component<DriftProps>
{
    protected override void Render(Ui ui, in DriftProps p)
    {
        var step = p.Model.Count + 1;
        using (ui.Box(letterSpacing: step))
        using (
            ui.Box(
                shrink: step,
                opacity: 1f / step,
                transformOrigin: new System.Numerics.Vector2(step, 0)
            )
        )
            ui.Text("drift", underline: step % 2 == 0);
    }
}

/// <summary>A bar filled from a gradient it shares with every other bar, as long as its value: half full, or, when it
/// toggles, a quarter or three quarters by its model's count.</summary>
internal readonly record struct Bar(Model Model, bool Toggles)
{
    static readonly Paint Fill = Paint.Linear(
        new GradientStops([
            new ColorStop(0f, Color.FromRgb(240, 200, 60)),
            new ColorStop(1f, Color.FromRgb(200, 60, 40)),
        ]),
        new Vector2(0f, 0.5f),
        new Vector2(1f, 0.5f)
    );

    public static readonly CanvasDraw<Bar> Draw = static (dc, bar, info) =>
    {
        var value =
            !bar.Toggles ? 0.5f
            : bar.Model.Count % 2 == 0 ? 0.25f
            : 0.75f;
        dc.DrawRectangle(Fill, new Rect(0, 0, info.Size.Width * value, info.Size.Height));
    };
}
