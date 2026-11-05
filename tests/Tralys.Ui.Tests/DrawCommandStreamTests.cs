using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class DrawCommandStreamTests
{
    [Test]
    public async Task What_the_view_pushes_itself_is_under_view_ahead_of_what_its_nodes_draw()
    {
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(width: 10, height: 10, background: Color.FromRgb(9, 9, 9), id: "box"))
                { }
            },
            new Size(50, 50),
            scale: 2
        );

        var lines = driver.Draw().Lines;

        await Assert.That(lines[0]).StartsWith("(view): ");
        await Assert.That(lines[1]).Contains("Box#box: ");
    }

    [Test]
    public async Task A_text_with_a_shadow_draws_its_glyphs_over_their_shadow_and_one_without_draws_none()
    {
        var shadow = new DropShadow(Color.FromRgb(0, 0, 0), 270f, 1f, 8f, 0.9f);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                ui.Text("Caption", shadow: shadow, id: "caption");
                ui.Text("Plain", id: "plain");
            }
        });

        var lines = driver.Draw().Lines;
        var caption = lines.Where(l => l.Contains("Text#caption")).ToArray();

        await Assert.That(caption.Length).IsEqualTo(2);
        await Assert
            .That(caption[0])
            .Contains("drop-shadow #000000 direction 270 depth 1 blur 8 opacity 0.9");
        await Assert.That(caption[1]).Contains("glyphs");
        await Assert.That(lines.Where(l => l.Contains("Text#plain")).Count()).IsEqualTo(1);
    }
}
