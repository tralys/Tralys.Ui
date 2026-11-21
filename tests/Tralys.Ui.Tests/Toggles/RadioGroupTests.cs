using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;
using static Tralys.Ui.Tests.ToggleLooks;

namespace Tralys.Ui.Tests;

public sealed class RadioGroupTests
{
    static readonly RadioGroupVariant[] Variants = Enum.GetValues<RadioGroupVariant>();

    [Test]
    public async Task A_press_space_or_enter_picks_an_option_in_every_look_one_already_picked_reports_nothing_and_a_group_beside_it_keeps_its_own()
    {
        foreach (var variant in Variants)
        {
            var picks = new Picks(new Choices { Picked = 2 });
            using var driver = UiDriver.Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 16))
                {
                    using (
                        ui.RadioGroup(
                            value: picks.Choices.Picked,
                            onValueChange: picks.Report,
                            variant: variant,
                            direction: Direction.Row
                        )
                    )
                        for (var i = 1; i <= 3; i++)
                            ui.Radio(value: i, label: Labels[i], id: Labels[i], key: i);
                    using (ui.RadioGroup(value: 3))
                        for (var i = 1; i <= 3; i++)
                            ui.Radio(value: i, label: Labels[i], id: "other " + Labels[i], key: i);
                }
            });

            driver.Click(driver.Get("three"));
            driver.Focus(driver.Get("one"));
            driver.Press(Key.Space);
            driver.Focus(driver.Get("two"));
            driver.Press(Key.Enter);
            driver.Click(driver.Get("two"));

            await Assert.That(string.Join(" ", picks.Log)).IsEqualTo("3 1 2");
            await Assert.That(IsPicked(driver.Get("two"))).IsTrue();
            await Assert
                .That((IsPicked(driver.Get("one")), IsPicked(driver.Get("three"))))
                .IsEqualTo((false, false));
            await Assert.That(IsPicked(driver.Get("other three"))).IsTrue();
        }
    }

    [Test]
    public async Task Tab_lands_on_the_picked_option_and_arrows_move_the_focus_and_the_pick_across_the_grid_wrapping_at_its_edges_while_ctrl_leaves_them()
    {
        var picks = new Picks(new Choices { Picked = "B" });
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.RadioGroup(
                    value: picks.Choices.Picked,
                    onValueChange: picks.Report,
                    variant: RadioGroupVariant.Segmented,
                    direction: Direction.Row,
                    rows: 2
                )
            )
                foreach (var letter in Letters)
                    ui.Radio(value: letter, label: letter, id: letter, key: letter);
        });
        string Focused() => driver.Focused?.Id ?? "none";

        driver.Tab();
        var landed = Focused();
        driver.Press(Key.Down);
        var below = (Focused(), picks.Choices.Picked.ToString());
        driver.Press(Key.Up);
        driver.Press(Key.Right);
        driver.Press(Key.Right);
        var wrapped = (Focused(), picks.Choices.Picked.ToString());
        var ctrl = driver.KeyDown(Key.Down, ModifierKeys.Control);

        await Assert.That(landed).IsEqualTo("B");
        await Assert.That(below).IsEqualTo(("E", "E"));
        await Assert.That(wrapped).IsEqualTo(("A", "A"));
        await Assert
            .That((ctrl, Focused(), string.Join(" ", picks.Log)))
            .IsEqualTo((false, "A", "E B C A"));
    }

    [Test]
    public async Task Arrows_and_home_and_end_pass_over_unavailable_options_and_an_arrow_with_nowhere_to_go_is_passed_on()
    {
        var picks = new Picks(new Choices { Picked = "A" });
        var solo = new Choices { Picked = "S" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 16))
            {
                using (
                    ui.RadioGroup(
                        value: picks.Choices.Picked,
                        onValueChange: picks.Report,
                        variant: RadioGroupVariant.Segmented,
                        direction: Direction.Row
                    )
                )
                {
                    ui.Radio(value: "X", label: "X", disabled: true, id: "X");
                    ui.Radio(value: "A", label: "A", id: "A");
                    ui.Radio(value: "B", label: "B", id: "B");
                    ui.Radio(value: "Y", label: "Y", disabled: true, id: "Y");
                }
                using (ui.RadioGroup(value: solo.Picked, onValueChange: solo.SetPicked))
                {
                    ui.Radio(value: "S", label: "Solo", id: "S");
                    ui.Radio(value: "T", label: "Off", disabled: true, id: "T");
                }
            }
        });
        string Focused() => driver.Focused?.Id ?? "none";
        driver.Focus(driver.Get("A"));

        driver.Press(Key.End);
        var end = Focused();
        driver.Press(Key.Home);
        var home = Focused();
        driver.Press(Key.Right);
        driver.Press(Key.Right);
        var wrapped = Focused();
        driver.Click(driver.Get("X"));
        driver.Focus(driver.Get("S"));
        var nowhere = driver.KeyDown(Key.Down);

        await Assert.That((end, home, wrapped)).IsEqualTo(("B", "A", "A"));
        await Assert.That(string.Join(" ", picks.Log)).IsEqualTo("B A B A");
        await Assert
            .That((nowhere, Focused(), solo.Picked.ToString()))
            .IsEqualTo((false, "S", "S"));
    }

    [Test]
    public async Task A_picked_radio_dots_its_circle_and_takes_the_accent_text_while_an_unpicked_one_hovered_takes_the_strong_text()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.RadioGroup(value: 1))
            {
                ui.Radio(value: 1, label: "one", id: "one");
                ui.Radio(value: 2, label: "two", id: "two");
            }
        });
        var one = driver.Get("one");
        var two = driver.Get("two");

        driver.Hover(two);

        await Assert
            .That((Edged(one).Visual.Background, Dot(one), Dot(two)))
            .IsEqualTo((Dark(ColorToken.BgSelected), 1f, 0f));
        await Assert
            .That((driver.GetText("one").Inherit.Color, driver.GetText("two").Inherit.Color))
            .IsEqualTo((Dark(ColorToken.FgAccent), Dark(ColorToken.FgStrong)));
    }

    [Test]
    public async Task A_picked_radios_content_takes_the_accent_text_as_its_label_would()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.RadioGroup(value: 1))
                ui.Radio(value: 1, content: Slot.Of(static ui => ui.Text("custom")));
        });

        var accent = Themes.Dark.Color(ColorToken.FgAccent);
        var glyphs = driver.Draw().Lines.Single(l => l.Contains(": glyphs 6 "));

        await Assert.That(glyphs).EndsWith($" #{accent.R:x2}{accent.G:x2}{accent.B:x2}");
    }

    [Test]
    public async Task A_segment_rings_where_its_margin_was_only_when_tabbed_to_and_lights_while_picked_hovered_or_unavailable()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (
                ui.RadioGroup(
                    value: "A",
                    variant: RadioGroupVariant.Segmented,
                    direction: Direction.Row
                )
            )
            {
                ui.Radio(value: "A", label: "A", id: "A");
                ui.Radio(value: "B", label: "B", id: "B");
                ui.Radio(value: "C", label: "C", disabled: true, id: "C");
            }
        });
        var ring = Dark(ColorToken.BorderFocus);
        bool Ringed(string id) => Has(driver.Get(id), n => n.Visual.BorderPaint == ring);
        Paint Ink(string id) => driver.GetText(id).Inherit.Color;
        var resting = driver.GetText("A").ViewBounds;

        var atRest = Ringed("A");
        driver.Tab();
        var tabbed = (Ringed("A"), driver.GetText("A").ViewBounds);
        driver.Hover(driver.Get("B"));
        var hovered = Ink("B");
        driver.Click(driver.Get("A"));

        await Assert.That((atRest, tabbed)).IsEqualTo((false, (true, resting)));
        await Assert.That(Ringed("A")).IsFalse();
        await Assert.That(Track(driver.Get("A"))).IsNotNull();
        await Assert.That(LitIn(driver.Get("A"))).IsEqualTo(Dark(ColorToken.BgAccentMuted));
        await Assert
            .That((Ink("A"), hovered, Ink("C")))
            .IsEqualTo(
                (Dark(ColorToken.FgAccent), Dark(ColorToken.FgHover), Dark(ColorToken.FgDisabled))
            );
        driver.Unhover();
        await Assert.That(Ink("B")).IsEqualTo(Dark(ColorToken.FgMuted));
    }

    [Test]
    public async Task A_tile_undims_and_frames_in_the_accent_when_picked_a_card_lights_under_the_pointer_each_rings_when_tabbed_to_and_fades_when_unavailable()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 16))
            {
                using (
                    ui.RadioGroup(
                        value: 1,
                        variant: RadioGroupVariant.Tile,
                        direction: Direction.Row
                    )
                )
                {
                    ui.Radio(value: 1, label: "Picked", id: "picked tile");
                    ui.Radio(value: 2, label: "Other", id: "tile");
                    ui.Radio(value: 3, label: "Off", disabled: true, id: "off tile");
                }
                using (ui.RadioGroup(value: 1, variant: RadioGroupVariant.Card))
                {
                    ui.Radio(value: 1, label: "Picked", id: "picked card");
                    ui.Radio(value: 2, label: "Other", id: "card");
                    ui.Radio(value: 3, label: "Off", disabled: true, id: "off card");
                }
            }
        });
        var scrim = Dark(ColorToken.BgScrim);
        float Dim(string id)
        {
            if (First(driver.Get(id), n => n.Visual.Background == scrim) is not { } dim)
                throw new InvalidOperationException($"'{id}' wears no scrim.");
            return dim.Visual.Opacity ?? 1f;
        }
        Border Frame(string id)
        {
            var edged = Edged(driver.Get(id));
            return new Border(edged.Layout.Border, edged.Visual.BorderPaint);
        }
        var card = driver.Get("card");
        var restFill = card.Visual.Background;

        driver.Hover(driver.Get("tile"));
        var hoveredTile = Frame("tile").Paint;
        driver.Hover(card);
        var lit = (card.Visual.Background != restFill, card.Visual.BorderPaint);
        driver.Unhover();
        var picked = (Frame("picked tile"), driver.Get("picked card").Visual.BorderPaint);
        driver.Tab();
        var tabbedTile = Frame("picked tile").Paint;
        driver.Tab();
        var tabbedCard = driver.Get("picked card").Visual.BorderPaint;

        await Assert.That((Dim("picked tile"), Dim("tile"))).IsEqualTo((0f, 1f));
        await Assert
            .That(picked)
            .IsEqualTo(
                (new Border(2f, Dark(ColorToken.BorderAccent)), Dark(ColorToken.BorderSelected))
            );
        await Assert
            .That((tabbedTile, tabbedCard))
            .IsEqualTo((Dark(ColorToken.BorderFocus), Dark(ColorToken.BorderFocus)));
        await Assert.That(hoveredTile).IsEqualTo(Dark(ColorToken.BorderHover));
        await Assert.That(lit).IsEqualTo((true, Dark(ColorToken.BorderHover)));
        await Assert.That(card.Visual.BorderPaint).IsEqualTo(Dark(ColorToken.BorderMuted));
        await Assert
            .That(
                (
                    driver.Get("off tile").Visual.Opacity ?? 1f,
                    driver.Get("off card").Visual.Opacity ?? 1f,
                    driver.Get("tile").Visual.Opacity ?? 1f
                )
            )
            .IsEqualTo((0.5f, 0.5f, 1f));
    }

    [Test]
    public async Task Each_look_lays_its_options_out_its_own_way_and_a_segmented_group_stands_on_its_rung_in_every_size_theme_and_density()
    {
        foreach (var theme in Themes.All)
        foreach (var density in Enum.GetValues<Density>())
        foreach (var size in Enum.GetValues<ComponentSize>())
        {
            var row = Ids("row", 3);
            var rows = Ids("rows", 4);
            var stack = Ids("stack", 3);
            using var driver = UiDriver.Render(
                ui =>
                {
                    using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 16))
                    {
                        Group(
                            ui,
                            RadioGroupVariant.Segmented,
                            size,
                            Direction.Row,
                            1,
                            0,
                            "row",
                            row
                        );
                        Group(
                            ui,
                            RadioGroupVariant.Segmented,
                            size,
                            Direction.Row,
                            2,
                            0,
                            "rows",
                            rows
                        );
                        Group(ui, RadioGroupVariant.Segmented, size, null, 0, 0, "stack", stack);
                    }
                },
                theme: theme,
                density: density
            );
            var rung = theme.Dimensions(density).Size(size).Height;

            await Assert.That(driver.Get("row").Bounds.Height).IsEqualTo(rung).Within(0.01f);
            await Assert
                .That(driver.Get("rows").Bounds.Height)
                .IsEqualTo(2f * (rung - 2f) + 2f)
                .Within(0.01f);
            await Assert
                .That(driver.Get("stack").Bounds.Height)
                .IsEqualTo(3f * (rung - 2f) + 2f)
                .Within(0.01f);
        }

        var stacked = Ids("default", 2);
        var across = Ids("across", 2);
        var tiles = Ids("tiles", 2);
        var grid = Ids("cards", 3);
        var oneRow = Ids("card row", 3);
        var equal = Ids("equal", 3);
        using var looks = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 16))
            {
                Group(
                    ui,
                    RadioGroupVariant.Default,
                    ComponentSize.Md,
                    null,
                    0,
                    0,
                    "default",
                    stacked
                );
                Group(
                    ui,
                    RadioGroupVariant.Default,
                    ComponentSize.Md,
                    Direction.Row,
                    0,
                    0,
                    "across",
                    across
                );
                Group(
                    ui,
                    RadioGroupVariant.Tile,
                    ComponentSize.Md,
                    Direction.Row,
                    0,
                    0,
                    "tiles",
                    tiles
                );
                Group(ui, RadioGroupVariant.Card, ComponentSize.Md, null, 0, 2, "cards", grid);
                Group(ui, RadioGroupVariant.Card, ComponentSize.Md, null, 0, 0, "card row", oneRow);
                Group(
                    ui,
                    RadioGroupVariant.Segmented,
                    ComponentSize.Md,
                    Direction.Row,
                    1,
                    0,
                    "equal",
                    equal
                );
            }
        });
        float Gap(string first, string second, bool across)
        {
            var a = looks.Get(first).ViewBounds;
            var b = looks.Get(second).ViewBounds;
            return across ? b.X - a.Right : b.Y - a.Bottom;
        }
        var cards = (
            Gap("cards 1", "cards 2", across: true),
            Gap("cards 1", "cards 3", across: false),
            looks.Get("card row 3").ViewBounds.Y == looks.Get("card row 1").ViewBounds.Y
        );
        var spacing = 2f * Themes.Dark.Dimensions(Density.Pointer).Spacing;

        await Assert
            .That(
                (
                    Gap("default 1", "default 2", across: false),
                    Gap("across 1", "across 2", across: true)
                )
            )
            .IsEqualTo((16f, 16f));
        await Assert.That(Gap("tiles 1", "tiles 2", across: true)).IsEqualTo(8f);
        await Assert.That(cards).IsEqualTo((spacing, spacing, true));
        await Assert
            .That(looks.Get("equal 1").Bounds.Width)
            .IsEqualTo(looks.Get("equal 3").Bounds.Width);
    }

    [FramePath]
    static void Group(
        Ui ui,
        RadioGroupVariant variant,
        ComponentSize size,
        Direction? direction,
        int rows,
        int columns,
        string id,
        string[] radios,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Box(id: id, key: key, file: file, line: line))
        using (
            ui.RadioGroup(
                value: 1,
                variant: variant,
                size: size,
                direction: direction,
                rows: rows,
                columns: columns
            )
        )
            for (var i = 0; i < radios.Length; i++)
                ui.Radio(
                    value: i + 1,
                    label: i == radios.Length - 1 ? "Widest of all" : Labels[i + 1],
                    id: radios[i],
                    key: i
                );
    }

    static string[] Ids(string id, int count)
    {
        var ids = new string[count];
        for (var i = 0; i < count; i++)
            ids[i] = $"{id} {i + 1}";
        return ids;
    }

    static readonly string[] Labels = ["", "one", "two", "three", "four", "five"];

    static readonly string[] Letters = ["A", "B", "C", "D", "E", "F"];

    static Node? Track(Node segment)
    {
        var sunken = Dark(ColorToken.BgSunken);
        for (var node = segment.Parent; node is not null; node = node.Parent)
            if (node.Visual.Background == sunken)
                return node;
        return null;
    }

    static bool IsPicked(Node radio) => (radio.States & InputStates.Selected) != 0;

    static float Dot(Node radio) =>
        Scale(
            First(
                radio,
                static n =>
                    n.Kind == NodeKind.Box && n.Visual.Background == Dark(ColorToken.IconOnAccent)
            ) ?? throw new InvalidOperationException("No dot.")
        );

    static Paint LitIn(Node segment) =>
        First(
            segment,
            static n => n.Kind == NodeKind.Box && n.Visual.Background.IsVisible
        )?.Visual.Background
        ?? default;

    sealed class Picks(Choices choices)
    {
        public readonly Choices Choices = choices;
        public readonly List<string> Log = [];
        public Callback<UiKey> Report => Callback<UiKey>.Of(this, static (p, key) => p.Picked(key));

        void Picked(UiKey key)
        {
            Log.Add(key.ToString());
            Choices.Picked = key;
        }
    }
}
