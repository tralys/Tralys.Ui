using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class ProgressTests
{
    [Test]
    public async Task A_progress_fills_its_share_of_the_range_from_the_left_on_the_frame_its_value_changes_and_never_leaves_its_track()
    {
        var model = new Model { Count = 50 };
        float min = 0f;
        float? max = null;
        using var driver = UiDriver.Render(ui =>
            ui.Progress(value: model.Count, min: min, max: max, width: 200, id: "bar")
        );
        var half = Fill(driver).Bounds;
        model.Count = 37;
        driver.Frame();
        var stepped = Fill(driver).Bounds.Width;

        float Shown(int value, float low, float? high)
        {
            model.Count = value;
            min = low;
            max = high;
            driver.Rerender();
            return Fill(driver).Bounds.Width;
        }

        await Assert.That(half).IsEqualTo(new Rect(0f, 0f, 100f, 4f));
        await Assert.That(stepped).IsEqualTo(74f);
        await Assert.That(Shown(-5, 0f, null)).IsEqualTo(0f);
        await Assert.That(Shown(120, 0f, null)).IsEqualTo(200f);
        await Assert.That(Shown(30, 20f, 70f)).IsEqualTo(40f);
        await Assert.That(Shown(30, 50f, 50f)).IsEqualTo(0f);
        await Assert.That(Shown(30, 50f, 10f)).IsEqualTo(0f);
        await Assert.That(Shown(1, 0f, 4f)).IsEqualTo(50f);
    }

    [Test]
    public async Task A_progress_fills_with_its_callers_colour()
    {
        var team = Color.FromArgb(255, 200, 40, 40);
        using var driver = UiDriver.Render(ui =>
            ui.Progress(value: 50, color: team, width: 200, id: "bar")
        );

        await Assert.That(Fill(driver).Visual.Background).IsEqualTo((Paint)team);
    }

    [Test]
    public async Task An_indeterminate_progress_sweeps_a_30_percent_segment_through_its_track_every_1_4_seconds_in_place_of_its_fill()
    {
        var model = new Model { Count = 60 };
        using var driver = UiDriver.Render(ui =>
            ui.Progress(value: model.Count, indeterminate: model.Remember, width: 200, id: "bar")
        );
        var progress = AnimatedLooks.Hex(Themes.Dark.Color(ColorToken.BgProgress));

        model.Remember = true;
        driver.Frame();
        var start = Segment(driver);
        var fills = HasFill(driver);
        driver.Frame(0.7f);
        var halfway = Segment(driver);
        driver.Frame(0.69f);
        var leaving = Segment(driver);
        driver.Frame(0.02f);
        var again = Segment(driver);
        model.Remember = false;
        driver.Frame();
        var settled = (HasFill(driver), Fill(driver).Bounds.Width, driver.View.NeedsFrame);

        await Assert.That(start).IsEqualTo($"box -60,0 60x4 fill {progress}");
        await Assert.That(fills).IsFalse();
        await Assert.That(halfway).IsEqualTo($"box 70,0 60x4 fill {progress}");
        await Assert.That(leaving).IsEqualTo($"box 198.14,0 60x4 fill {progress}");
        await Assert.That(again).IsEqualTo($"box -58.14,0 60x4 fill {progress}");
        await Assert.That(settled).IsEqualTo((true, 120f, false));
    }

    [Test]
    public async Task A_skins_progress_track_art_replaces_the_plain_track_with_the_fill_inside_its_slice_under_its_overlay_art()
    {
        using var driver = UiDriver.Render(ui =>
            ui.Progress(value: 50, width: 200, height: 12, id: "bar")
        );
        driver.View.Context.Skin = new Skin([
            new(
                SkinPart.ProgressTrack,
                new SkinPartImage(new TestTexture(16, 8), new Thickness(4, 2, 4, 2), Scale: 0.5f)
            ),
            new(SkinPart.ProgressFillOverlay, new SkinPartImage(new TestTexture(4, 4), default)),
        ]);
        driver.Settle();
        var bar = driver.Get("bar");
        var fill = Fill(driver);

        await Assert.That(bar.Visual.Background).IsEqualTo(default(Paint));
        await Assert
            .That(Nodes.Find(bar, NodeKind.Image).Bounds)
            .IsEqualTo(new Rect(0, 0, 200, 12));
        await Assert.That(Track(driver).Bounds).IsEqualTo(new Rect(2, 1, 196, 10));
        await Assert.That(fill.Bounds).IsEqualTo(new Rect(0, 0, 98, 10));
        await Assert
            .That(Nodes.Find(fill, NodeKind.Image).Bounds)
            .IsEqualTo(new Rect(0, 0, 98, 10));
    }

    static Node Track(UiDriver driver)
    {
        foreach (var child in driver.Get("bar").Children)
        {
            if (child.Kind == NodeKind.Box)
                return child;
        }
        throw new InvalidOperationException("No track.");
    }

    static bool HasFill(UiDriver driver)
    {
        foreach (var child in Track(driver).Children)
        {
            if (child.Kind == NodeKind.Box)
                return true;
        }
        return false;
    }

    static Node Fill(UiDriver driver) => Nodes.Find(Track(driver), NodeKind.Box);

    static string Segment(UiDriver driver)
    {
        foreach (var line in driver.Draw().Lines)
        {
            var at = line.IndexOf("/Canvas[0]: ", StringComparison.Ordinal);
            if (at >= 0)
                return line[(at + "/Canvas[0]: ".Length)..];
        }
        return "";
    }
}
