using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class GeneratedCallAllocationTests
{
    [Test]
    public async Task Props_retaining_a_formatted_text_and_flipping_disabled_every_frame_allocate_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                ui.Tally(label: ui.Format($"n {model.Count}"), disabled: (model.Count & 1) == 1);
                ui.Pick(value: "a", disabled: (model.Count & 1) == 0);
            }
        });

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }
}
