using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class InputLookTests
{
    static Paint Dark(ColorToken token) => Themes.Dark.Color(token);

    [Test]
    public async Task The_caret_draws_in_fg_only_while_focused_editable_and_unselected_solid_again_as_it_moves_and_the_selection_only_while_focused()
    {
        var readOnly = false;
        var text = new ObservableText("abc");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(
                    text: text,
                    kind: TextInputKind.Name,
                    readOnly: readOnly,
                    width: 200,
                    id: "field"
                );
                using (ui.Box(focusable: true, width: 20, height: 20, id: "other")) { }
            }
        });
        var caret = Hex(Dark(ColorToken.Fg));
        var selection = Hex(Dark(ColorToken.BgTextSelection));

        driver.Focus(driver.TextOf("field"));
        var selected = Fills(driver);
        var keyboard = driver.TextInput?.Kind;
        driver.Press(Key.End);
        var collapsed = Fills(driver);
        driver.Frame(0.6f);
        var blinked = Editor(driver).CaretOn;
        driver.Press(Key.Right);
        var solid = Editor(driver).CaretOn;
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Focus(driver.Get("other"));
        var blurred = Fills(driver);
        readOnly = true;
        driver.Rerender();
        driver.Focus(driver.TextOf("field"));
        driver.Press(Key.End);

        await Assert.That(selected).IsEquivalentTo([selection]);
        await Assert.That(keyboard).IsEqualTo(TextInputKind.Name);
        await Assert.That(collapsed).IsEquivalentTo([caret]);
        await Assert.That((blinked, solid)).IsEqualTo((false, true));
        await Assert.That(blurred).IsEmpty();
        await Assert.That(Fills(driver)).IsEmpty();
        await Assert.That(driver.TextInput).IsNull();
    }

    [Test]
    public async Task The_caret_is_whole_device_pixels_wide_on_a_whole_device_pixel_at_a_fractional_scale()
    {
        var text = new ObservableText("hello world");
        using var driver = UiDriver.Render(ui => ui.Input(text: text, width: 200), scale: 1.5f);

        driver.CaretAt(3);

        var area = driver.TextInput!.Value.Area;
        await Assert.That(area.X).IsEqualTo(MathF.Round(area.X)).Within(0.001f);
        await Assert.That(area.Width).IsEqualTo(MathF.Round(area.Width)).Within(0.001f);
        await Assert.That(area.Width).IsGreaterThanOrEqualTo(1f);
    }

    [Test]
    public async Task Typing_hides_the_placeholder_and_shows_the_clear_button_until_emptied_disabled_or_read_only()
    {
        var entry = new Entry();
        var disabled = false;
        var readOnly = false;
        using var driver = UiDriver.Render(ui =>
            ui.Input(
                text: entry.Text,
                placeholder: "Name",
                clearable: true,
                disabled: disabled,
                readOnly: readOnly,
                width: 200
            )
        );
        var placeholder = Hex(Dark(ColorToken.FgPlaceholder));
        driver.Focus(driver.TextOf());
        var empty = (Glyphs(driver), driver.All<InputClear>().Count);

        driver.Type("a");
        var typed = (Glyphs(driver), driver.All<InputClear>().Count);
        driver.Press(Key.BackSpace);
        var emptied = (Glyphs(driver), driver.All<InputClear>().Count);
        driver.Type("b");
        disabled = true;
        driver.Rerender();
        var off = driver.All<InputClear>().Count;
        disabled = false;
        readOnly = true;
        driver.Rerender();

        await Assert.That(empty).IsEqualTo((placeholder, 0));
        await Assert.That(typed.Item2).IsEqualTo(1);
        await Assert.That(typed.Item1).IsNotEqualTo(placeholder);
        await Assert.That(emptied).IsEqualTo((placeholder, 0));
        await Assert.That((off, driver.All<InputClear>().Count)).IsEqualTo((0, 0));
    }

    [Test]
    public async Task The_count_follows_the_text_in_characters_or_utf8_bytes_and_shows_only_within_its_threshold()
    {
        var chars = new Entry();
        var bytes = new Entry();
        var near = new Entry();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(text: chars.Text, maxLength: 10, id: "chars");
                ui.Input(text: bytes.Text, maxBytes: 4, id: "bytes");
                ui.Input(text: near.Text, maxLength: 10, countWithin: 3, id: "near");
            }
        });

        driver.Focus(driver.TextOf("chars"));
        driver.Type("ab");
        driver.Focus(driver.TextOf("bytes"));
        driver.Type("é");
        driver.Focus(driver.TextOf("near"));
        driver.Type("abcdef");
        var far = Texts(driver.Get<Input>("near"));
        var width = driver.RootOf("near").Bounds.Width;
        driver.Type("g");

        await Assert.That(Texts(driver.Get<Input>("chars"))).IsEquivalentTo(["2/10"]);
        await Assert.That(Texts(driver.Get<Input>("bytes"))).IsEquivalentTo(["2/4"]);
        await Assert.That(far).IsEmpty();
        await Assert.That(Texts(driver.Get<Input>("near"))).IsEquivalentTo(["7/10"]);
        await Assert.That(driver.RootOf("near").Bounds.Width).IsGreaterThan(width);
    }

    [Test]
    public async Task An_input_outlines_in_the_disabled_colour_over_invalid_muted_while_read_only_and_flush_has_only_a_square_rule_beneath()
    {
        var blank = new ObservableText();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                {
                    ui.Input(
                        text: blank,
                        invalid: true,
                        disabled: true,
                        leadingIcon: IconGlyph.Of(IconRole.Search),
                        width: 200,
                        id: "disabled"
                    );
                    ui.Input(text: blank, readOnly: true, width: 200, id: "read-only");
                    ui.Input(text: blank, variant: FieldVariant.Flush, width: 200, id: "flush");
                }
            })
            .WithIcons();
        var disabled = driver.RootOf("disabled");
        var flush = driver.RootOf("flush");

        await Assert
            .That(
                (
                    disabled.Visual.BorderPaint,
                    driver.RootOf("read-only").Visual.BorderPaint,
                    FirstChild(Nodes.Glyph(disabled)).Inherit.Color
                )
            )
            .IsEqualTo(
                (
                    Dark(ColorToken.BorderDisabled),
                    Dark(ColorToken.BorderMuted),
                    Dark(ColorToken.IconDisabled)
                )
            );
        await Assert
            .That((flush.Layout.Border, flush.Visual.Radius))
            .IsEqualTo((new Thickness(0f, 0f, 0f, 1f), default(CornerRadius)));
    }

    [Test]
    public async Task Centred_or_right_aligned_text_and_placeholder_sit_in_the_middle_or_against_the_end_until_longer_than_the_field()
    {
        var ab = new ObservableText("ab");
        var blank = new ObservableText();
        var longer = new ObservableText("a line of text far longer than this field is wide");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(text: ab, width: 200, id: "left");
                ui.Input(text: ab, textAlign: TextAlign.Center, width: 200, id: "centre");
                ui.Input(text: ab, textAlign: TextAlign.Right, width: 200, id: "right");
                ui.Input(
                    text: blank,
                    placeholder: "ab",
                    textAlign: TextAlign.Center,
                    width: 200,
                    id: "hint"
                );
                ui.Input(text: longer, textAlign: TextAlign.Center, width: 200, id: "long");
            }
        });
        var room = driver.TextOf("left").Bounds.Width;

        await Assert.That(GlyphX(driver, "left")).IsLessThan(1f);
        await Assert
            .That(GlyphX(driver, "centre"))
            .IsGreaterThan(room / 2f - 20f)
            .And.IsLessThan(room / 2f);
        await Assert.That(GlyphX(driver, "right")).IsGreaterThan(room - 25f);
        await Assert.That(GlyphX(driver, "hint")).IsEqualTo(GlyphX(driver, "centre")).Within(0.5f);
        await Assert.That(GlyphX(driver, "long")).IsLessThan(1f);
    }

    [Test]
    public async Task Its_slots_show_inside_its_border_follow_app_state_and_keep_their_presses_from_its_text()
    {
        var model = new Model { Name = "first" };
        var clicks = new Counter();
        var text = new ObservableText("text");
        using var driver = UiDriver.Render(ui =>
            ui.Input(
                text: text,
                width: 240,
                leading: Slot.Of(model, static (ui, m) => ui.Text(m.Name)),
                trailing: Slot.Of(
                    clicks,
                    static (ui, c) =>
                    {
                        using (
                            ui.Box(
                                width: 20,
                                height: 20,
                                onClick: Callback.Of(c, static c => c.Value++),
                                id: "go"
                            )
                        ) { }
                    }
                )
            )
        );
        var root = driver.RootOf().ViewBounds;
        var label = driver.GetText("first").ViewBounds;

        model.Name = "second";
        driver.Settle();
        driver.Click(driver.Get("go"));

        await Assert.That(root.Contains(new Vector2(label.X, label.Y))).IsTrue();
        await Assert.That(driver.GetText("second")).IsNotNull();
        await Assert.That(clicks.Value).IsEqualTo(1);
        await Assert.That(driver.Focused).IsNotSameReferenceAs(driver.TextOf());
    }

    [Test]
    public async Task Inputs_and_search_inputs_stand_their_rungs_height_in_every_size_theme_and_density()
    {
        var wrong = new List<string>();
        var sizes = Enum.GetValues<ComponentSize>();
        var blank = new ObservableText();
        foreach (var theme in Themes.All)
        foreach (var density in Enum.GetValues<Density>())
        {
            using var driver = UiDriver
                .Render(
                    ui =>
                    {
                        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                            foreach (var size in sizes)
                            {
                                ui.Input(
                                    text: blank,
                                    placeholder: "Name",
                                    size: size,
                                    key: (long)size
                                );
                                ui.SearchInput(
                                    text: blank,
                                    placeholder: "Name",
                                    size: size,
                                    key: (long)size
                                );
                            }
                    },
                    size: new Size(1600f, 900f),
                    theme: theme,
                    density: density
                )
                .WithIcons();
            var inputs = driver.All<Input>();
            for (var i = 0; i < inputs.Count; i++)
            {
                var size = sizes[i / 2];
                var height = theme.Dimensions(density).Size(size).Height;
                var root = Nodes.Find(inputs[i], NodeKind.Box).Bounds.Height;
                if (MathF.Abs(root - height) > 0.01f)
                    wrong.Add($"{theme.Name} {density} {size} #{i}: {root} not {height}");
            }
        }

        await Assert.That(wrong).IsEmpty();
    }

    [Test]
    public async Task A_wrapping_input_grows_from_the_top_inside_its_padding_while_one_line_stands_on_its_rung_and_text_sizes_an_unsized_one()
    {
        var text = new ObservableText(
            "A note that runs on well past the end of its first line, so it wraps."
        );
        var wide = new ObservableText();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(text: text, size: ComponentSize.Sm, width: 160, id: "line");
                ui.Input(text: text, wrap: true, size: ComponentSize.Sm, width: 160, id: "wrap");
                ui.Input(text: wide, id: "sized");
            }
        });
        var m = Themes.Dark.Dimensions(Density.Pointer).Size(ComponentSize.Sm);
        var wrap = driver.RootOf("wrap").ViewBounds;
        var at = driver.TextOf("wrap").ViewBounds;
        var empty = driver.RootOf("sized").Bounds.Width;

        wide.Set("wider");
        driver.Rerender();

        await Assert.That(driver.RootOf("line").Bounds.Height).IsEqualTo(m.Height).Within(0.01f);
        await Assert.That(wrap.Height).IsGreaterThan(2f * m.Height);
        await Assert.That((at.X - wrap.X, at.Y - wrap.Y)).IsEqualTo((m.FieldPadX, m.PadY));
        await Assert.That(driver.RootOf("sized").Bounds.Width).IsGreaterThan(empty + 20f);
    }

    [Test]
    public async Task A_wrapping_inputs_count_overlays_its_last_line_rather_than_narrowing_its_lines()
    {
        var lines = new ObservableText("one\ntwo");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(text: lines, multiline: true, maxLength: 100, width: 200, id: "counted");
                ui.Input(text: lines, multiline: true, width: 200, id: "plain");
            }
        });
        var text = driver.TextOf("counted").ViewBounds;
        var count = driver.GetText("7/100").ViewBounds;

        await Assert.That(text.Width).IsEqualTo(driver.TextOf("plain").Bounds.Width);
        await Assert.That(count.Right).IsLessThanOrEqualTo(text.Right + 12f);
        await Assert.That(count.Bottom).IsGreaterThan(text.Y + text.Height / 2f);
    }

    [Test]
    public async Task It_scrolls_to_keep_the_caret_past_trailing_spaces_and_down_its_lines_back_to_the_start_and_clips_to_its_text_area()
    {
        var line = new Entry("a line of text far longer than the field");
        var numbers = new ObservableText("1\n2\n3\n4\n5\n6");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(text: line.Text, width: 84, id: "line");
                ui.Input(text: numbers, multiline: true, width: 84, height: 40, id: "lines");
            }
        });
        var text = driver.TextOf("line").ViewBounds;

        driver.CaretAt(0, "line");
        driver.Press(Key.End);
        driver.Type(" ");
        var spaced = driver.TextInput!.Value.Area;
        var shown = Editor(driver, "line").CaretShown;
        var clipped = driver.Draw().Lines.First(static l => l.Contains("/EditableText"));
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("a");
        var restarted = driver.TextInput!.Value.Area;
        driver.CaretAt(0, "lines");
        driver.Press(Key.End, ModifierKeys.Control);
        var lines = driver.TextOf("lines").ViewBounds;
        var bottom = driver.TextInput!.Value.Area;
        driver.Click(
            driver.RootOf("lines"),
            new Vector2(14f, driver.RootOf("lines").Bounds.Height - 8f)
        );

        await Assert.That(spaced.Right).IsLessThanOrEqualTo(text.Right);
        await Assert.That(shown).IsTrue();
        await Assert.That(clipped).Contains(": clip 0,0 ");
        await Assert.That(restarted.X).IsLessThan(text.X + 10f);
        await Assert.That(bottom.Bottom).IsLessThanOrEqualTo(lines.Bottom);
        await Assert.That(driver.Selection("lines")).IsEqualTo((10, 10));
    }

    [Test]
    public async Task An_autofocus_input_selects_all_or_keeping_its_selection_puts_the_caret_at_the_end()
    {
        var selected = new ObservableText("prefilled");
        var kept = new ObservableText("prefilled");
        using var selecting = UiDriver.Render(ui =>
            ui.Input(text: selected, autoFocus: true, width: 200)
        );
        using var keeping = UiDriver.Render(ui =>
            ui.Input(text: kept, autoFocus: true, keepSelectionOnFocus: true, width: 200)
        );

        await Assert.That(selecting.Selection()).IsEqualTo((0, 9));
        await Assert.That(keeping.Focused).IsSameReferenceAs(keeping.TextOf());
        await Assert.That(keeping.Selection()).IsEqualTo((9, 9));
    }

    [Test]
    public async Task An_autofocus_input_shown_again_takes_the_focus_but_not_when_an_ancestor_is_shown_again()
    {
        var input = false;
        var around = false;
        var text = new ObservableText("auto");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (ui.Box(focusable: true, width: 20, height: 20, id: "other")) { }
                using (ui.Box(collapsed: around))
                    ui.Input(text: text, autoFocus: true, collapsed: input, width: 200);
            }
        });

        driver.Click(driver.Get("other"));
        input = true;
        driver.Rerender();
        input = false;
        driver.Rerender();
        var itself = driver.Focused;
        driver.Click(driver.Get("other"));
        around = true;
        driver.Rerender();
        around = false;
        driver.Rerender();

        await Assert.That(itself).IsSameReferenceAs(driver.TextOf());
        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("other"));
    }

    static EditorPayload Editor(UiDriver driver, string? id = null) =>
        (EditorPayload)driver.TextOf(id).Payload!;

    static List<string> Fills(UiDriver driver)
    {
        var fills = new List<string>();
        foreach (var line in driver.Draw().Lines)
        {
            var at = line.IndexOf(" fill #", StringComparison.Ordinal);
            if (line.Contains("/EditableText", StringComparison.Ordinal) && at >= 0)
                fills.Add(line[(at + 6)..]);
        }
        return fills;
    }

    static string Glyphs(UiDriver driver)
    {
        foreach (var line in driver.Draw().Lines)
            if (
                line.Contains("/EditableText", StringComparison.Ordinal)
                && line.Contains(": glyphs ")
            )
                return line[(line.LastIndexOf(' ') + 1)..];
        return "";
    }

    static float GlyphX(UiDriver driver, string id)
    {
        var path = driver.TextOf(id);
        foreach (var line in driver.Draw().Lines)
        {
            var at = line.IndexOf(": glyphs ", StringComparison.Ordinal);
            if (
                at < 0
                || !line.Contains($"Box#{id}/", StringComparison.Ordinal)
                || !line.Contains("/EditableText")
            )
                continue;
            var start = line.IndexOf(" in ", at, StringComparison.Ordinal) + 4;
            return float.Parse(
                line[start..line.IndexOf(',', start)],
                System.Globalization.CultureInfo.InvariantCulture
            );
        }
        throw new InvalidOperationException($"'{id}' draws no glyphs.");
    }

    static Node FirstChild(Node node)
    {
        foreach (var child in node.Children)
            return child;
        throw new InvalidOperationException("No child.");
    }

    static List<string> Texts(Node within)
    {
        var found = new List<string>();
        Collect(within, found);
        return found;

        static void Collect(Node node, List<string> into)
        {
            foreach (var child in node.Children)
            {
                if (child.Kind == NodeKind.Text)
                    into.Add(child.Text.ToString());
                Collect(child, into);
            }
        }
    }

    static string Hex(Paint paint)
    {
        var c = paint.Color;
        var hex = $"#{c.R:x2}{c.G:x2}{c.B:x2}";
        return c.A == 255 ? hex : hex + $"{c.A:x2}";
    }
}
