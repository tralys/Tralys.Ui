using System.Globalization;
using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class ArtButtonTests
{
    [Test]
    public async Task A_close_button_is_a_round_disc_that_lights_with_its_cross_while_hovered()
    {
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(padding: 40))
                using (ui.Box(width: 120, height: 100))
                    ui.CloseButton(position: Position.Absolute, top: -10, right: -10, id: "close");
            })
            .WithIcons();
        var t = Themes.Dark;
        var m = t.Dimensions(Density.Pointer).Size(ComponentSize.Md);
        var close = driver.Get("close");
        var backdrop = t.Color(ColorToken.BgBackdrop);

        var resting = (
            close.Visual.Background,
            close.Visual.BorderPaint,
            close.Visual.Radius,
            Crosses(driver, t.Color(ColorToken.FgMuted))
        );
        driver.Hover(close);
        var lit = (close.Visual.Background, Crosses(driver, t.Color(ColorToken.FgHover)));
        driver.Unhover();

        await Assert
            .That(resting)
            .IsEqualTo(
                (
                    (Paint)backdrop,
                    (Paint)t.Color(ColorToken.BorderOverlay),
                    new CornerRadius(m.Height / 2f),
                    true
                )
            );
        await Assert
            .That(lit)
            .IsEqualTo(((Paint)t.Color(ColorToken.StateHover).Over(backdrop), true));
        await Assert.That(close.Visual.Background).IsEqualTo((Paint)backdrop);
    }

    [Test]
    public async Task A_close_button_wears_the_skins_close_art_in_place_of_its_disc_and_its_hover_art_while_hovered()
    {
        using var driver = UiDriver.Render(ui => ui.CloseButton(id: "close")).WithIcons();
        driver.View.Context.Skin = new Skin([
            new(SkinPart.CloseButton, new SkinPartImage(new TestTexture(16, 16), new Thickness(4))),
            new(
                SkinPart.CloseButtonHover,
                new SkinPartImage(new TestTexture(16, 16), new Thickness(4))
            ),
        ]);
        driver.Settle();
        var close = driver.Get("close");

        var resting = (
            ButtonLooks.Images(close),
            close.Visual.Background,
            close.Visual.BorderPaint,
            Has(close, NodeKind.Text)
        );
        driver.Hover(close);

        await Assert.That(resting).IsEqualTo((1, default(Paint), default(Paint), false));
        await Assert.That(ButtonLooks.Images(close)).IsEqualTo(2);
    }

    [Test]
    public async Task An_image_button_shows_its_pressed_art_while_held_over_its_active_art_and_takes_a_press_anywhere()
    {
        var rest = new TestTexture(40, 20);
        var active = new TestTexture(50, 10);
        var pressed = new TestTexture(30, 30);
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 8))
            {
                ui.ImageButton(source: rest, pressedSource: pressed, id: "plain");
                ui.ImageButton(
                    source: rest,
                    activeSource: active,
                    pressedSource: pressed,
                    active: true,
                    id: "active"
                );
                ui.ImageButton(source: rest, activeSource: active, active: true, id: "unpressed");
                ui.ImageButton(
                    source: "loading",
                    width: 40,
                    height: 40,
                    onClick: Callback.Of(clicks, static c => c.Value++),
                    id: "loading"
                );
            }
        });
        Size Shown(string id) => Nodes.Find(driver.Get(id), NodeKind.Image).Bounds.Size;

        var plain = driver.Hold(driver.Get("plain"));
        var plainHeld = Shown("plain");
        plain.Release();
        var activeHeld = Held(driver, "active", Shown);
        var unpressedHeld = Held(driver, "unpressed", Shown);
        driver.Click(driver.Get("loading"));

        await Assert.That(plainHeld).IsEqualTo(new Size(30f, 30f));
        await Assert.That(Shown("plain")).IsEqualTo(new Size(40f, 20f));
        await Assert
            .That((Shown("active"), activeHeld))
            .IsEqualTo((new Size(50f, 10f), new Size(30f, 30f)));
        await Assert.That(unpressedHeld).IsEqualTo(new Size(50f, 10f));
        await Assert.That(clicks.Value).IsEqualTo(1);
    }

    static Size Held(UiDriver driver, string id, Func<string, Size> shown)
    {
        var press = driver.Hold(driver.Get(id));
        var size = shown(id);
        press.Release();
        return size;
    }

    static bool Has(Node node, NodeKind kind)
    {
        foreach (var child in node.Children)
            if (child.Kind == kind || Has(child, kind))
                return true;
        return false;
    }

    static bool Crosses(UiDriver driver, Color color)
    {
        var hex = string.Create(
            CultureInfo.InvariantCulture,
            $"#{color.R:x2}{color.G:x2}{color.B:x2}{(color.A == 255 ? "" : color.A.ToString("x2", CultureInfo.InvariantCulture))}"
        );
        return driver
            .Draw()
            .Lines.Any(l =>
                l.Contains("/Icon[", StringComparison.Ordinal)
                && l.Contains(hex, StringComparison.Ordinal)
            );
    }
}
