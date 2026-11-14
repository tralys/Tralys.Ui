using Tralys.Ui.Testing;
using Tralys.Ui.Tests;

namespace Tralys.Ui.Components.Tests;

public sealed class SelectAllocationTests
{
    static readonly string[] TownNames = Name(10_000);
    static readonly IItemsSource<string> Towns10k = ItemsSource.From(TownNames);
    static readonly Key[] Letters = [Key.A, Key.B, Key.C, Key.D, Key.E, Key.F];

    static string[] Name(int count)
    {
        string[] stems =
        [
            "Ashford",
            "Brightwater",
            "Coldharbour",
            "Dunmere",
            "Eastwick",
            "Fairhaven",
        ];
        var names = new string[count];
        for (var i = 0; i < count; i++)
            names[i] = $"{stems[i % stems.Length]} {i / stems.Length + 1}";
        return names;
    }

    static UiDriver Towns(Picked picked)
    {
        var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(align: Align.Start))
                    ui.Select(
                        items: Towns10k,
                        itemKey: Components.Tests.Towns.ByName,
                        itemText: Components.Tests.Towns.Named,
                        value: picked.Value,
                        onValueChange: picked.OnValueChange,
                        width: 200
                    );
            })
            .WithIcons();
        driver.Focus(driver.RootOf<Select<string>>());
        return driver;
    }

    [Test]
    public async Task A_ten_thousand_row_select_opened_by_f4_on_a_far_value_and_closed_by_escape_every_10_frames_allocates_nothing()
    {
        var picked = new Picked { Value = TownNames[0] };
        using var driver = Towns(picked);
        var frames = new Counter();

        var bytes = AllocationGate.WorstFrameBytes(
            () =>
            {
                var n = ++frames.Value;
                if (n % 10 == 0)
                {
                    if (driver.View.Layers.Length > 0)
                        driver.Press(Key.Escape, settle: false);
                    else
                    {
                        picked.Value = TownNames[n / 20 * 997 % TownNames.Length];
                        driver.Press(Key.F4, settle: false);
                    }
                }
                driver.Frame();
            },
            warmup: 1200
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_ten_thousand_row_selects_focus_moved_by_arrows_picked_by_enter_and_reopened_by_f4_one_key_a_frame_allocates_nothing()
    {
        var picked = new Picked { Value = TownNames[5000] };
        using var driver = Towns(picked);
        var frames = new Counter();

        var bytes = AllocationGate.WorstFrameBytes(
            () =>
            {
                var n = frames.Value++;
                var step = n % 10;
                var key =
                    step == 0 ? Key.F4
                    : step == 9 ? Key.Enter
                    : n / 100 % 2 == 0 ? Key.Down
                    : Key.Up;
                driver.Press(key, settle: false);
                driver.Frame();
            },
            warmup: 1200
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task An_open_ten_thousand_row_selects_focus_paged_and_tabbed_every_frame_allocates_nothing()
    {
        var picked = new Picked();
        using var driver = Towns(picked);
        driver.Press(Key.F4);

        var frames = new Counter();

        var bytes = AllocationGate.WorstFrameBytes(
            () =>
            {
                var step = frames.Value++ % 45;
                if (step < 20)
                    driver.Press(Key.PageDown, settle: false);
                else if (step < 35)
                    driver.Press(Key.PageUp, settle: false);
                else
                    driver.Press(
                        Key.Tab,
                        step < 40 ? ModifierKeys.None : ModifierKeys.Shift,
                        settle: false
                    );
                driver.Frame();
            },
            warmup: 900
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Typing_an_items_first_letter_every_frame_closed_and_open_in_turn_allocates_nothing()
    {
        var picked = new Picked { Value = TownNames[0] };
        using var driver = Towns(picked);
        var frames = new Counter();

        var bytes = AllocationGate.WorstFrameBytes(
            () =>
            {
                var n = frames.Value++;
                var block = n / 20;
                if (n % 20 == 0)
                    driver.Press(block % 2 == 0 ? Key.Escape : Key.F4, settle: false);
                else
                    driver.Press(Letters[block / 2 % Letters.Length], settle: false);
                driver.Frame();
            },
            warmup: 1200
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Selects_showing_records_by_name_typed_into_every_frame_and_by_a_ping_that_changes_every_frame_allocate_nothing()
    {
        Server[] servers =
        [
            new("Europe West", 24),
            new("Europe East", 41),
            new("North America", 96),
            new("South America", 132),
            new("Asia", 180),
        ];
        var items = ItemsSource.From(servers);
        var picked = new Picked { Value = "Asia" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Select(
                    items: items,
                    itemKey: Server.ByName,
                    itemText: Server.Named,
                    value: picked.Value,
                    onValueChange: picked.OnValueChange,
                    width: 200
                );
                ui.Select(
                    items: items,
                    itemKey: Server.ByName,
                    itemText: Server.PingOf,
                    value: "Asia",
                    width: 200
                );
            }
        });
        driver.Focus(Menus.RootOf(driver.All<Select<Server>>()[0]));

        var bytes = driver.WorstFrameBytes(
            (servers, new Counter()),
            static (d, s) =>
            {
                var tick = ++s.Item2.Value;
                foreach (var server in s.servers)
                    server.Ping = (server.Ping + tick) % 1000;
                d.Press(Key.E, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        var ping = Texts(Menus.RootOf(driver.All<Select<Server>>()[1]));
        await Assert.That(ping).Contains(Server.PingOf(servers[4]).ToString());
        await Assert.That(picked.Value).StartsWith("Europe");
    }

    static List<string> Texts(Node within, List<string>? found = null)
    {
        found ??= [];
        foreach (var child in within.Children)
        {
            if (child.Kind == NodeKind.Text)
                found.Add(child.Text.ToString());
            Texts(child, found);
        }
        return found;
    }
}
