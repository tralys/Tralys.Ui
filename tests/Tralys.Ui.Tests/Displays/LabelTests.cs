using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class LabelTests
{
    static readonly Theme Dark = Themes.Dark;

    [Test]
    public async Task A_badge_counts_to_99_then_reads_99_plus_after_its_prefix()
    {
        var badge = new BadgeModel { Count = 5, Prefix = "@" };
        using var driver = UiDriver.Render(ui =>
            ui.Badge(count: badge.Count, prefix: badge.Prefix, id: "badge")
        );
        string Shown() => Nodes.Find(driver.Get("badge"), NodeKind.Text).Text.ToString();

        var prefixed = Shown();
        badge.Count = 100;
        driver.Rerender();
        var past = Shown();
        badge.Prefix = null;
        badge.Count = 99;
        driver.Rerender();

        await Assert.That((prefixed, past, Shown())).IsEqualTo(("@5", "@99+", "99"));
    }

    [Test]
    public async Task A_badge_with_nothing_to_count_takes_no_space_until_it_counts_again()
    {
        var badge = new BadgeModel();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 8))
            {
                using (ui.Box(width: 10, height: 10)) { }
                ui.Badge(count: badge.Count, id: "badge");
                using (ui.Box(width: 10, height: 10, id: "after")) { }
            }
        });

        var empty = driver.Get("after").ViewBounds.X;
        badge.Count = 3;
        driver.Rerender();

        await Assert.That(empty).IsEqualTo(18f);
        await Assert
            .That(driver.Get("after").ViewBounds.X)
            .IsEqualTo(driver.Get("badge").ViewBounds.Right + 8f);
    }

    [Test]
    public async Task A_badge_is_a_14_tall_round_pill_filled_on_its_scheme_and_edged_only_on_the_secondary()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 8))
            {
                ui.Badge(count: 3, id: "primary");
                ui.Badge(count: 3, scheme: ColorScheme.Secondary, id: "secondary");
                ui.Badge(count: 3, scheme: ColorScheme.Danger, id: "danger");
            }
        });
        var primary = driver.Get("primary");
        var secondary = driver.Get("secondary");
        var filled = Dark.Colors(
            ColorScheme.Primary,
            ComponentVariant.Filled,
            ComponentState.Normal
        );

        await Assert
            .That(
                (
                    primary.Bounds.Height,
                    primary.Bounds.Width >= 14f,
                    primary.Visual.Radius,
                    primary.Visual.Background,
                    primary.Layout.Border
                )
            )
            .IsEqualTo(
                (14f, true, new CornerRadius(7f), (Paint)filled.Background, default(Thickness))
            );
        await Assert
            .That((secondary.Layout.Border, secondary.Visual.BorderPaint))
            .IsEqualTo(
                (
                    new Thickness(1f),
                    (Paint)
                        Dark.Colors(
                            ColorScheme.Secondary,
                            ComponentVariant.Filled,
                            ComponentState.Normal
                        ).Border
                )
            );
        await Assert
            .That(driver.Get("danger").Visual.Background)
            .IsEqualTo(
                (Paint)
                    Dark.Colors(
                        ColorScheme.Danger,
                        ComponentVariant.Filled,
                        ComponentState.Normal
                    ).Background
            );
    }

    [Test]
    public async Task Tags_badges_and_status_badges_label_without_acting_so_a_click_passes_through_and_hover_leaves_their_look()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    direction: Direction.Column,
                    align: Align.Start,
                    gap: 8,
                    padding: 8,
                    onClick: Callback.Of(clicks, static c => c.Value++)
                )
            )
            {
                ui.Tag(text: "Member", id: "tag");
                ui.Badge(count: 4, id: "badge");
                ui.StatusBadge(text: "Done", state: StatusBadgeState.Done, id: "status");
            }
        });
        var changed = new List<string>();
        foreach (var id in (string[])["tag", "badge", "status"])
        {
            var node = driver.Get(id);
            var resting = node.Visual.Background;
            driver.Hover(node);
            var hovered = node.Visual.Background;
            var press = driver.Hold(node);
            var held = node.Visual.Background;
            press.Release();
            if (hovered != resting || held != resting)
                changed.Add(id);
        }

        await Assert.That(clicks.Value).IsEqualTo(3);
        await Assert.That(changed).IsEmpty();
    }

    [Test]
    public async Task A_tag_is_a_16_tall_caption_on_its_palette_with_a_10_unit_icon_4_before_its_text_and_no_gap_without_one()
    {
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                {
                    ui.Tag(text: "Member", id: "plain");
                    ui.Tag(text: "Blocked", icon: IconGlyph.Of(IconRole.Lock), id: "icon");
                }
            })
            .WithIcons();
        var plain = driver.Get("plain");
        var withIcon = driver.Get("icon");
        var plainText = Nodes.Find(plain, NodeKind.Text);
        var glyph = Nodes.Glyph(withIcon);
        var label = driver.GetText("Blocked");
        var look = Dark.Colors(
            ColorScheme.Secondary,
            ComponentVariant.Subtle,
            ComponentState.Normal
        );

        await Assert
            .That(
                (
                    plain.Bounds.Height,
                    plainText.ViewBounds.X - plain.ViewBounds.X,
                    plain.Visual.Background,
                    plain.Visual.Radius,
                    plainText.Env.FontSize,
                    plainText.Inherit.Color
                )
            )
            .IsEqualTo(
                (
                    16f,
                    7f,
                    (Paint)look.Background,
                    Dark.Dimensions(Density.Pointer).Radius(RadiusToken.Md),
                    Dark.Dimensions(Density.Pointer).FontSize(TextVariant.Caption),
                    (Paint)look.Foreground
                )
            );
        await Assert
            .That(
                (
                    glyph.Bounds.Width,
                    label.ViewBounds.X - glyph.ViewBounds.Right,
                    Nodes.Find(glyph, NodeKind.Text).Inherit.Color
                )
            )
            .IsEqualTo((10f, 4f, (Paint)look.Foreground));
    }

    [Test]
    public async Task A_status_badge_takes_its_scheme_and_icon_from_its_state()
    {
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(align: Align.Start, gap: 8))
                {
                    ui.StatusBadge(text: "a", id: "pending");
                    ui.StatusBadge(text: "b", state: StatusBadgeState.Active, id: "active");
                    ui.StatusBadge(text: "c", state: StatusBadgeState.Done, id: "done");
                    ui.StatusBadge(text: "d", state: StatusBadgeState.Locked, id: "locked");
                }
            })
            .WithIcons();
        (Paint, string) Look(string id)
        {
            var node = driver.Get(id);
            var icon = driver.All<Icon>().FirstOrDefault(n => node.IsAncestorOrSelfOf(n));
            return (
                node.Visual.Background,
                icon is null ? "" : Nodes.Find(icon, NodeKind.Text).Text.ToString()
            );
        }
        static Paint Fill(ColorScheme scheme) =>
            Dark.Colors(scheme, ComponentVariant.Subtle, ComponentState.Normal).Background;

        await Assert
            .That((Look("pending"), Look("active"), Look("done"), Look("locked")))
            .IsEqualTo(
                (
                    (Fill(ColorScheme.Secondary), ""),
                    (Fill(ColorScheme.Primary), ""),
                    (
                        Fill(ColorScheme.Success),
                        TestIcons.Set.Glyph(IconRole.Check).Text.ToString()
                    ),
                    (
                        Fill(ColorScheme.Secondary),
                        TestIcons.Set.Glyph(IconRole.Lock).Text.ToString()
                    )
                )
            );
    }

    [Test]
    public async Task A_kbd_is_a_sunken_cap_thicker_along_its_bottom_spelling_its_chord_and_a_click_passes_through()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    direction: Direction.Column,
                    align: Align.Start,
                    gap: 8,
                    onClick: Callback.Of(clicks, static c => c.Value++)
                )
            )
            {
                ui.Kbd(text: "F", id: "f");
                ui.Kbd(chord: Chord.Of(Key.PageUp, ModifierKeys.Control | ModifierKeys.Shift));
                ui.Kbd(chord: Key.Escape);
            }
        });
        var f = driver.Get("f");
        var text = Nodes.Find(f, NodeKind.Text);
        driver.Click(f);

        await Assert
            .That(
                (
                    f.Layout.Border,
                    f.Visual.Background,
                    f.Bounds.Width,
                    text.ViewBounds.Y - f.ViewBounds.Y,
                    text.Inherit.Color
                )
            )
            .IsEqualTo(
                (
                    new Thickness(1f, 1f, 1f, 2f),
                    (Paint)Dark.Color(ColorToken.BgSunken),
                    22f,
                    2f,
                    (Paint)Dark.Color(ColorToken.FgStrong)
                )
            );
        await Assert.That(driver.GetText("Ctrl+Shift+PgUp")).IsNotNull();
        await Assert.That(driver.GetText("Esc")).IsNotNull();
        await Assert.That(clicks.Value).IsEqualTo(1);
    }

    sealed class BadgeModel
    {
        public int Count;
        public string? Prefix;
    }
}
