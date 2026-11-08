using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class CooldownTests
{
    [Test]
    public async Task A_cooldown_shades_what_is_left_under_tabular_heading_digits_and_rounds_up_to_whole_seconds_or_shows_tenths()
    {
        var model = new CooldownModel { Duration = 8f, Remaining = 3.4f };
        using var driver = Render(model, new Counter());
        var whole = Shown(driver);
        var digits = Nodes.Find(driver.Get("slot"), NodeKind.Text);
        var style = (digits.Env.TabularDigits, digits.Env.FontSize, digits.Env.Color);
        model.Remaining = 0.6f;
        driver.Rerender();
        var tenths = Shown(driver);
        model.Remaining = 20f;
        driver.Rerender();
        var over = Shown(driver);
        model.Duration = 0f;
        driver.Rerender();
        var unbounded = Shown(driver);
        model.Remaining = 0f;
        driver.Rerender();

        await Assert.That(whole).IsEqualTo(("4", true));
        await Assert
            .That(style)
            .IsEqualTo(
                (
                    true,
                    Themes.Dark.Dimensions(Density.Pointer).FontSize(TextVariant.Heading),
                    (Paint)Themes.Dark.Color(ColorToken.FgStrong)
                )
            );
        await Assert.That(tenths).IsEqualTo(("0.6", true));
        await Assert.That(over).IsEqualTo(("20", true));
        await Assert.That(unbounded).IsEqualTo(("20", false));
        await Assert.That(Shown(driver)).IsEqualTo(("", false));
    }

    [Test]
    public async Task A_click_on_a_cooling_slot_reaches_what_it_holds_beneath_the_shade_and_digits()
    {
        var clicks = new Counter();
        using var driver = Render(new CooldownModel { Duration = 8f, Remaining = 5f }, clicks);

        driver.Click(driver.Get("slot"));

        await Assert.That(clicks.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_cooldown_refuses_a_negative_or_unbounded_time()
    {
        float[] refused = [-1f, float.NaN, float.PositiveInfinity];
        var thrown = 0;
        foreach (var time in refused)
        {
            foreach (var asDuration in (bool[])[true, false])
            {
                try
                {
                    using var driver = UiDriver.Render(ui =>
                    {
                        using (
                            ui.Cooldown(
                                duration: asDuration ? time : 1f,
                                remaining: asDuration ? 0.5f : time
                            )
                        ) { }
                    });
                }
                catch (ArgumentOutOfRangeException)
                {
                    thrown++;
                }
            }
        }

        await Assert.That(thrown).IsEqualTo(refused.Length * 2);
    }

    static UiDriver Render(CooldownModel model, Counter clicks) =>
        UiDriver.Render(ui =>
        {
            using (
                ui.Cooldown(
                    duration: model.Duration,
                    remaining: model.Remaining,
                    width: 48,
                    height: 48,
                    id: "slot"
                )
            )
            using (
                ui.Box(
                    grow: 1,
                    background: Themes.Dark.Color(ColorToken.BgAccent),
                    onClick: Callback.Of(clicks, static c => c.Value++)
                )
            ) { }
        });

    static (string Digits, bool Shaded) Shown(UiDriver driver) =>
        (FindText(driver.Get("slot")), Shade(driver) > 0);

    static string FindText(Node node)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind == NodeKind.Text)
                return child.Text.ToString();
            if (FindText(child) is { Length: > 0 } inner)
                return inner;
        }
        return "";
    }

    static int Shade(UiDriver driver) =>
        driver
            .Draw()
            .Lines.Count(l =>
                l.Contains("Shade[", StringComparison.Ordinal)
                && l.Contains("Canvas[", StringComparison.Ordinal)
            );

    sealed class CooldownModel
    {
        public float Duration;
        public float Remaining;
    }
}
