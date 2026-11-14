using System.Numerics;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Tests;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components.Tests;

public sealed class ColorPickerTests
{
    static readonly float Knob = Themes
        .Dark.Dimensions(Density.Pointer)
        .Size(ComponentSize.Md)
        .IconSize;

    static readonly Color Red = Color.FromRgb(0xFF, 0x00, 0x00);

    static UiDriver Render(
        Colors colors,
        bool alpha = false,
        IReadOnlyList<Color>? swatches = null
    ) =>
        UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.ColorPicker(
                    value: colors.Value,
                    onValueChange: colors.OnValueChange,
                    swatches: swatches,
                    showAlpha: alpha,
                    width: 300
                );
                using (ui.Box(width: 40, height: 20, focusable: true, id: "after")) { }
            }
        });

    static Node Picker(UiDriver driver) => Menus.RootOf(driver.Get<ColorPicker>());

    static Node Column(UiDriver driver) => Kids.Of(Picker(driver))[^1];

    static Node Square(UiDriver driver) => Kids.Of(Column(driver))[0];

    static Node Hue(UiDriver driver) => Kids.Of(Column(driver))[1];

    static Node Alpha(UiDriver driver) => Kids.Of(Column(driver))[2];

    static Node KnobOf(Node part) => Kids.Of(Kids.Of(part)[^1])[0];

    static Node HexText(UiDriver driver) => Nodes.Find(driver.Get<Input>(), NodeKind.EditableText);

    static Vector2 At(Node part, float x, float y)
    {
        var b = part.ViewBounds;
        return new(b.X + Knob / 2f + x * (b.Width - Knob), b.Y + Knob / 2f + y * (b.Height - Knob));
    }

    static void ClickAt(UiDriver driver, Node part, float x, float y) =>
        driver.Click(part, At(part, x, y) - new Vector2(part.ViewBounds.X, part.ViewBounds.Y));

    [Test]
    public async Task The_hue_bar_and_square_set_the_colour_from_the_pointer_keeping_the_hue_through_near_black_and_the_hex_box_follows()
    {
        var colors = new Colors { Value = Red with { A = 0x80 } };
        using var driver = Render(colors);

        ClickAt(driver, Hue(driver), 100f / 360f, 0.5f);
        var hued = (colors.Value, Square(driver).Visual.Background);
        var held = driver.Hold(Square(driver));
        held.MoveBy(new Vector2(400f, 400f));
        var black = (colors.Value, HexText(driver).Text.ToString());
        held.MoveBy(At(Square(driver), 1f, 0.98f) - held.Position);
        var dim = colors.Value;
        held.MoveBy(At(Square(driver), 1f, 0f) - held.Position);
        held.Release();

        await Assert
            .That(hued)
            .IsEqualTo(
                (Color.FromRgb(0x55, 0xFF, 0x00), Paint.Solid(Color.FromRgb(0x55, 0xFF, 0x00)))
            );
        await Assert.That(black).IsEqualTo((Color.FromRgb(0, 0, 0), "#000000"));
        await Assert.That(dim).IsEqualTo(Color.FromRgb(0x02, 0x05, 0x00));
        await Assert.That(colors.Value).IsEqualTo(Color.FromRgb(0x55, 0xFF, 0x00));
        await Assert.That(HexText(driver).Text.ToString()).IsEqualTo("#55FF00");
    }

    [Test]
    public async Task Only_the_first_left_pointer_drags_and_after_its_release_over_nothing_hovering_changes_nothing()
    {
        var colors = new Colors { Value = Red };
        using var driver = Render(colors);

        driver.Click(Square(driver), MouseButton.Right);
        driver.Click(Square(driver), MouseButton.Middle);
        var others = colors.Reports.Count;
        var mouse = driver.Hold(Square(driver));
        var pressed = colors.Reports.Count;
        var finger = driver.Hold(Hue(driver), kind: PointerKind.Touch);
        finger.MoveBy(new Vector2(-40f, 0f));
        finger.Release();
        var fingered = colors.Reports.Count;
        mouse.MoveBy(new Vector2(30f, 0f));
        mouse.MoveBy(new Vector2(600f, 300f));
        mouse.Release();
        var released = colors.Reports.Count;
        driver.Hover(Square(driver));

        await Assert.That(others).IsEqualTo(0);
        await Assert.That(fingered).IsEqualTo(pressed);
        await Assert.That(released).IsGreaterThan(pressed);
        await Assert.That(colors.Reports.Count).IsEqualTo(released);
    }

    [Test]
    public async Task Its_knobs_sit_flush_with_their_ends_at_the_default_colour_and_past_a_bars_end()
    {
        var colors = new Colors();
        using var driver = Render(colors);
        var square = Square(driver).ViewBounds;
        var knob = KnobOf(Square(driver)).ViewBounds;

        var held = driver.Hold(Hue(driver));
        held.MoveBy(new Vector2(500f, 0f));
        held.Release();
        var hue = KnobOf(Hue(driver)).ViewBounds;

        await Assert.That(knob.Bottom).IsEqualTo(square.Bottom).Within(0.5f);
        await Assert.That(knob.Y).IsGreaterThanOrEqualTo(square.Y);
        await Assert.That(hue.Right).IsEqualTo(Hue(driver).ViewBounds.Right).Within(0.5f);
    }

    [Test]
    public async Task A_swatch_picks_only_for_a_press_that_started_on_it_and_over_a_clear_colour_brings_its_own_alpha()
    {
        var colors = new Colors();
        Color[] swatches = [Color.FromArgb(0, 0, 0, 0), Color.FromRgb(0x66, 0xCC, 0xFF)];
        using var driver = Render(colors, alpha: true, swatches: swatches);
        static Node Swatch(UiDriver d, int i) => d.All<ColorPickerSwatch>()[i];

        var held = driver.Hold(Swatch(driver, 0));
        held.MoveTo(Swatch(driver, 1));
        held.Release();
        var slid = colors.Reports.Count;
        driver.Click(Swatch(driver, 1));
        var blue = colors.Value;
        driver.Click(Swatch(driver, 0));

        await Assert.That(slid).IsEqualTo(0);
        await Assert.That(blue).IsEqualTo(Color.FromRgb(0x66, 0xCC, 0xFF));
        await Assert.That(colors.Reports.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_typed_hex_is_the_colour_at_once_and_the_box_shows_the_colours_hex_once_enter_or_the_focus_lets_go()
    {
        var colors = new Colors { Value = Red };
        using var driver = Render(colors);
        var text = HexText(driver);
        driver.Focus(text);
        void Replace(string typed)
        {
            driver.Press(Key.A, ModifierKeys.Control);
            driver.Type(typed);
        }

        Replace("#00ff00");
        var typed = (colors.Value, text.Text.ToString());
        driver.Press(Key.Enter);
        var entered = text.Text.ToString();
        Replace("zz");
        Replace("#0000ff80");
        var refused = colors.Reports.Count;
        Replace("#12");
        colors.Value = Red;
        driver.Rerender();
        var kept = text.Text.ToString();
        Replace("0000ff");
        driver.Focus(driver.Get("after"));

        await Assert.That(typed).IsEqualTo((Color.FromRgb(0, 0xFF, 0), "#00ff00"));
        await Assert.That(entered).IsEqualTo("#00FF00");
        await Assert.That(refused).IsEqualTo(1);
        await Assert.That(kept).IsEqualTo("#12");
        await Assert.That(colors.Value).IsEqualTo(Color.FromRgb(0, 0, 0xFF));
        await Assert.That(text.Text.ToString()).IsEqualTo("#0000FF");
    }

    [Test]
    public async Task Its_hex_is_eight_digits_only_for_a_translucent_colour_with_its_opacity_bar_shown()
    {
        var colors = new Colors { Value = Color.FromArgb(0x80, 0xC0, 0x50, 0x28) };
        var alpha = new Switches { A = true };
        using var driver = UiDriver.Render(ui =>
            ui.ColorPicker(value: colors.Value, showAlpha: alpha.A, width: 300)
        );

        var translucent = HexText(driver).Text.ToString();
        alpha.A = false;
        driver.Rerender();
        var hidden = HexText(driver).Text.ToString();
        colors.Value = Color.FromRgb(0xC0, 0x50, 0x28);
        alpha.A = true;
        driver.Rerender();

        await Assert.That((translucent, hidden)).IsEqualTo(("#C0502880", "#C05028"));
        await Assert.That(HexText(driver).Text.ToString()).IsEqualTo("#C05028");
    }

    [Test]
    public async Task A_grey_or_black_from_outside_moves_the_square_knob_and_keeps_the_hue()
    {
        var colors = new Colors { Value = Color.FromRgb(0x55, 0xFF, 0x00) };
        using var driver = Render(colors);
        var hue = KnobOf(Hue(driver)).ViewBounds.X;
        Rect Knob() => KnobOf(Square(driver)).ViewBounds;

        colors.Value = Color.FromRgb(0x80, 0x80, 0x80);
        driver.Rerender();
        var grey = Knob();
        colors.Value = Color.FromRgb(0, 0, 0);
        driver.Rerender();
        var square = Square(driver).ViewBounds;

        await Assert.That(grey.X).IsEqualTo(square.X);
        await Assert
            .That(grey.Y + grey.Height / 2f)
            .IsEqualTo(square.Y + square.Height / 2f)
            .Within(1f);
        await Assert.That(KnobOf(Hue(driver)).ViewBounds.X).IsEqualTo(hue);
        await Assert.That((Knob().X, Knob().Bottom)).IsEqualTo((square.X, square.Bottom));
    }

    [Test]
    public async Task A_press_the_window_cut_short_leaves_the_next_press_dragging_what_it_lands_on()
    {
        var colors = new Colors { Value = Red };
        using var driver = Render(colors, alpha: true);

        ClickAt(driver, Alpha(driver), 0.4f, 0.5f);
        var faded = colors.Value.A;
        var held = driver.Hold(Square(driver));
        held.MoveBy(At(Square(driver), 0.5f, 0.2f) - held.Position);
        driver.WindowFocus(false);
        held.LoseRelease();
        driver.WindowFocus(true);
        ClickAt(driver, Hue(driver), 120f / 360f, 0.5f);

        await Assert.That(faded).IsEqualTo((byte)0x66);
        await Assert.That(colors.Value).IsEqualTo(Color.FromArgb(0x66, 0x66, 0xCC, 0x66));
    }

    [Test]
    public async Task A_finger_on_its_square_inside_a_panning_scroll_area_belongs_to_the_picker()
    {
        var colors = new Colors { Value = Red };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.ScrollArea(panning: Panning.Both, width: 200, height: 200, id: "area"))
            using (ui.Box(width: 600, height: 600, padding: 40))
                ui.ColorPicker(
                    value: colors.Value,
                    onValueChange: colors.OnValueChange,
                    width: 300
                );
        });
        var square = Square(driver).ViewBounds;

        driver.Drag(Square(driver), new Vector2(-30f, -30f), kind: PointerKind.Touch);

        await Assert.That(colors.Reports.Count).IsGreaterThan(1);
        await Assert.That(Square(driver).ViewBounds).IsEqualTo(square);
    }

    [Test]
    public async Task Its_select_opens_a_picker_whose_swatch_closes_it_unless_it_stays_open_keeping_the_edited_alpha_and_restoring_the_previous_colour()
    {
        var clear = new Colors();
        var kept = new Colors { Value = Color.FromArgb(0x80, 0xFF, 0x55, 0x00) };
        var open = new Reports();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(gap: 200, padding: 20, align: Align.Start))
            {
                ui.ColorPickerSelect(
                    value: clear.Value,
                    onValueChange: clear.OnValueChange,
                    onOpenChange: open.OnOpenChange,
                    showAlpha: true,
                    id: "clear"
                );
                ui.ColorPickerSelect(
                    value: kept.Value,
                    onValueChange: kept.OnValueChange,
                    staysOpen: true,
                    showAlpha: true,
                    showPrevious: true,
                    id: "kept"
                );
            }
        });
        static Node Blue(UiDriver d) =>
            d.All<ColorPickerSwatch>()
                .Single(s =>
                    s.Props<ColorPickerSwatchProps>().Color == Color.FromRgb(0x66, 0xCC, 0xFF)
                );

        driver.Click(Menus.RootOf(Menus.RootOf(driver.Get<ColorPicker.Select>("clear"))));
        var trigger = OverlayProbes.BoxOf(driver.Get<ColorPicker.Select>("clear")).ViewBounds;
        var frame = driver.RootOf<Overlay.Frame>().ViewBounds;
        driver.Click(Blue(driver));
        var closed = driver.View.Layers.Length;
        driver.Click(Menus.RootOf(Menus.RootOf(driver.Get<ColorPicker.Select>("kept"))));
        driver.Click(Blue(driver));
        var ringed = Blue(driver).Props<ColorPickerSwatchProps>().Selected;
        driver.Click(driver.Get<Tooltip>());

        await Assert.That(frame.Y).IsGreaterThanOrEqualTo(trigger.Bottom);
        await Assert.That(clear.Reports).IsEquivalentTo([Color.FromRgb(0x66, 0xCC, 0xFF)]);
        await Assert.That(open.Opens).IsEquivalentTo([true, false]);
        await Assert.That(closed).IsEqualTo(0);
        await Assert.That(ringed).IsTrue();
        await Assert
            .That(kept.Reports)
            .IsEquivalentTo([
                Color.FromArgb(0x80, 0x66, 0xCC, 0xFF),
                Color.FromArgb(0x80, 0xFF, 0x55, 0x00),
            ]);
        await Assert.That(driver.All<ColorPicker>().Count).IsEqualTo(1);
    }
}

internal sealed partial class Colors : Tralys.Ui.Mvvm.ObservableObject
{
    public readonly List<Color> Reports = [];

    [Tralys.Ui.Mvvm.ObservableProperty]
    public partial Color Value { get; set; }

    public Callback<Color> OnValueChange =>
        Callback<Color>.Of(
            this,
            static (c, color) =>
            {
                c.Reports.Add(color);
                c.Value = color;
            }
        );
}
