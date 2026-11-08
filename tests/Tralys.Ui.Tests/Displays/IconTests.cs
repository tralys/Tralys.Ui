using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class IconTests
{
    static readonly IconGlyph Star = new(0xe176, "lucide");

    [Test]
    public async Task An_icon_of_a_role_draws_nothing_until_the_context_gets_its_set_then_the_sets_glyph_in_its_font()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 8))
            {
                ui.Icon(glyph: IconGlyph.Of(IconRole.Search), id: "search");
                using (ui.Box(width: 10, height: 10, id: "after")) { }
            }
        });
        var before = (driver.All<Icon>()[0].Bounds.Size, driver.Get("after").ViewBounds.X);

        driver.WithIcons();
        var glyph = Nodes.Find(driver.Get("search"), NodeKind.Text);
        var drawn = TestIcons.Set.Glyph(IconRole.Search);

        await Assert.That(before).IsEqualTo((default(Size), 0f));
        await Assert
            .That((glyph.Text.ToString(), glyph.Env.FontFamily))
            .IsEqualTo((drawn.Text.ToString(), drawn.FontFamily!));
    }

    [Test]
    public async Task An_info_icon_takes_its_rungs_icon_size_less_2_unless_sized_and_follows_the_rung_and_density_while_a_plain_one_stands_16()
    {
        var rung = new RungModel { Size = ComponentSize.Xs };
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(align: Align.Start, gap: 8))
                {
                    ui.Icon(variant: IconVariant.Info, rung: rung.Size, id: "info");
                    ui.Icon(variant: IconVariant.Info, size: 11, rung: rung.Size, id: "sized");
                    ui.Icon(glyph: IconGlyph.Of(IconRole.Info), id: "plain");
                }
            })
            .WithIcons();
        float Size(string id) => Nodes.Find(driver.Get(id), NodeKind.Text).Env.FontSize;
        float Rung(ComponentSize size, Density density) =>
            Themes.Dark.Dimensions(density).Size(size).IconSize - 2f;
        var info = Nodes.Find(driver.Get("info"), NodeKind.Text);

        var xs = (Size("info"), Size("sized"), Size("plain"));
        rung.Size = ComponentSize.Lg;
        driver.Rerender();
        var lg = Size("info");
        driver.View.Context.Density = Density.Touch;
        driver.Settle();

        await Assert.That(xs).IsEqualTo((Rung(ComponentSize.Xs, Density.Pointer), 11f, 16f));
        await Assert
            .That(info.Inherit.Color)
            .IsEqualTo((Paint)Themes.Dark.Color(ColorToken.FgMuted));
        await Assert.That(lg).IsEqualTo(Rung(ComponentSize.Lg, Density.Pointer));
        await Assert.That(Size("info")).IsEqualTo(Rung(ComponentSize.Lg, Density.Touch));
    }

    [Test]
    public async Task An_icon_takes_the_text_colour_around_it_and_one_with_no_glyph_takes_no_space()
    {
        var accent = Themes.Dark.Color(ColorToken.FgAccent);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 8, color: accent))
            {
                ui.Icon(glyph: Star, id: "star");
                ui.Icon();
                using (ui.Box(width: 10, height: 10, id: "after")) { }
            }
        });
        var star = driver.Get("star");

        await Assert.That(Nodes.Find(star, NodeKind.Text).Env.Color).IsEqualTo((Paint)accent);
        await Assert.That(driver.Get("after").ViewBounds.X).IsEqualTo(star.ViewBounds.Right + 8f);
    }

    [Test]
    public async Task The_context_takes_one_icon_set_for_its_life_and_a_set_names_one_glyph_for_every_role()
    {
        using var driver = UiDriver.Render(ui => ui.Icon(glyph: Star));
        var context = driver.View.Context;
        context.Icons = TestIcons.Set;
        context.Icons = TestIcons.Set;
        var roles = Enum.GetValues<IconRole>().Length;
        IconGlyph[] Glyphs(int count) =>
            [.. Enumerable.Range(0, count).Select(static i => new IconGlyph(0xE100 + i, "lucide"))];
        var withRole = Glyphs(roles);
        withRole[3] = IconGlyph.Of(IconRole.Info);
        var withNone = Glyphs(roles);
        withNone[0] = default;

        await Assert
            .That(() => context.Icons = new IconSet(Glyphs(roles)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => new IconSet(Glyphs(roles - 1))).Throws<ArgumentException>();
        await Assert.That(() => new IconSet(Glyphs(roles + 1))).Throws<ArgumentException>();
        await Assert.That(() => new IconSet(withRole)).Throws<ArgumentException>();
        await Assert.That(() => new IconSet(withNone)).Throws<ArgumentException>();
        await Assert.That(() => new IconGlyph(0, "lucide")).Throws<ArgumentOutOfRangeException>();
        await Assert
            .That(() => new IconGlyph(0xD800, "lucide"))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new IconGlyph(0xE000, "")).Throws<ArgumentException>();
    }

    sealed class RungModel
    {
        public ComponentSize Size;
    }
}
