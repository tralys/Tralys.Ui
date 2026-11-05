using System.Numerics;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class LayerExitTests
{
    [Test]
    public async Task A_closed_layer_fades_with_its_content_taking_no_press_dismissal_or_focus_and_its_call_revives_it()
    {
        var open = new Switches();
        var under = new Counter();
        var panel = new Counter();
        var dismissed = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    id: "under",
                    width: 300,
                    height: 300,
                    onClick: Callback.Of(under, static c => c.Value++)
                )
            )
            {
                if (open.A)
                    using (
                        ui.Layer(
                            dismissOn: Dismiss.OutsidePress | Dismiss.Escape,
                            onDismiss: Callback.Of(dismissed, static c => c.Value++),
                            restoreFocus: true
                        )
                    )
                        ui.Fader(onClick: Callback.Of(panel, static c => c.Value++), id: "panel");
            }
        });
        driver.Focus(driver.Get("under"));
        open.A = true;
        driver.Settle();
        driver.Focus(driver.Get("panel"));
        var layer = driver.View.Layers[0];

        open.A = false;
        driver.Frame();
        var fading = driver.View.Layers.Length;
        var focused = driver.Focused;
        driver.Click(new Vector2(50, 50), settle: false);
        driver.Press(Key.Escape, settle: false);
        open.A = true;
        driver.Frame();
        var revived = driver.View.Layers[0];
        open.A = false;
        driver.Settle();

        await Assert.That(fading).IsEqualTo(1);
        await Assert.That(focused).IsSameReferenceAs(driver.Get("under"));
        await Assert.That(under.Value).IsEqualTo(1);
        await Assert.That(panel.Value).IsEqualTo(0);
        await Assert.That(dismissed.Value).IsEqualTo(0);
        await Assert.That(revived).IsSameReferenceAs(layer);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }
}
