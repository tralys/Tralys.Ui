using System.Globalization;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class FormatTests
{
    [Test]
    public async Task An_enum_formats_as_its_own_ToString_does()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                ui.Text(ui.Format($"{Tone.High}"), id: "named");
                ui.Text(ui.Format($"{(Tone)7}"), id: "undefined");
                ui.Text(ui.Format($"{(Tone)(-9)}"), id: "negative");
                ui.Text(ui.Format($"{Sides.Both | Sides.Top}"), id: "flags");
                ui.Text(ui.Format($"{(Sides)9}"), id: "stray bit");
                ui.Text(ui.Format($"{Sides.Right:D}"), id: "number");
            }
        });

        await Assert.That(Shown(driver, "named")).IsEqualTo(Tone.High.ToString());
        await Assert.That(Shown(driver, "undefined")).IsEqualTo(((Tone)7).ToString());
        await Assert.That(Shown(driver, "negative")).IsEqualTo(((Tone)(-9)).ToString());
        await Assert.That(Shown(driver, "flags")).IsEqualTo((Sides.Both | Sides.Top).ToString());
        await Assert.That(Shown(driver, "stray bit")).IsEqualTo(((Sides)9).ToString());
        await Assert.That(Shown(driver, "number")).IsEqualTo(Sides.Right.ToString("D"));
    }

    [Test]
    public async Task A_number_shows_with_a_point_whatever_the_threads_culture()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            using var driver = UiDriver.Render(ui =>
            {
                using (ui.Box(direction: Direction.Column))
                {
                    ui.Text($"{0.5}", id: "text");
                    ui.Text(ui.Format($"{1.5:0.0}s"), id: "formatted");
                }
            });

            await Assert.That(Shown(driver, "text")).IsEqualTo("0.5");
            await Assert.That(Shown(driver, "formatted")).IsEqualTo("1.5s");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    static string Shown(UiDriver driver, string id) => driver.Get(id).Text.ToString();
}

public enum Tone : short
{
    Low = -2,
    Mid = 0,
    High = 5,
}

[Flags]
public enum Sides : byte
{
    None = 0,
    Left = 1,
    Right = 2,
    Both = 3,
    Top = 4,
}
