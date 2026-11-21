using Tralys.Ui.Fonts;
using Tralys.Ui.FreeType;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class UiDriverTests
{
    [Test]
    public async Task A_click_on_a_node_covered_at_its_centre_throws_and_lands_on_it_once_the_cover_leaves_its_centre()
    {
        var cover = new Model { Count = 40 };
        var presses = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 100, height: 100))
            {
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 0,
                        top: 0,
                        width: 40,
                        height: 40,
                        onClick: Callback.Of(presses, static c => c.Value++),
                        id: "target"
                    )
                ) { }
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 0,
                        top: 0,
                        width: cover.Count,
                        height: 40,
                        background: Color.FromRgb(9, 9, 9)
                    )
                ) { }
            }
        });

        var threw = false;
        try
        {
            driver.Click(driver.Get("target"));
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        cover.Count = 15;
        driver.Settle();
        driver.Click(driver.Get("target"));

        await Assert.That(threw).IsTrue();
        await Assert.That(presses.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_driver_given_the_apps_fonts_lays_its_text_out_and_draws_it_in_them()
    {
        using var backend = new FreeTypeBackend();
        using var fonts = new FontCollection(backend);
        using var stream = await Assert
            .That(
                typeof(UiDriver).Assembly.GetManifestResourceStream(
                    "Tralys.Ui.Testing.Inter-Regular.otf"
                )
            )
            .IsNotNull();
        var face = new byte[stream.Length];
        stream.ReadExactly(face);
        using var text = new TextServices(fonts, fonts.Add(face), new Texels(), atlasPageSize: 512);

        using var driver = UiDriver.Render(static ui => ui.Text("Hello"), text: text);

        await Assert.That(text.Atlas.MissCount).IsGreaterThan(0);
    }

    sealed class Texels : ITextureFactory
    {
        public WritableTexture CreateWritable(int width, int height) => new Page(width, height);

        sealed class Page(int width, int height) : WritableTexture
        {
            public override int Width => width;

            public override int Height => height;

            public override void Write(
                int x,
                int y,
                int width,
                int height,
                ReadOnlySpan<byte> texels,
                int stride
            ) { }

            public override void Dispose() { }
        }
    }
}
