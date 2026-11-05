using Tralys.Ui.Testing;
using TUnit.Assertions.Enums;

namespace Tralys.Ui.Tests;

public sealed class SpriteTests
{
    static readonly TestTexture Sheet = new(512, 320);
    static readonly Size Frame = new(64, 64);

    [Test]
    public async Task A_sprite_walks_its_columns_then_the_row_below_and_wraps_after_its_last_frame()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.Image(Sheet, sprite: new Sprite(Frame, Columns: 2, FrameCount: 3, Row: 1));
        });
        var shown = new List<string> { Shown(driver)[0] };

        for (var i = 0; i < 3; i++)
        {
            driver.Frame(0.1f);
            shown.Add(Shown(driver)[0]);
        }

        await Assert
            .That(shown)
            .IsEquivalentTo(["0,64", "64,64", "0,128", "0,64"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Sprites_that_joined_at_different_times_turn_their_frames_on_the_same_advance()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            {
                ui.Image(Sheet, sprite: new Sprite(Frame, 2, 3));
                if (model.Remember)
                    ui.Image(Sheet, sprite: new Sprite(Frame, 2, 3));
            }
        });

        model.Remember = true;
        driver.Frame(0.03f);
        driver.Frame(0.04f);

        await Assert.That(Shown(driver)).IsEquivalentTo(["64,0", "64,0"]);
    }

    [Test]
    public async Task A_collapsed_sprite_holds_its_frame_asking_for_no_frames_and_plays_again_once_shown()
    {
        var model = new Model { Remember = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, collapsed: model.Remember))
                ui.Image(Sheet, sprite: new Sprite(Frame, 2, 3));
        });

        driver.Frame(0.2f);
        var held = (driver.View.NeedsFrame, driver.Stats.Paints, driver.Stats.DrawListRebuilt);
        model.Remember = false;
        driver.Frame();

        await Assert.That(held).IsEqualTo((false, 0, false));
        await Assert.That(driver.View.NeedsFrame).IsTrue();
    }

    [Test]
    public async Task A_paused_sprite_holds_the_frame_it_shows_and_starts_over_from_its_first_once_played()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.Image(Sheet, sprite: new Sprite(Frame, 2, 3, Paused: model.Remember));
        });
        driver.Frame(0.1f);

        model.Remember = true;
        driver.Frame(0.1f);
        var paused = Shown(driver)[0];
        driver.Frame(0.2f);
        var held = Shown(driver)[0];
        model.Remember = false;
        driver.Frame(0.01f);

        await Assert.That(paused).IsNotEqualTo("0,0");
        await Assert.That(held).IsEqualTo(paused);
        await Assert.That(Shown(driver)[0]).IsEqualTo("0,0");
    }

    [Test]
    public async Task A_sprites_texel_inset_pulls_its_frames_source_in_on_every_side_keeping_its_size()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.Image(
                    Sheet,
                    sprite: new Sprite(Frame, 2, 3, Row: 1, TexelInset: 0.5f),
                    id: "glow"
                );
        });

        var image = driver.Draw().Lines.Single(l => l.Contains(": image "));
        var source = image[(image.IndexOf(" from ", StringComparison.Ordinal) + " from ".Length)..];

        await Assert.That(source).StartsWith("0.5,64.5 63x63 ");
        await Assert.That(driver.Get("glow").ViewBounds.Size).IsEqualTo(Frame);
    }

    static List<string> Shown(UiDriver driver)
    {
        var shown = new List<string>();
        foreach (var line in driver.Draw().Lines)
        {
            var at = line.IndexOf(": image ", StringComparison.Ordinal);
            if (at < 0)
                continue;
            var from = line.IndexOf(" from ", at, StringComparison.Ordinal) + " from ".Length;
            shown.Add(line[from..line.IndexOf(' ', from)]);
        }
        return shown;
    }
}
