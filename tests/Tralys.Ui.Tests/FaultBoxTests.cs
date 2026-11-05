#if DEBUG
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class FaultBoxTests
{
    [Test]
    public async Task A_faulted_component_draws_an_error_box_over_its_space_instead_of_its_content()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui => ui.Thrower(model: model));
        driver.View.Context.OnFault = static _ => { };
        var drawn = driver.Draw().Lines;

        model.Count = 1;
        driver.Settle();
        var faulted = driver.Draw().Lines;

        await Assert.That(drawn.Any(l => l.Contains("Ticker", StringComparison.Ordinal))).IsTrue();
        await Assert
            .That(faulted.Any(l => l.Contains("Ticker", StringComparison.Ordinal)))
            .IsFalse();
        await Assert
            .That(faulted.Count(l => l.Contains("Thrower[0]: line", StringComparison.Ordinal)))
            .IsEqualTo(2);
    }
}
#endif
