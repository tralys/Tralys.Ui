using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class CommandTests
{
    [Test]
    public async Task A_component_whose_command_cant_run_is_disabled_until_it_says_it_can()
    {
        var open = new Switches();
        var runs = new Counter();
        var command = new Command(() => runs.Value++, () => open.A);
        var disabled = new Seen<bool>();
        using var driver = UiDriver.Render(ui =>
            ui.CommandButton(onClick: command, disabled: disabled, id: "button")
        );

        driver.Click(driver.Get<CommandButton>("button"));
        driver.Tab();
        var refused = (disabled.Value, runs.Value, driver.Focused);
        open.A = true;
        driver.Settle();
        driver.Click(driver.Get<CommandButton>("button"));

        await Assert.That(refused).IsEqualTo((true, 0, (Node?)null));
        await Assert.That(disabled.Value).IsFalse();
        await Assert.That(runs.Value).IsEqualTo(1);
    }
}
