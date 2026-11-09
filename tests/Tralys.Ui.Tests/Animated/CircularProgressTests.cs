using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class CircularProgressTests
{
    [Test]
    public async Task A_ring_draws_only_its_track_at_or_below_its_minimum_a_whole_fill_past_its_maximum_and_nothing_without_depth_or_size()
    {
        var value = 0f;
        float? thickness = null;
        var size = 40f;
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.CircularProgress(
                    value: value,
                    min: 10,
                    max: 60,
                    thickness: thickness,
                    width: size,
                    height: size
                )
            ) { }
        });
        var t = Themes.Dark;
        var track = AnimatedLooks.Hex(t.Color(ColorToken.BgSunken));
        var fill = AnimatedLooks.Hex(t.Color(ColorToken.BgAccent));

        string[] Arcs(float v, float? depth, float side)
        {
            value = v;
            thickness = depth;
            size = side;
            driver.Rerender();
            return [.. driver.Draw().Lines.Select(AnimatedLooks.Primitive)];
        }

        await Assert
            .That(Arcs(10f, null, 40f))
            .IsEquivalentTo([$"arc 0,0 40x40 width 4 from -90 sweep 360 {track}"]);
        await Assert
            .That(Arcs(35f, 6f, 40f))
            .IsEquivalentTo([
                $"arc 0,0 40x40 width 6 from -90 sweep 360 {track}",
                $"arc 0,0 40x40 width 6 from -90 sweep 180 {fill}",
            ]);
        await Assert
            .That(Arcs(90f, null, 40f))
            .IsEquivalentTo([
                $"arc 0,0 40x40 width 4 from -90 sweep 360 {track}",
                $"arc 0,0 40x40 width 4 from -90 sweep 360 {fill}",
            ]);
        await Assert.That(Arcs(35f, 0f, 40f)).IsEmpty();
        await Assert.That(Arcs(35f, null, 0f)).IsEmpty();
    }
}
