using Tralys.Ui.Components;
using Tralys.Ui.Docking;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.AotSmoke;

sealed partial class SmokeModel : ObservableObject
{
    [ObservableProperty]
    public partial bool On { get; set; }

    [ObservableProperty]
    public partial int Tick { get; set; }

    public readonly DockManager Dock = SmokeDock.Made();

    public readonly ObservableText Name = new("Ada");
    public readonly ObservableText Find = new("Ada");
    public readonly ObservableText Password = new("secret");
    public readonly ObservableText Suggested = new("Ad");

    public float LowestPulse = 1f;
    public float HighestPulse;
    public int Draws;
    public int Clicks;
}

sealed class SmokeBranch(string name, SmokeBranch[] children) : TreeRow
{
    public string Name { get; } = name;

    public SmokeBranch[] Children { get; } = children;
}

sealed class SmokeForest : ITreeChildren<SmokeBranch>
{
    readonly SmokeBranch[] _roots =
    [
        new("Staff", [new("Alice", []), new("Dave", [])]) { IsExpanded = true },
        new("Contractors", [new("Erin", [])]),
    ];

    public SmokeForest()
    {
        Rows = new TreeRows<SmokeBranch>(this);
        Rows.Refresh();
    }

    public TreeRows<SmokeBranch> Rows { get; }

    public int ChildCount(SmokeBranch? parent) => (parent?.Children ?? _roots).Length;

    public SmokeBranch ChildAt(SmokeBranch? parent, int index) =>
        (parent?.Children ?? _roots)[index];
}

readonly record struct SmokeProps
{
    public SmokeModel? Model { get; init; }
}

readonly record struct Meter(SmokeModel Model, int Tick, Color Fill);

sealed class SmokeRoot : Component<SmokeProps>
{
    static readonly GridTracks Columns = new(Track.Px(200), Track.Fr(1));

    static readonly IItemsSource<string> Fruits = ItemsSource.From(
        new[] { "Apple", "Banana", "Cherry", "Grape" }
    );

    static readonly SelectionSet Picked = new();

    static readonly KeyOf<string> ByName = static name => name;

    static readonly TextOf<string> Named = static name => name;

    static readonly SmokeForest Forest = new();

    static readonly KeyOf<SmokeBranch> BranchKey = static branch => branch.Name;

    static readonly TextOf<SmokeBranch> BranchName = static branch => branch.Name;

