using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ThreadingTests
{
    [Test]
    public async Task A_model_change_from_another_thread_rerenders_on_the_ui_thread()
    {
        var model = new Model { Name = "before" };
        await UiDriver.Run(
            ui => ui.Label(model: model),
            async driver =>
            {
                await Task.Run(() => model.Name = "after");
                await Assert.That(model.Name).IsEqualTo("before");

                driver.Frame();

                await Assert.That(driver.Stats.RendersOf<Label>()).IsEqualTo(1);
                await Assert.That(driver.GetText("after")).IsNotNull();
            }
        );
    }

    [Test]
    public async Task Changing_an_observed_list_from_another_thread_throws()
    {
        var items = new ObservableList<int> { 1 };
        await UiDriver.Run(
            ui =>
            {
                using (ui.Box())
                    ui.Text(ui.Format($"{items.Count}"));
            },
            async driver =>
            {
                var thrown = await Task.Run(() =>
                {
                    try
                    {
                        items.Add(2);
                        return null;
                    }
                    catch (InvalidOperationException e)
                    {
                        return e;
                    }
                });
                await Assert.That(thrown).IsNotNull();
            }
        );
    }
}
