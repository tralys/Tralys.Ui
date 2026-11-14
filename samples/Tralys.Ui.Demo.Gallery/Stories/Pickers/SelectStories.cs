using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Select<string>>("Pickers")]
static class SelectStories
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
        ui.Select(
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
        ui.Select(
            items: Choices.Letters,
            itemKey: Choices.ByName,
            itemText: Choices.Named,
            searchable: true,
            value: value.Value,
            onValueChange: value.Setter,
            width: 200
        );
    }

    [FramePath]
    public static void Disabled(Ui ui) =>
        ui.Select(
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
            ui.Select(
                items: Choices.Roles,
                itemKey: Choices.ByName,
                itemText: Choices.Named,
                value: role.Value,
                onValueChange: role.Setter,
                placeholder: "Pick a role"
            );
            ui.Select(
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
            ui.Select(
                items: Choices.Roles,
                itemKey: Choices.ByName,
                itemText: Choices.Named,
                value: role.Value,
                onValueChange: role.Setter,
                sublabel: "Role"
            );
            ui.Select(
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
                ui.Select(
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
                "Ten thousand towns: the dropdown realizes only the rows in view; the arrows, Page Up and Page Down, Home, End and typing a town's first letters move through them"
            );
            ui.Select(
                items: Choices.ManyTowns,
                itemKey: Choices.ByName,
                itemText: Choices.Named,
                value: town.Value,
                onValueChange: town.Setter
            );
        }
    }
}

[Stories<Select<Server>>("Pickers")]
static class ServerSelectStories
{
    [FramePath]
    public static void DisplayMember(Ui ui)
    {
        var byName = ui.State<UiKey>("Europe West");
        var byPing = ui.State<UiKey>("Europe West");
        using (ui.Box(direction: Direction.Column, gap: 8, width: 200))
        {
            ui.Select(
                items: Server.All,
                itemKey: Server.ByName,
                itemText: Server.Named,
                searchable: true,
                value: byName.Value,
                onValueChange: byName.Setter,
                sublabel: "Server"
            );
            ui.Select(
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

[Stories<Select<SignInAccount>>("Pickers")]
static class AccountSelectStories
{
    static readonly Slot<SignInAccount> Row = Slot<SignInAccount>.Of(
        static (ui, account, _) =>
            ui.SelectItem(
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

    static readonly Slot<SignInAccount> Chosen = Slot<SignInAccount>.Of(
        static (ui, account, _) =>
        {
            using (ui.Box(align: Align.Center, gap: 8))
            {
                ui.Text(account.Name);
                ui.Text(
                    account.Server,
                    color: ui.Theme.Color(ColorToken.FgMuted),
                    variant: TextVariant.Caption
                );
            }
        }
    );

    [FramePath]
    public static void Templated(Ui ui)
    {
        var accounts = ui.Remember(static () => new SignInAccounts());
        var chosen = ui.State<UiKey>(default);
        using (ui.Box(direction: Direction.Column, gap: 8, width: 320))
        {
            HoldingListBoxStories.Caption(
                ui,
                "The closed box draws its own template; each row's forget button runs a command it reads from the form's root, across the dropdown"
            );
            ui.Select(
                items: accounts.Items,
                itemKey: SignInAccount.ByName,
                item: Row,
                valueContent: Chosen,
                value: chosen.Value,
                onValueChange: chosen.Setter,
                placeholder: "Choose an account",
                size: ComponentSize.Lg
            );
        }
    }
}

[Stories<Select.Item>("Pickers")]
static class SelectItemStories
{
    static readonly IItemsSource<LockableRole> Roster = ItemsSource.From(
        (LockableRole[])
            [
                new("Engineer", false),
                new("Designer", false),
                new("Writer (locked)", true),
                new("Tester", false),
            ]
    );

    static readonly KeyOf<LockableRole> ByName = static r => r.Name;

    static readonly TextOf<LockableRole> Named = static r => r.Name;

    static readonly Func<LockableRole, bool> Locked = static r => r.IsLocked;

    [FramePath]
    public static void States(Ui ui)
    {
        var role = ui.State<UiKey>("Designer");
        using (ui.Box(direction: Direction.Column, gap: 8, width: 240, height: 220))
        {
            HoldingListBoxStories.Caption(
                ui,
                "The open dropdown's rows: the selected one in the accent, lit while the keyboard or the pointer is on it, a locked one disabled"
            );
            ui.Select(
                items: Roster,
                itemKey: ByName,
                itemText: Named,
                itemDisabled: Locked,
                value: role.Value,
                onValueChange: role.Setter,
                defaultOpen: true
            );
        }
    }
}

/// <summary>A role a team can staff, or one locked against changes.</summary>
sealed record LockableRole(string Name, bool IsLocked);

/// <summary>An account a sign-in form remembers on a server, or the row that asks for another, and how to forget
/// it.</summary>
sealed class SignInAccount(SignInAccounts owner, string name, string server, bool isAnother = false)
{
    public string Name { get; } = name;

    public string Server { get; } = server;

    public bool IsAnother { get; } = isAnother;

    SignInAccounts Owner { get; } = owner;

    public Callback Forget => Callback.Of(this, static a => a.Owner.Accounts.Remove(a));

    public static readonly KeyOf<SignInAccount> ByName = static a => a.Name;
}

/// <summary>The accounts a sign-in form's select lists.</summary>
sealed class SignInAccounts
{
    public readonly ObservableList<SignInAccount> Accounts = new();

    public SignInAccounts()
    {
        Accounts.Add(new SignInAccount(this, "Aria Vell", "Europe West"));
        Accounts.Add(new SignInAccount(this, "Bo Tamsin", "Europe West"));
        Accounts.Add(new SignInAccount(this, "Cass Orrin", "Asia"));
        Accounts.Add(new SignInAccount(this, "Use another account", "", isAnother: true));
        Items = ItemsSource.From(Accounts);
    }

    public IItemsSource<SignInAccount> Items { get; }
}
