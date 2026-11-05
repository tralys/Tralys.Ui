using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ImageTests
{
    static readonly TestTexture Wide = new(100, 50);

    [Test]
    public async Task A_keyed_image_takes_its_textures_size_and_the_context_resolves_each_key_once()
    {
        var textures = new CountingTextures();
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(align: Align.Start, gap: 4))
                {
                    ui.Image("icon", id: "a");
                    ui.Image("icon", id: "b");
                    ui.Image("missing", width: 12, id: "c");
                }
            },
            textures: textures
        );
        driver.Rerender();

        await Assert.That(driver.Get("a").Bounds.Size).IsEqualTo(new Size(30, 20));
        await Assert.That(driver.Get("b").Bounds.Size).IsEqualTo(new Size(30, 20));
        await Assert.That(driver.Get("c").Bounds.Size).IsEqualTo(new Size(12, 0));
        await Assert.That(textures.Asked).IsEqualTo(2);
    }

    [Test]
    public async Task An_auto_sized_image_capped_by_its_max_sizes_keeps_its_aspect()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.Image(Wide, maxWidth: 80, maxHeight: 20, id: "capped");
        });

        await Assert.That(driver.Get("capped").ViewBounds.Size).IsEqualTo(new Size(40, 20));
    }

    [Test]
    public async Task A_covering_image_crops_evenly_round_its_middle()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.Image(Wide, fit: ObjectFit.Cover, width: 50, height: 50);
        });

        await Assert.That(ImageLine(driver)).EndsWith("image 0,0 50x50 from 25,0 50x50 of 100x50");
    }

    [Test]
    public async Task A_contained_image_draws_centred_and_hits_there_but_not_in_its_letterbox()
    {
        var under = new Counter();
        var image = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    id: "under",
                    width: 200,
                    height: 200,
                    onClick: Callback.Of(under, static c => c.Value++)
                )
            )
                ui.Image(
                    Wide,
                    fit: ObjectFit.Contain,
                    width: 200,
                    height: 200,
                    onClick: Callback.Of(image, static c => c.Value++)
                );
        });
        var box = driver.Get("under");

        driver.Click(box, new Vector2(100, 40));
        driver.Click(box, new Vector2(100, 60));

        await Assert.That(under.Value).IsEqualTo(1);
        await Assert.That(image.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_skin_replaces_the_switch_track_it_has_art_for_and_swapping_it_rerenders()
    {
        using var driver = UiDriver.Render(ui => ui.Switch(value: true));
        var plain = driver.Draw();

        driver.View.Context.Skin = new Skin([
            new(SkinPart.ToggleTrackOn, new SkinPartImage(Wide, new Thickness(8))),
        ]);
        driver.Settle();
        var skinned = driver.Draw();
        driver.View.Context.Skin = null;
        driver.Settle();

        await Assert.That(skinned).IsNotEqualTo(plain);
        await Assert.That(driver.Draw()).IsEqualTo(plain);
    }

    [Test]
    public async Task An_image_sized_by_its_source_rect_draws_that_rect_and_draws_the_new_texture_once_its_key_changes()
    {
        var model = new Model { Name = "atlas" };
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(align: Align.Start))
                    ui.Image(model.Name, sourceRect: new Rect(64, 0, 64, 32), id: "sprite");
            },
            textures: new AtlasTextures()
        );
        var atlas = ImageLine(driver);

        model.Name = "sheet";
        driver.Frame();

        await Assert.That(driver.Get("sprite").Bounds.Size).IsEqualTo(new Size(64, 32));
        await Assert.That(atlas).EndsWith("image 0,0 64x32 from 64,0 64x32 of 512x32");
        await Assert.That(ImageLine(driver)).EndsWith("image 0,0 64x32 from 64,0 64x32 of 256x32");
    }

    [Test]
    public async Task A_box_painted_with_a_keyed_image_draws_the_texture_the_views_provider_resolves()
    {
        var textures = new AtlasTextures();
        using var driver = UiDriver.Render(
            static ui =>
            {
                using (ui.Box(width: 40, height: 40, background: KeyedAtlas)) { }
            },
            textures: textures
        );
        var list = new DrawList(new GradientRampAtlas());

        driver.View.Render(list);

        RenderTexture? drawn = null;
        foreach (var batch in list.Batches)
            drawn ??= batch.PaintTexture;
        await Assert.That(drawn).IsSameReferenceAs(textures.Resolve("atlas"));
    }

    static readonly Paint KeyedAtlas = Paint.FromImage(new PaintImage("atlas"));

    [Test]
    public async Task A_sliced_image_measures_to_its_corners_at_their_scale()
    {
        using var driver = UiDriver.Render(static ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.Image(Panel, slice: new NineSlice(new Thickness(48), Scale: 0.5f), id: "panel");
        });

        await Assert.That(driver.Get("panel").Bounds.Size).IsEqualTo(new Size(48, 48));
    }

    static readonly TestTexture Panel = new(144, 144);

    [Test]
    public async Task A_frame_whose_texture_binds_elsewhere_rebuilds_and_one_whose_bindings_hold_does_not()
    {
        var texture = new MovingLayer(new TestTextureArray(24, 24, layers: 2));
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.Image(texture);
        });

        driver.Frame();
        var idle = driver.Stats.DrawListRebuilt;
        texture.Layer = 1;
        driver.Frame();
        var moved = driver.Stats.DrawListRebuilt;
        driver.Frame();
        var after = driver.Stats.DrawListRebuilt;

        await Assert.That((idle, moved, after)).IsEqualTo((false, true, false));
    }

    static string ImageLine(UiDriver driver)
    {
        foreach (var line in driver.Draw().Lines)
        {
            if (line.Contains(": image ", StringComparison.Ordinal))
                return line;
        }
        return "";
    }
}

/// <summary>A texture living in a layer of <paramref name="array"/> that a host can move to another.</summary>
internal sealed class MovingLayer(RenderTextureArray array) : RenderTexture
{
    public int Layer;

    public override int Width => array.Width;

    public override int Height => array.Height;

    public override RenderTexture Bind(out int layer)
    {
        layer = Layer;
        return array;
    }
}

internal sealed class TestTexture(int width, int height) : RenderTexture
{
    public override int Width => width;

    public override int Height => height;
}

internal sealed class AtlasTextures : ITextureProvider
{
    static readonly TestTexture Atlas = new(512, 32);
    static readonly TestTexture Sheet = new(256, 32);

    public RenderTexture? Resolve(string source) =>
        source switch
        {
            "atlas" => Atlas,
            "sheet" => Sheet,
            _ => null,
        };
}

internal sealed class CountingTextures : ITextureProvider
{
    static readonly TestTexture Icon = new(30, 20);
    static readonly TestTextureArray Icons = new(24, 24, layers: 2);
    static readonly RenderTextureLayer First = new(Icons, 0);
    static readonly RenderTextureLayer Second = new(Icons, 1);

    public int Asked;

    public RenderTexture? Resolve(string source)
    {
        Asked++;
        return source switch
        {
            "icon" => Icon,
            "layer 0" => First,
            "layer 1" => Second,
            _ => null,
        };
    }
}

internal sealed class TestTextureArray(int width, int height, int layers) : RenderTextureArray
{
    public override int Width => width;

    public override int Height => height;

    public override int LayerCount => layers;
}
