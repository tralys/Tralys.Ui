using System.Numerics;
using Tralys.Ui.Testing;
using Tralys.Ui.Tests;

namespace Tralys.Ui.Components.Tests;

public sealed partial class PickersAllocationTests
{
    static readonly string[] TownNames = Name(10_000);
    static readonly IItemsSource<string> Towns10k = ItemsSource.From(TownNames);

    static string[] Name(int count)
    {
        var names = new string[count];
        for (var i = 0; i < count; i++)
            names[i] = $"Town {i + 1}";
        return names;
    }

    static UiDriver Towns(Picked picked) =>
        UiDriver
            .Render(ui =>
            {
                using (ui.Box(align: Align.Start))
                    ui.Combobox(
                        items: Towns10k,
                        itemKey: Components.Tests.Towns.ByName,
                        itemText: Components.Tests.Towns.Named,
                        value: picked.Value,
                        onValueChange: picked.OnValueChange,
                        onOpenChange: picked.OnOpenChange,
                        width: 200
                    );
            })
            .WithIcons();

    static void FocusText(UiDriver driver) =>
        driver.Focus(Nodes.Find(driver.Get<Combobox<string>>(), NodeKind.EditableText));

    [Test]
    public async Task A_ten_thousand_row_combobox_opened_on_a_far_value_and_closed_by_escape_every_10_frames_allocates_nothing()
    {
        var picked = new Picked { Value = TownNames[0] };
        using var driver = Towns(picked);
        FocusText(driver);
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
                        driver.Press(Key.Down, settle: false);
                    }
                }
                driver.Frame();
            },
            warmup: 1200
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_ten_thousand_row_comboboxs_light_moved_by_arrows_and_picked_by_enter_one_key_a_frame_allocates_nothing()
    {
        var picked = new Picked { Value = TownNames[5000] };
        using var driver = Towns(picked);
        FocusText(driver);
        var frames = new Counter();

        var bytes = AllocationGate.WorstFrameBytes(
            () =>
            {
                var n = frames.Value++;
                var step = n % 10;
                var key =
                    step == 0 ? Key.Down
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
    public async Task A_ten_thousand_row_comboboxs_light_paged_down_and_up_every_frame_allocates_nothing()
    {
        var picked = new Picked();
        using var driver = Towns(picked);
        FocusText(driver);
        driver.Press(Key.Down);

        var bytes = driver.WorstFrameBytes(
            new Counter(),
            static (d, c) =>
            {
                d.Press(c.Value++ % 35 < 20 ? Key.PageDown : Key.PageUp, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Typing_into_a_ten_thousand_row_combobox_backspacing_clearing_and_closing_it_one_step_a_frame_allocates_nothing()
    {
        var picked = new Picked { Value = TownNames[0] };
        using var driver = Towns(picked);
        FocusText(driver);
        var frames = new Counter();

        var bytes = AllocationGate.WorstFrameBytes(
            () =>
            {
                switch (frames.Value++ % 7)
                {
                    case 0 or 4:
                        driver.Press(Key.A, ModifierKeys.Control, settle: false);
                        break;
                    case 1:
                        driver.Type("Town 5", settle: false);
                        break;
                    case 2:
                        driver.Type("7", settle: false);
                        break;
                    case 3 or 5:
                        driver.Press(Key.BackSpace, settle: false);
                        break;
                    default:
                        driver.Press(Key.Escape, settle: false);
                        break;
                }
                driver.Frame();
            },
            warmup: 1200
        );
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("Town 9999");
        var shown = driver.All<Combobox.Item>().Count;

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(shown).IsEqualTo(1);
    }

    [Test]
    public async Task Comboboxes_showing_records_by_name_and_by_a_ping_that_changes_every_frame_the_name_one_typed_into_allocate_nothing()
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
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Combobox(
                    items: items,
                    itemKey: Server.ByName,
                    itemText: Server.Named,
                    value: "Asia",
                    width: 200
                );
                ui.Combobox(
                    items: items,
                    itemKey: Server.ByName,
                    itemText: Server.PingOf,
                    value: "Asia",
                    width: 200
                );
            }
        });

        driver.Focus(Nodes.Find(driver.All<Combobox<Server>>()[0], NodeKind.EditableText));

        var bytes = driver.WorstFrameBytes(
            (servers, new Counter()),
            static (d, s) =>
            {
                var tick = ++s.Item2.Value;
                foreach (var server in s.servers)
                    server.Ping = (server.Ping + tick) % 1000;
                if (tick % 2 == 0)
                    d.Type("e", settle: false);
                else
                    d.Press(Key.BackSpace, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        var ping = Nodes.Find(driver.All<Combobox<Server>>()[1], NodeKind.EditableText);
        await Assert.That(ping.Text.ToString()).IsEqualTo(Server.PingOf(servers[4]).ToString());
    }

    [Test]
    public async Task A_picker_field_opened_and_shut_by_space_every_6_frames_allocates_nothing()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.PickerField(text: "Short", content: Body, width: 120);
        });
        driver.Focus(driver.RootOf<PickerField>());
        var frames = new Counter();

        var bytes = AllocationGate.WorstFrameBytes(
            () =>
            {
                if (++frames.Value % 6 == 0)
                    driver.Press(Key.Space, settle: false);
                driver.Frame();
            },
            warmup: 1200
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    static readonly Slot Body = Slot.Of(static ui =>
    {
        using (ui.Box(width: 60, height: 24)) { }
    });

    [Test]
    public async Task An_open_collection_pickers_items_arrowed_through_every_frame_allocate_nothing()
    {
        var picked = new Picked();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.CollectionPicker(
                    items: Towns10k,
                    itemKey: Components.Tests.Towns.ByName,
                    itemText: Components.Tests.Towns.Named,
                    value: picked.Value,
                    onValueChange: picked.OnValueChange,
                    defaultLayout: ListLayout.Rows,
                    width: 240
                );
        });
        driver.Click(driver.RootOf<CollectionPicker<string>>());
        driver.Press(Key.Down);

        var bytes = driver.WorstFrameBytes(
            new Counter(),
            static (d, c) =>
            {
                d.Press(c.Value++ % 16 < 8 ? Key.Down : Key.Up, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(picked.Values.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task An_open_collection_pickers_search_typed_into_and_left_for_its_items_every_frame_allocates_nothing()
    {
        var picked = new Picked();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.CollectionPicker(
                    items: Towns10k,
                    itemKey: Components.Tests.Towns.ByName,
                    itemText: Components.Tests.Towns.Named,
                    value: picked.Value,
                    onValueChange: picked.OnValueChange,
                    defaultLayout: ListLayout.Rows,
                    width: 240
                );
        });
        driver.Click(driver.RootOf<CollectionPicker<string>>());
        var search = Nodes.Find(driver.All<SearchInput>()[0], NodeKind.EditableText);

        var bytes = driver.WorstFrameBytes(
            (search, new Counter()),
            static (d, s) =>
            {
                var step = s.Item2.Value++;
                switch (step % 4)
                {
                    case 0:
                        d.Focus(s.search, settle: false);
                        d.Type("5", settle: false);
                        break;
                    case 1:
                        d.Press(Key.BackSpace, settle: false);
                        break;
                    case 2:
                        d.Press(Key.Down, settle: false);
                        break;
                    default:
                        d.Press(step / 4 % 2 == 0 ? Key.Down : Key.Up, settle: false);
                        break;
                }
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(picked.Values.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task A_color_pickers_hex_box_retyped_with_a_whole_code_every_frame_allocates_nothing()
    {
        var colors = new Colors();
        using var driver = UiDriver.Render(ui =>
            ui.ColorPicker(
                value: colors.Value,
                onValueChange: Callback<Color>.Of(colors, static (c, color) => c.Value = color),
                width: 300
            )
        );
        driver.Focus(Nodes.Find(driver.Get<ColorPicker>(), NodeKind.EditableText));

        var bytes = driver.WorstFrameBytes(
            new Counter(),
            static (d, c) =>
            {
                d.Press(Key.A, ModifierKeys.Control, settle: false);
                d.Type(Codes[c.Value++ % Codes.Length], settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(colors.Value.A).IsEqualTo((byte)255);
    }

    static readonly string[] Codes = ["#3399ff", "#ff5500", "#00c060", "#123456", "#fedcba"];

    [Test]
    public async Task A_color_pickers_square_knob_dragged_round_a_circle_every_frame_allocates_nothing()
    {
        var colors = new Colors();
        using var driver = UiDriver.Render(ui =>
            ui.ColorPicker(value: colors.Value, onValueChange: colors.OnValueChange, width: 300)
        );
        var square = Kids.Of(Kids.Of(Menus.RootOf(driver.Get<ColorPicker>()))[^1])[0];
        var centre = square.ViewBounds.Center();
        var radius = 0.4f * MathF.Min(square.ViewBounds.Width, square.ViewBounds.Height);
        var held = driver.Hold(square);

        var bytes = driver.WorstFrameBytes(
            (held, new Counter(), centre, radius),
            static (d, s) =>
            {
                var angle = s.Item2.Value++ * 0.37f;
                var to = s.centre + s.radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                s.held.MoveBy(to - s.held.Position, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(colors.Reports.Count).IsGreaterThan(10);
    }

    [Test]
    public async Task A_color_pickers_hue_bar_dragged_back_and_forth_every_frame_allocates_nothing()
    {
        var colors = new Colors { Value = Color.FromRgb(0xC0, 0x50, 0x28) };
        using var driver = UiDriver.Render(ui =>
            ui.ColorPicker(value: colors.Value, onValueChange: colors.OnValueChange, width: 300)
        );
        var hue = Kids.Of(Kids.Of(Menus.RootOf(driver.Get<ColorPicker>()))[^1])[1];
        var held = driver.Hold(hue);

        var bytes = driver.WorstFrameBytes(
            (held, new Counter()),
            static (d, s) =>
            {
                s.held.MoveBy(
                    new Vector2(s.Item2.Value++ % 20 < 10 ? 13f : -13f, 0f),
                    settle: false
                );
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_color_picker_select_opened_and_shut_every_10_frames_allocates_nothing()
    {
        var colors = new Colors { Value = Color.FromRgb(0xFF, 0x55, 0x00) };
        var open = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.ColorPickerSelect(
                    value: colors.Value,
                    onValueChange: colors.OnValueChange,
                    open: open.A,
                    showAlpha: true,
                    showPrevious: true
                );
        });
        var frames = new Counter();

        var bytes = AllocationGate.WorstFrameBytes(
            () =>
            {
                if (++frames.Value % 10 == 0)
                    open.A = !open.A;
                driver.Frame();
            },
            warmup: 1200
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_filter_buttons_dot_toggled_every_frame_and_its_icon_clicked_every_8_frames_allocates_nothing()
    {
        var active = new Switches();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(padding: 120, align: Align.Start))
                    ui.FilterButton(content: Body, active: active.A);
            })
            .WithIcons();
        var button = driver.Get<IconButton>();
        var frames = new Counter();

        var bytes = AllocationGate.WorstFrameBytes(
            () =>
            {
                var n = ++frames.Value;
                active.A = n % 3 == 0;
                if (n % 8 == 0)
                    driver.Click(button, settle: false);
                driver.Frame();
            },
            warmup: 1200
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_pagination_stepped_forward_and_back_every_few_frames_allocates_nothing()
    {
        var page = new Paging();
        using var driver = UiDriver
            .Render(ui =>
                ui.Pagination(
                    value: page.Value,
                    onValueChange: page.Set,
                    total: 95,
                    pageSize: 10,
                    width: 320
                )
            )
            .WithIcons();
        var buttons = driver.All<IconButton>();

        var bytes = driver.WorstFrameBytes(
            (buttons, new Counter()),
            static (d, s) =>
            {
                var n = ++s.Item2.Value;
                if (n % 4 == 0)
                    d.Click(s.buttons[n / 40 % 2 == 0 ? 2 : 1], settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    sealed partial class Paging : Tralys.Ui.Mvvm.ObservableObject
    {
        [Tralys.Ui.Mvvm.ObservableProperty]
        public partial int Value { get; set; }

        public Callback<int> Set => Callback<int>.Of(this, static (p, page) => p.Value = page);
    }
}
