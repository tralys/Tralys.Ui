using Tralys.Ui.Gallery;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Demo.Tests;

public sealed class GalleryAllocationTests
{
    [Test]
    public async Task Stepping_back_and_forth_between_two_stories_allocates_nothing_once_warm()
    {
        var model = new GalleryModel("Button.Active", false, null) { OpenLink = static _ => { } };
        using var driver = UiDriver.Render(
            ui => GalleryShell.Root(ui, model),
            size: new Size(1280, 720)
        );

        var bytes = driver.WorstFrameBytes(static d =>
        {
            d.Press(Key.Down);
            d.Press(Key.Up);
        });

        await Assert.That(bytes).IsEqualTo(0L);
    }
}
