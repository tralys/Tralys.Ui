using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Combobox<string>>("Pickers")]
static class ComboboxStories
{
    static readonly ComponentSize[] Rungs =
    [
        ComponentSize.Xs,
        ComponentSize.Sm,
        ComponentSize.Md,
        ComponentSize.Lg,
    ];

    [FramePath]
    public static void Default(Ui ui)
    {
        var value = ui.State<UiKey>("Option A");
        ui.Combobox(
            items: Choices.Options,
            itemKey: Choices.ByName,
            itemText: Choices.Named,
            value: value.Value,
            onValueChange: value.Setter,
            width: 200
        );
    }

    [FramePath]
    public static void ManyItems(Ui ui)
    {
        var value = ui.State<UiKey>("Alpha");
        ui.Combobox(
            items: Choices.Letters,
            itemKey: Choices.ByName,
            itemText: Choices.Named,
            value: value.Value,
            onValueChange: value.Setter,
            width: 200
        );
    }

    [FramePath]
    public static void Disabled(Ui ui) =>
        ui.Combobox(
            items: Choices.Options,
            itemKey: Choices.ByName,
            itemText: Choices.Named,
            value: "Option A",
            disabled: true,
            width: 200
        );

    [FramePath]
    public static void Placeholder(Ui ui)
    {
        var role = ui.State<UiKey>(default);
        var labelled = ui.State<UiKey>(default);
        using (ui.Box(direction: Direction.Column, gap: 8, width: 200))
        {
            ui.Combobox(
                items: Choices.Roles,
                itemKey: Choices.ByName,
                itemText: Choices.Named,
                value: role.Value,
                onValueChange: role.Setter,
                placeholder: "Pick a role"
            );
            ui.Combobox(
                items: Choices.Roles,
                itemKey: Choices.ByName,
                itemText: Choices.Named,
                value: labelled.Value,
                onValueChange: labelled.Setter,
                placeholder: "Pick a role",
                sublabel: "Role"
            );
        }
    }

    [FramePath]
    public static void Variants(Ui ui)
    {
        var role = ui.State<UiKey>("Engineer");
        var flush = ui.State<UiKey>("Engineer");
        using (ui.Box(direction: Direction.Column, gap: 8, width: 200))
        {
            ui.Combobox(
                items: Choices.Roles,
                itemKey: Choices.ByName,
                itemText: Choices.Named,
                value: role.Value,
                onValueChange: role.Setter,
                sublabel: "Role"
            );
            ui.Combobox(
                items: Choices.Roles,
                itemKey: Choices.ByName,
                itemText: Choices.Named,
                value: flush.Value,
                onValueChange: flush.Setter,
                sublabel: "Role",
                variant: FieldVariant.Flush
            );
        }
    }

    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 8, width: 200))
        {
            foreach (var size in Rungs)
                ui.Combobox(
                    items: Choices.Roles,
                    itemKey: Choices.ByName,
                    itemText: Choices.Named,
                    value: "Engineer",
                    size: size,
                    key: (int)size
                );
        }
    }

    [FramePath]
    public static void Virtualized(Ui ui)
    {
        var town = ui.State<UiKey>(Choices.FirstTown);
        using (ui.Box(direction: Direction.Column, gap: 8, width: 240))
        {
            HoldingListBoxStories.Caption(
                ui,
                "Ten thousand towns: the dropdown realizes only the rows in view; the arrows, Page Up and Page Down, and typing part of a town's name move through them"
            );
            ui.Combobox(
                items: Choices.ManyTowns,
                itemKey: Choices.ByName,
                itemText: Choices.Named,
                value: town.Value,
                onValueChange: town.Setter
            );
        }
    }
}

[Stories<Combobox<Server>>("Pickers")]
static class ServerComboboxStories
{
    [FramePath]
    public static void DisplayMember(Ui ui)
    {
        var byName = ui.State<UiKey>("Europe West");
        var byPing = ui.State<UiKey>("Europe West");
        using (ui.Box(direction: Direction.Column, gap: 8, width: 200))
        {
            ui.Combobox(
                items: Server.All,
                itemKey: Server.ByName,
                itemText: Server.Named,
                value: byName.Value,
                onValueChange: byName.Setter,
                sublabel: "Server"
            );
            ui.Combobox(
                items: Server.All,
                itemKey: Server.ByName,
                itemText: Server.PingOf,
                value: byPing.Value,
                onValueChange: byPing.Setter,
                sublabel: "Ping"
            );
        }
    }
}

[Stories<Combobox<SavedAccount>>("Pickers")]
static class AccountComboboxStories
{
    static readonly Slot<SavedAccount> Row = Slot<SavedAccount>.Of(
        static (ui, account, _) =>
            ui.ComboboxItem(
                value: account.Name,
                content: Slot.Of(
                    account,
                    static (ui, a) =>
                    {
                        ui.Text(a.Name, grow: 1f);
                        if (!a.IsAnother)
                            ui.IconButton(
                                icon: IconGlyph.Of(IconRole.X),
                                scheme: ColorScheme.Secondary,
                                variant: ComponentVariant.Ghost,
                                size: ComponentSize.Sm,
                                onClick: a.Forget
                            );
                    }
                )
            )
    );

