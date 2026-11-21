using System.Numerics;
using System.Runtime.InteropServices;
using Tralys.Ui.Fonts;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class PaintTests
{
    static readonly Color Ink = Color.FromRgb(200, 60, 40);

    [Test]
    public async Task A_fade_over_one_primitive_multiplies_into_it_rather_than_taking_a_layer()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 40, height: 40, opacity: 0.5f, id: "faded"))
            using (ui.Box(width: 20, height: 20, background: Ink)) { }
        });

        await Assert.That(Fade(driver, "faded")).IsEqualTo("opacity 0.5");
    }

    [Test]
    public async Task A_fade_over_overlapping_primitives_keeps_its_layer()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 40, height: 40, opacity: 0.5f, background: Ink, id: "faded"))
            using (ui.Box(width: 20, height: 20, background: Ink)) { }
        });

        await Assert.That(Fade(driver, "faded")).IsEqualTo("layer 0.5");
    }

    [Test]
    public async Task A_changed_fade_is_patched_to_what_a_rebuild_draws()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            var opacity = model.Count % 2 == 0 ? 0.6f : 0.35f;
            using (ui.Box(direction: Direction.Column, gap: 4))
            {
                using (ui.Box(width: 40, height: 40, opacity: opacity, background: Ink))
                    ui.Text("layered");
                using (ui.Box(width: 40, height: 40, opacity: opacity))
                using (ui.Box(width: 30, height: 30, opacity: 0.7f))
                using (ui.Box(width: 20, height: 20, background: Ink)) { }
            }
        });
        var list = new DrawList(new GradientRampAtlas());
        driver.View.Render(list);
        var generation = list.Generation;

        model.Count++;
        driver.View.Update();
        driver.View.Render(list);
        var rebuilt = new DrawList(new GradientRampAtlas());
        driver.View.Render(rebuilt);

        await Assert.That(list.Generation).IsEqualTo(generation);
        await Assert
            .That(
                MemoryMarshal
                    .AsBytes(list.Vertices)
                    .SequenceEqual(MemoryMarshal.AsBytes(rebuilt.Vertices))
            )
            .IsTrue();
    }

    [Test]
    public async Task A_patch_that_throws_leaves_the_next_frame_as_a_rebuild_draws_it()
    {
        var model = new Model();
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Column, gap: 4))
                {
                    using (
                        ui.Box(width: 40, height: 40, background: model.Count == 0 ? Calm : Failing)
                    ) { }
                    ui.Text("steady");
                }
            },
            textures: new FailingOnce()
        );
        var list = new DrawList(new GradientRampAtlas());
        driver.View.Render(list);

        model.Count = 1;
        driver.View.Update();
        var threw = false;
        try
        {
            driver.View.Render(list);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        driver.View.Render(list);
        var rebuilt = new DrawList(new GradientRampAtlas());
        driver.View.Render(rebuilt);

        await Assert.That(threw).IsTrue();
        await Assert.That(list.QuadCount).IsEqualTo(rebuilt.QuadCount);
        await Assert
            .That(
                MemoryMarshal
                    .AsBytes(list.Vertices)
                    .SequenceEqual(MemoryMarshal.AsBytes(rebuilt.Vertices))
            )
            .IsTrue();
    }

    [Test]
    public Task A_text_whose_glyph_page_starts_over_is_recorded_again_in_the_next_frame_alone_and_once() =>
        UiDriver.Run(
            ui =>
            {
                using (ui.Box(direction: Direction.Column, gap: 4))
                {
                    using (ui.Box(width: 20, height: 20, background: Ink)) { }
                    ui.Text("steady");
                }
            },
            static async driver =>
            {
                var text = driver.View.Context.Text;
                var atlas = text.Atlas;
                var font = text.Resolve(null, FontWeight.Normal, FontStyle.Normal);
                var resets = atlas.ResetCount;
                var glyph = 0;
                for (var frame = 0; frame < 400 && atlas.ResetCount == resets; frame++)
                {
                    for (var i = 0; i < 16 && atlas.ResetCount == resets; i++)
                        atlas.TryGet(font, ++glyph, 14f, 0f, out _);
                    if (atlas.ResetCount == resets)
                        driver.Frame();
                }

                driver.Frame();
                var after = driver.Stats;
                driver.Frame();

                await Assert.That(atlas.ResetCount).IsGreaterThan(resets);
                await Assert.That(after.DrawListRebuilt).IsTrue();
                await Assert.That(after.Paints).IsEqualTo(1);
                await Assert.That(driver.Stats.DrawListRebuilt).IsFalse();
            }
        );

    [Test]
    public async Task A_frame_is_reused_until_a_siblings_resize_a_fade_a_transform_or_a_new_child_and_each_change_draws_as_a_rebuild()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            var step = model.Count;
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 4))
            {
                using (ui.Box(width: step >= 1 ? 30 : 20, height: 20, background: Ink)) { }
                using (
                    ui.Box(width: 20, height: 20, background: Ink, opacity: step >= 2 ? 0.5f : 1f)
                ) { }
                using (
                    ui.Box(
                        width: 20,
                        height: 20,
                        background: Ink,
                        transform: step >= 3 ? Transform.Rotate(10) : default
                    )
                ) { }
                if (step >= 4)
                {
                    using (ui.Box(width: 20, height: 20, background: Ink)) { }
                }
            }
        });
        var view = driver.View;
        view.VerifiesFrames = true;
        var list = new DrawList(new GradientRampAtlas());
        var changed = new List<bool> { view.Render(list), view.Render(list) };

        for (var step = 1; step <= 4; step++)
        {
            model.Count = step;
            view.Update();
            changed.Add(view.Render(list));
            changed.Add(view.Render(list));
        }

        await Assert
            .That(string.Join(" ", changed))
            .IsEqualTo("True False True False True False True False True False");
        await Assert.That(view.FrameDifference).IsNull();
    }

    [Test]
    public async Task A_view_drawing_into_a_list_another_view_drew_into_since_draws_its_whole_frame_again()
    {
        using var first = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, gap: 4))
            {
                for (var i = 0; i < 3; i++)
                {
                    using (ui.Box(width: 20, height: 20, background: Ink, key: i)) { }
                }
            }
        });
        using var second = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 50, height: 50, background: Ink)) { }
        });
        var list = new DrawList(new GradientRampAtlas());
        first.View.Render(list);
        var quads = list.QuadCount;

        second.View.Render(list);
        var redrawn = first.View.Render(list);

        await Assert.That(redrawn).IsTrue();
        await Assert.That(list.QuadCount).IsEqualTo(quads);
    }

    [Test]
    public async Task A_reused_frame_whose_gradient_row_another_list_took_is_drawn_again()
    {
        var atlas = new GradientRampAtlas(rows: 1);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 40, height: 20, background: Warm)) { }
        });
        var list = new DrawList(atlas);
        atlas.BeginFrame();
        driver.View.Render(list);
        atlas.BeginFrame();
        var reused = !driver.View.Render(list);

        atlas.BeginFrame();
        atlas.Acquire(Cold);
        atlas.BeginFrame();
        var redrawn = driver.View.Render(list);

        await Assert.That(reused).IsTrue();
        await Assert.That(redrawn).IsTrue();
        await Assert.That(atlas.Acquire(WarmStops)).IsEqualTo(0);
    }

    [Test]
    public async Task A_leaf_drawing_another_number_of_shapes_rebuilds_the_frame_while_a_dash_nudged_along_its_outline_is_patched()
    {
        var pips = new Model { Count = 3 };
        var dash = new Model { Count = 81 };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, gap: 4))
            {
                ui.Canvas(state: pips, draw: Pips, width: 100, height: 10);
                ui.Canvas(state: dash, draw: Dashed, width: 100, height: 40);
                ui.Text("steady");
            }
        });
        driver.View.VerifiesFrames = true;

        pips.Count = 4;
        driver.Frame();
        var more = driver.Stats.DrawListRebuilt;
        dash.Count = 82;
        driver.Frame();
        var nudged = driver.Stats.DrawListRebuilt;
        driver.Frame();

        await Assert.That((more, nudged)).IsEqualTo((true, false));
        await Assert.That(driver.View.FrameDifference).IsNull();
    }

    [Test]
    public async Task Leaves_in_a_turned_faded_group_repainting_after_each_turn_are_patched_in_place()
    {
        var turn = new Model();
        var shade = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            var tint = Color.FromRgb((byte)(shade.Count * 9), 120, 60);
            using (
                ui.Box(
                    width: 200,
                    height: 120,
                    wrap: FlexWrap.Wrap,
                    opacity: 0.5f,
                    background: Ink,
                    transform: Transform.Rotate(turn.Count * 3f)
                )
            )
            {
                for (var i = 0; i < 24; i++)
                {
                    using (
                        ui.Box(
                            width: 30,
                            height: 18,
                            margin: new Thickness(2),
                            background: tint,
                            key: i
                        )
                    ) { }
                }
            }
        });
        driver.View.VerifiesFrames = true;
        var patched = 0;

        for (var step = 1; step <= 12; step++)
        {
            turn.Count = step;
            driver.Frame();
            shade.Count = step;
            driver.Frame();
            if (!driver.Stats.DrawListRebuilt)
                patched++;
        }

        await Assert.That(patched).IsEqualTo(12);
        await Assert.That(driver.View.FrameDifference).IsNull();
    }

    [Test]
    public async Task A_custom_paint_that_throws_halfway_leaves_the_next_frame_as_a_rebuild_draws_it()
    {
        var halfway = new Halfway();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, gap: 4))
            {
                ui.Canvas(state: halfway, draw: Halfway.Draw, width: 100, height: 10);
                ui.Text("steady");
            }
        });
        var list = new DrawList(new GradientRampAtlas());
        driver.View.Render(list);

        halfway.Model.Count = 1;
        driver.View.Update();
        var threw = false;
        try
        {
            driver.View.Render(list);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        driver.View.Render(list);
        var rebuilt = new DrawList(new GradientRampAtlas());
        driver.View.Render(rebuilt);

        await Assert.That(threw).IsTrue();
        await Assert.That(list.QuadCount).IsEqualTo(rebuilt.QuadCount);
        await Assert
            .That(
                MemoryMarshal
                    .AsBytes(list.Vertices)
                    .SequenceEqual(MemoryMarshal.AsBytes(rebuilt.Vertices))
            )
            .IsTrue();
    }

    static readonly GradientStops WarmStops = new([
        new ColorStop(0f, Color.FromRgb(240, 200, 60)),
        new ColorStop(1f, Color.FromRgb(200, 60, 40)),
    ]);
    static readonly Paint Warm = Paint.Linear(WarmStops, new Vector2(0, 0), new Vector2(1, 0));
    static readonly GradientStops Cold = new([
        new ColorStop(0f, Color.FromRgb(40, 60, 200)),
        new ColorStop(1f, Color.FromRgb(60, 200, 240)),
    ]);

    static readonly CanvasDraw<Model> Pips = static (dc, m, info) =>
    {
        for (var i = 0; i < m.Count; i++)
            dc.DrawRectangle(Ink, new Rect(i * 12, 0, 10, 10));
    };

    static readonly DashArray Dashes = new(6f, 4f);
    static readonly PathBuilder Outline = OutlineOf(new Rect(4, 4, 80, 30));

    static readonly CanvasDraw<Model> Dashed = static (dc, m, info) =>
        dc.DrawPath(Outline, default, new Pen(Ink, 2) { DashArray = Dashes, DashOffset = m.Count });

    static PathBuilder OutlineOf(Rect rect)
    {
        var path = new PathBuilder();
        path.AddRectangle(rect);
        return path;
    }

    sealed class Halfway
    {
        public readonly Model Model = new();
        bool _failed;

        public static readonly CanvasDraw<Halfway> Draw = static (dc, h, info) =>
        {
            var count = h.Model.Count;
            dc.DrawRectangle(Ink, new Rect(0, 0, 10, 10));
            dc.DrawRectangle(Ink, new Rect(12, 0, 10 + count, 10));
            if (count == 1 && !h._failed)
            {
                h._failed = true;
                throw new InvalidOperationException();
            }
            dc.DrawRectangle(Ink, new Rect(30, 0, 10, 10));
            dc.DrawRectangle(Ink, new Rect(44, 0, 10, 10));
        };
    }

    static readonly Paint Calm = Paint.FromImage(new PaintImage("calm"));
    static readonly Paint Failing = Paint.FromImage(new PaintImage("failing"));

    sealed class FailingOnce : ITextureProvider
    {
        static readonly TestTexture Texture = new(8, 8);
        bool _failed;

        public RenderTexture? Resolve(string source)
        {
            if (source == "failing" && !_failed)
            {
                _failed = true;
                throw new InvalidOperationException();
            }
            return Texture;
        }
    }

    static string Fade(UiDriver driver, string id)
    {
        foreach (var line in driver.Draw().Lines)
        {
            var at = line.IndexOf("#" + id + ": ", StringComparison.Ordinal);
            if (
                at >= 0
                && line.AsSpan(at + id.Length + 3) is var rest
                && (rest.StartsWith("layer") || rest.StartsWith("opacity"))
            )
                return rest.ToString();
        }
        return "";
    }

    [Test]
    public async Task A_gradient_whose_stops_a_render_rewrites_draws_its_new_ramp_on_the_next_frame()
    {
        var model = new Model();
        var stops = new GradientStops([new(0f, Black), new(1f, Black)]);
        var ramp = Paint.Linear(stops, System.Numerics.Vector2.Zero, System.Numerics.Vector2.One);
        using var driver = UiDriver.Render(ui =>
        {
            var red = model.Count == 1;
            stops.Set([new(0f, red ? Red : Black), new(1f, red ? Red : Black)]);
            using (ui.Box(width: 40, height: 40, background: ramp)) { }
        });
        var ramps = new GradientRampAtlas(rows: 1);
        var list = new DrawList(ramps);
        ramps.BeginFrame();
        driver.View.Render(list);

        model.Count = 1;
        driver.View.Update();
        ramps.BeginFrame();
        driver.View.Render(list);

        var first = ramps.Pixels;
        await Assert
            .That((first[0], first[1], first[2], first[3]))
            .IsEqualTo(((byte)255, (byte)0, (byte)0, (byte)255));
    }

    static readonly Color Black = Color.FromRgb(0, 0, 0);
    static readonly Color Red = Color.FromRgb(255, 0, 0);

    [Test]
    public async Task A_colour_change_patches_the_frame_in_place()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, gap: 4))
            {
                ui.Swatch(model: model);
                ui.Text("steady");
            }
        });

        model.Count = 1;
        driver.Frame();

        await Assert.That(driver.Stats.Paints).IsEqualTo(1);
        await Assert.That(driver.Stats.DrawListRebuilt).IsFalse();
    }
}

public readonly record struct SwatchProps
{
    public required Model Model { get; init; }
}

/// <summary>A box whose colour follows its model's count.</summary>
public sealed class Swatch : Component<SwatchProps>
{
    protected override void Render(Ui ui, in SwatchProps p)
    {
        var shade = (byte)(p.Model.Count * 40);
        using (ui.Box(width: 30, height: 30, radius: 4, background: Color.FromRgb(shade, 100, 100)))
        { }
    }
}
