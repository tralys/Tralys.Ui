using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class DispatcherTests
{
    [Test]
    public async Task Posts_run_at_the_next_update_in_post_order_across_state_types_and_one_posted_by_a_post_waits_for_the_update_after()
    {
        var log = new List<string>();
        using var driver = UiDriver.Render(static _ => { });
        var dispatcher = driver.View.Context.Dispatcher;

        dispatcher.Post((Log: log, Name: "a1"), static s => s.Log.Add(s.Name));
        dispatcher.Post(log, static l => l.Add("b"));
        dispatcher.Post((Log: log, Name: "a2"), static s => s.Log.Add(s.Name));
        dispatcher.Post(
            (Log: log, Dispatcher: dispatcher),
            static s => s.Dispatcher.Post(s.Log, static l => l.Add("posted by a post"))
        );
        driver.Frame();
        var first = string.Join(", ", log);
        driver.Frame();

        await Assert.That(first).IsEqualTo("a1, b, a2");
        await Assert.That(string.Join(", ", log)).IsEqualTo("a1, b, a2, posted by a post");
    }

    [Test]
    public async Task A_post_that_throws_reaches_the_fault_handler_once_and_the_posts_after_it_run_in_that_update()
    {
        var log = new List<string>();
        var faults = new List<Exception>();
        using var driver = UiDriver.Render(static _ => { });
        driver.View.Context.OnFault = faults.Add;
        var dispatcher = driver.View.Context.Dispatcher;

        dispatcher.Post(log, static _ => throw new InvalidOperationException("boom"));
        dispatcher.Post(log, static l => l.Add("after"));
        driver.Frame();

        await Assert.That(faults.Count).IsEqualTo(1);
        await Assert.That(log).IsEquivalentTo(["after"]);
    }

    [Test]
    public async Task With_no_fault_handler_a_post_that_throws_surfaces_from_the_update_and_what_it_cut_short_runs_at_the_next()
    {
        var log = new List<string>();
        var model = new Model { Name = "before" };
        using var driver = UiDriver.Render(ui => ui.Label(model: model));
        var dispatcher = driver.View.Context.Dispatcher;

        dispatcher.Post(log, static _ => throw new InvalidOperationException("boom"));
        dispatcher.Post(log, static l => l.Add("after"));
        model.Name = "after";
        var threw = false;
        try
        {
            driver.Frame();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        var cutShort = (log.Count, driver.GetText("before").IsMounted);
        driver.Frame();

        await Assert.That(threw).IsTrue();
        await Assert.That(cutShort).IsEqualTo((0, true));
        await Assert.That(log).IsEquivalentTo(["after"]);
        await Assert.That(driver.GetText("after").IsMounted).IsTrue();
    }
}
