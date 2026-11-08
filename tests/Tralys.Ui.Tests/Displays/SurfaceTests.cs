using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class SurfaceTests
{
    static readonly Theme Dark = Themes.Dark;

    [Test]
    public async Task A_card_wears_the_card_gradient_in_a_muted_edge_lit_while_highlighted_and_edged_selected_over_highlighted()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 8))
            {
                using (ui.Card(width: 40, height: 40, id: "rest")) { }
                using (ui.Card(highlighted: true, width: 40, height: 40, id: "lit")) { }
                using (
                    ui.Card(selected: true, highlighted: true, width: 40, height: 40, id: "both")
                )
                    ui.Text("In", id: "inside");
            }
        });
        var rest = driver.Get("rest");
        var lit = driver.Get("lit");
        var both = driver.Get("both");

        await Assert
            .That(
                (
                    rest.Visual.Background.Kind,
                    rest.Layout.Border,
                    rest.Visual.BorderPaint,
                    rest.Visual.Radius
                )
            )
            .IsEqualTo(
                (
                    PaintKind.LinearGradient,
                    new Thickness(1f),
                    (Paint)Dark.Color(ColorToken.BorderMuted),
                    Dark.Dimensions(Density.Pointer).Radius(RadiusToken.Md)
                )
            );
        await Assert
            .That((lit.Visual.Background == rest.Visual.Background, lit.Visual.BorderPaint))
            .IsEqualTo((false, (Paint)Dark.Color(ColorToken.BorderHover)));
        await Assert
            .That((both.Visual.Background, both.Visual.BorderPaint))
            .IsEqualTo((lit.Visual.Background, (Paint)Dark.Color(ColorToken.BorderSelected)));
        await Assert.That(both.IsAncestorOrSelfOf(driver.Get("inside"))).IsTrue();
    }

    [Test]
    public async Task A_skeleton_stands_12_tall_in_the_subtle_fill_unless_sized_and_a_click_passes_through()
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
                ui.Skeleton(width: 100, id: "line");
                ui.Skeleton(width: 64, height: 40, id: "cell");
            }
        });
        var line = driver.Get("line");
        driver.Click(line);

        await Assert
            .That((line.Bounds.Size, line.Visual.Background, line.Visual.Radius))
            .IsEqualTo(
                (
                    new Size(100f, 12f),
                    (Paint)Dark.Color(ColorToken.BgSubtle),
                    Dark.Dimensions(Density.Pointer).Radius(RadiusToken.Md)
                )
            );
        await Assert.That(driver.Get("cell").Bounds.Size).IsEqualTo(new Size(64f, 40f));
        await Assert.That(clicks.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_separator_rules_a_1_unit_line_across_or_down_its_whole_run_with_8_clear_either_side_unless_spaced()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, width: 300))
            {
                ui.Separator(id: "across");
                using (ui.Box(height: 50))
                    ui.Separator(orientation: Orientation.Vertical, id: "down");
                ui.Separator(spacing: 4, id: "near");
                using (ui.Box(height: 50))
                    ui.Separator(orientation: Orientation.Vertical, spacing: 0, id: "flush");
            }
        });
        var across = driver.Get("across");
        var down = driver.Get("down");
        var line = Only(across);
        var post = Only(down);

        await Assert
            .That((across.Bounds.Height, line.ViewBounds, line.Visual.Background))
            .IsEqualTo((17f, new Rect(0f, 8f, 300f, 1f), (Paint)Dark.Color(ColorToken.Border)));
        await Assert
            .That((down.Bounds.Width, post.ViewBounds))
            .IsEqualTo((17f, new Rect(8f, 17f, 1f, 50f)));
        await Assert
            .That((driver.Get("near").Bounds.Height, driver.Get("flush").Bounds.Width))
            .IsEqualTo((9f, 1f));
    }

    [Test]
    public async Task An_empty_state_centres_its_muted_message_under_its_emblem_within_the_measure_and_shows_nothing_without_one()
    {
        var state = new EmptyModel
        {
            Text = "Nothing here yet, so add something to see it in this list.",
        };
        var clicks = new Counter();
        using var driver = UiDriver.Render(
            ui =>
            {
                using (
                    ui.Box(
                        direction: Direction.Column,
                        align: Align.Start,
                        width: 600,
                        height: 400,
                        onClick: Callback.Of(clicks, static c => c.Value++),
                        id: "area"
                    )
                )
                {
                    ui.EmptyState(
                        text: state.Text,
                        emblem: "icon",
                        noMatchText: state.NoMatch,
                        searchMiss: state.Miss,
                        backdrop: state.Backdrop,
                        id: "empty"
                    );
                    using (ui.Box(width: 10, height: 10, id: "after")) { }
                }
            },
            textures: new CountingTextures()
        );
        var empty = driver.Get("empty");
        var image = Nodes.Find(empty, NodeKind.Image);
        var message = Nodes.Find(empty, NodeKind.Text);
        var measure = Dark.Dimensions(Density.Pointer).Size(ComponentSize.Sm).Measure;
        var shown = (
            empty.Bounds.Width <= measure,
            message.Env.Color,
            message.Env.TextAlign,
            image.Bounds.Size,
            image.Visual.Opacity,
            message.ViewBounds.Y - image.ViewBounds.Bottom
        );
        driver.Click(message);

        state.Backdrop = true;
        driver.Rerender();
        var fade = First(driver.Get("empty"));
        var backdrop = (
            fade.ViewBounds.X - driver.Get("empty").ViewBounds.X,
            fade.ViewBounds.Bottom - driver.Get("empty").ViewBounds.Bottom,
            fade.Visual.Background.Kind
        );

        state.Miss = true;
        driver.Rerender();
        var missWithout = driver.Get("after").ViewBounds.Y;
        state.NoMatch = "No match";
        driver.Rerender();
        var miss = (
            driver.GetText("No match") is not null,
            Has(driver.Get("empty"), NodeKind.Image)
        );
        state.Miss = false;
        state.Text = "";
        driver.Rerender();

        await Assert
            .That(shown)
            .IsEqualTo(
                (
                    true,
                    (Paint)Dark.Color(ColorToken.FgMuted),
                    TextAlign.Center,
                    new Size(72f, 72f),
                    (float?)0.75f,
                    12f
                )
            );
        await Assert.That(clicks.Value).IsEqualTo(1);
        await Assert.That(backdrop).IsEqualTo((-40f, 24f, PaintKind.RadialGradient));
        await Assert.That(missWithout).IsEqualTo(0f);
        await Assert.That(miss).IsEqualTo((true, false));
        await Assert.That(driver.Get("after").ViewBounds.Y).IsEqualTo(0f);
    }

    static Node Only(Node node) => First(node);

    static Node First(Node node)
    {
        foreach (var child in node.Children)
            return child;
        throw new InvalidOperationException("No child.");
    }

    static bool Has(Node node, NodeKind kind)
    {
        foreach (var child in node.Children)
            if (child.Kind == kind || Has(child, kind))
                return true;
        return false;
    }

    sealed class EmptyModel
    {
        public string Text = "";
        public string? NoMatch;
        public bool Miss;
        public bool Backdrop;
    }
}