    static readonly Slot FileRows = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "New map", shortcut: "Ctrl+N");
        ui.MenuSeparator();
        ui.MenuItem(text: "Quit", destructive: true);
    });
    static readonly TableColumn<string>[] FruitColumns =
    [
        new()
        {
            Header = "Fruit",
            SortKey = "Fruit",
            Width = 120,
            Text = Named,
        },
    ];
    static readonly ISuggestionSource Names = new LocalSuggestions(static names =>
        names.AddRange(["Ada", "Adele"])
    );

    static readonly CanvasDraw<Meter> DrawMeter = static (dc, meter, info) =>
    {
        meter.Model.Draws++;
        var width = info.Size.Width * (meter.Tick % 60) / 60f;
        dc.DrawRoundedRectangle(meter.Fill, new Rect(0f, 0f, width, info.Size.Height), 4f);
    };

    protected override void Render(Ui ui, in SmokeProps p)
    {
        var model = p.Model!;
        var t = ui.Theme;
        var pulse = ui.Loop(1f, alternate: true, easing: Easings.CubicInOut);
        model.LowestPulse = MathF.Min(model.LowestPulse, pulse);
        model.HighestPulse = MathF.Max(model.HighestPulse, pulse);
        using (ui.Grid(columns: Columns, gap: 12, padding: 16, background: t.Color(ColorToken.Bg)))
        {
            using (ui.Box(direction: Direction.Column, gap: 8))
            {
                ui.Text(ui.Format($"Tick {model.Tick}"));
                ui.Switch(value: model.On, onValueChange: model.SetOn, label: "Auto", id: "switch");
                ui.Checkbox(
                    value: model.On ? CheckState.Checked : CheckState.Indeterminate,
                    label: "Mixed"
                );
                using (ui.RadioGroup(value: model.On ? 1 : 2, direction: Direction.Row))
                {
                    ui.Radio(value: 1, label: "One");
                    ui.Radio(value: 2, label: "Two");
                }
                using (ui.Collapsible(header: "More", open: model.On))
                    ui.Text("Inside");
                using (ui.Tooltip(text: "Saves the model", open: true))
                    ui.Button(
                        text: "Save",
                        variant: model.On ? ComponentVariant.Filled : ComponentVariant.Outlined,
                        onClick: Callback.Of(model, static m => m.Clicks++),
                        id: "button"
                    );
                using (ui.Tabs(value: model.On ? "Auto" : "Manual", width: 180))
                {
                    using (ui.TabsList())
                    {
                        ui.TabsTab(value: "Manual", text: "Manual");
                        ui.TabsTab(value: "Auto", text: "Auto");
                    }
                    using (ui.TabsPanel(value: "Auto"))
                        ui.Text("Running");
                }
                ui.Input(text: model.Name, width: 180);
                using (ui.Field(label: "Find", description: "Every name"))
                    ui.SearchInput(text: model.Find, width: 120);
                ui.PasswordInput(text: model.Password, width: 180);
                ui.NumberInput(value: model.On ? 2 : 1, steppers: true, width: 180);
                ui.MoneyInput(value: 1234, width: 180);
                ui.SuggestionInput(text: model.Suggested, source: Names, width: 180);
                ui.Slider(value: model.On ? 7 : 3, ticks: TickPlacement.BottomRight, width: 180);
                using (ui.Box(direction: Direction.Column, width: 240))
                    ui.SliderField(label: "Volume", value: 40, unit: "%");
                ui.NumberPad(hasStep: true, hasMax: true);
                ui.Editable(value: "Rename me", width: 180);
                using (ui.Menubar())
                {
                    ui.MenuItem(text: "File", items: FileRows);
                    ui.MenuItem(text: "Help");
                }
                ui.Tree(
                    items: Forest.Rows,
                    itemKey: BranchKey,
                    itemText: BranchName,
                    value: model.On ? "Alice" : "Dave",
                    width: 180,
                    height: 96
                );
                ui.Combobox(
                    items: Fruits,
                    itemKey: ByName,
                    itemText: Named,
                    value: "Banana",
                    defaultOpen: true,
                    width: 180
                );
                ui.Select(
                    items: Fruits,
                    itemKey: ByName,
                    itemText: Named,
                    value: model.On ? "Cherry" : "Apple",
                    searchable: true,
                    width: 180
                );
                ui.ListBox(
                    items: Fruits,
                    itemKey: ByName,
                    itemText: Named,
                    value: model.On ? "Cherry" : "Apple",
                    width: 180,
                    height: 64
                );
                ui.MultiSelect(
                    items: Fruits,
                    itemKey: ByName,
                    itemText: Named,
                    value: Picked,
                    width: 180
                );
                ui.CheckList(
                    items: Fruits,
                    itemKey: ByName,
                    itemText: Named,
                    value: Picked,
                    maxHeight: 48
                );
                ui.DataTable(
                    items: Fruits,
                    itemKey: ByName,
                    columns: FruitColumns,
                    sort: new TableSort("Fruit", SortDirection.Descending),
                    pageSize: 2,
                    width: 180,
                    height: 120
                );
                ui.CollectionPicker(
                    items: Fruits,
                    itemKey: ByName,
                    itemText: Named,
                    value: model.On ? "Cherry" : "Apple",
                    width: 180
                );
                ui.ColorPicker(
                    value: model.On ? Color.FromRgb(0x55, 0xFF, 0x00) : default,
                    showAlpha: true,
                    showPrevious: true,
                    width: 220
                );
                using (ui.Box(align: Align.Center, gap: 8))
                {
                    ui.ColorPickerSelect(value: Color.FromRgb(0xFF, 0x55, 0x00));
                    ui.FilterButton(active: model.On);
                    ui.PickerField(text: "Short", width: 120);
                }
                ui.Pagination(value: model.Tick % 5 + 1, total: 45, pageSize: 10, width: 220);
                using (
                    ui.Box(
                        width: 32,
                        height: 32,
                        radius: 6,
                        background: ui.Animate(
                            t.Color(model.On ? ColorToken.BgAccent : ColorToken.BgMuted)
                        ),
                        opacity: 0.4f + 0.6f * pulse,
                        transform: Transform.Rotate(90f * pulse)
                    )
                ) { }
                using (ui.Box(align: Align.Center, gap: 8))
                {
                    ui.Badge(count: model.Tick % 120, prefix: "@");
                    if (model.On)
                        ui.Badge(count: 3);
                    else
                        ui.Spinner();
                    ui.Kbd(chord: Chord.Of(Key.PageUp, ModifierKeys.Control));
                    using (
                        ui.Cooldown(
                            duration: 2f,
                            remaining: 2f - model.Tick % 120 / 60f,
                            width: 32,
                            height: 32
                        )
                    ) { }
                    ui.ResizeHandle(value: 100f, width: 4, height: 32);
                    ui.TouchStick(width: 48, height: 48);
                }
                using (ui.Box(align: Align.Center, gap: 8))
                {
                    ui.Spinner();
                    ui.Progress(value: model.Tick % 101, indeterminate: model.On, width: 60);
                    using (ui.CircularProgress(value: model.Tick % 101, width: 24, height: 24)) { }
                    using (ui.Marquee(width: 60))
                        ui.Text("A name too long to fit");
                }
                if (model.Tick > 0)
                    using (ui.Toaster(duration: 2f))
                        ui.Toast(title: "Saved", severity: ToastSeverity.Warning);
                using (ui.ScrollArea(width: 180, height: 40))
                    for (var i = 0; i < 4; i++)
                        ui.Text("Row", key: i);
                ui.Dock(manager: model.Dock, content: SmokeDock.Content, width: 180, height: 80);
                using (ui.PanZoom(width: 180, height: 40))
                using (ui.Thumb(width: 24, height: 24, background: t.Color(ColorToken.BgAccent)))
                { }
            }
            ui.Canvas(
                state: new Meter(model, model.Tick, t.Color(ColorToken.BgProgress)),
                draw: DrawMeter,
                height: 24
            );
        }
    }
}

static class SmokeDock
{
    public static readonly Slot<DockElement> Content = Slot<DockElement>.Of(
        static (ui, element, _) => ui.Text(element.Title)
    );

    public static DockManager Made()
    {
        var manager = new DockManager();
        var tool = new DockElement("tool", "Tool");
        var page = new DockElement("page", "Page", DockType.Document);
        manager.Register(tool);
        manager.Register(page);
        manager.Dock(tool, DockPosition.Left);
        manager.Dock(page, DockPosition.Center);
        return manager;
    }
}