    [FramePath]
    public static void Templated(Ui ui)
    {
        var accounts = ui.Remember(static () => new SavedAccounts());
        var chosen = ui.State<UiKey>(default);
        using (ui.Box(direction: Direction.Column, gap: 8, width: 320))
        {
            HoldingListBoxStories.Caption(
                ui,
                "Each row's forget button runs the action the form hands its rows, across the dropdown"
            );
            ui.Combobox(
                items: accounts.Items,
                itemKey: SavedAccount.ByName,
                itemText: SavedAccount.Named,
                item: Row,
                value: chosen.Value,
                onValueChange: chosen.Setter,
                placeholder: "Choose an account",
                size: ComponentSize.Lg
            );
        }
    }
}

[Stories<Combobox.Item>("Pickers")]
static class ComboboxItemStories
{
    [FramePath]
    public static void States(Ui ui)
    {
        var role = ui.State<UiKey>("Designer");
        using (ui.Box(direction: Direction.Column, gap: 8, width: 240, height: 220))
        {
            HoldingListBoxStories.Caption(
                ui,
                "The open dropdown's rows: the selected one in the accent, lit while the keyboard or the pointer is on it"
            );
            ui.Combobox(
                items: Choices.OpenRoles,
                itemKey: Choices.ByName,
                itemText: Choices.Named,
                value: role.Value,
                onValueChange: role.Setter,
                defaultOpen: true
            );
        }
    }
}

static class Choices
{
    public static readonly KeyOf<string> ByName = static name => name;

    public static readonly TextOf<string> Named = static name => name;

    public static readonly IItemsSource<string> Options = ItemsSource.From(
        (string[])["Option A", "Option B", "Option C"]
    );

    public static readonly IItemsSource<string> Letters = ItemsSource.From(
        (string[])["Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta"]
    );

    public static readonly IItemsSource<string> Roles = ItemsSource.From(
        (string[])["Engineer", "Designer", "Writer"]
    );

    public static readonly IItemsSource<string> OpenRoles = ItemsSource.From(
        (string[])["Engineer", "Designer", "Tester"]
    );

    static readonly string[] Towns = Name(10_000);

    public static readonly IItemsSource<string> ManyTowns = ItemsSource.From(Towns);

    public static readonly string FirstTown = Towns[0];

    static string[] Name(int count)
    {
        string[] names =
        [
            "Ashford",
            "Brightwater",
            "Coldharbour",
            "Dunmere",
            "Eastwick",
            "Fairhaven",
        ];
        var towns = new string[count];
        for (var i = 0; i < towns.Length; i++)
            towns[i] = $"{names[i % names.Length]} {i / names.Length + 1}";
        return towns;
    }
}

/// <summary>A server a sign-in form connects to, and the latency the client last measured to it.</summary>
sealed class Server
{
    static readonly string[] Pings = Spell();

    public static readonly IItemsSource<Server> All = ItemsSource.From(
        (Server[])
            [
                new("Europe West", 24),
                new("Europe East", 41),
                new("North America", 96),
                new("South America", 132),
                new("Asia", 180),
            ]
    );

    public static readonly KeyOf<Server> ByName = static s => s.Name;

    public static readonly TextOf<Server> Named = static s => s.Name;

    public static readonly TextOf<Server> PingOf = static s => Pings[Math.Clamp(s.Ping, 0, 999)];

    Server(string name, int ping)
    {
        Name = name;
        Ping = ping;
    }

    public string Name { get; }

    public int Ping { get; }

    static string[] Spell()
    {
        var all = new string[1000];
        for (var i = 0; i < all.Length; i++)
            all[i] = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return all;
    }
}

/// <summary>An account a sign-in form remembers, or the row that asks for another, and how to forget it.</summary>
sealed class SavedAccount(SavedAccounts owner, string name, bool isAnother = false)
{
    public string Name { get; } = name;

    public bool IsAnother { get; } = isAnother;

    SavedAccounts Owner { get; } = owner;

    public Callback Forget => Callback.Of(this, static a => a.Owner.Accounts.Remove(a));

    public static readonly KeyOf<SavedAccount> ByName = static a => a.Name;

    public static readonly TextOf<SavedAccount> Named = static a => a.Name;
}

/// <summary>The accounts a sign-in form's picker lists.</summary>
sealed class SavedAccounts
{
    public readonly ObservableList<SavedAccount> Accounts = new();

    public SavedAccounts()
    {
        Accounts.Add(new SavedAccount(this, "Aria Vell"));
        Accounts.Add(new SavedAccount(this, "Bo Tamsin"));
        Accounts.Add(new SavedAccount(this, "Cass Orrin"));
        Accounts.Add(new SavedAccount(this, "Use another account", isAnother: true));
        Items = ItemsSource.From(Accounts);
    }

    public IItemsSource<SavedAccount> Items { get; }
}
