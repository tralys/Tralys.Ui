using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Gallery.Stories;

[Stories<MultiSelect<Permission>>("Pickers")]
static class MultiSelectStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var granted = ui.Remember(static () => Permissions.Of(Permission.Write, Permission.Deploy));
        ui.MultiSelect(
            items: Permissions.All,
            itemKey: Permissions.ByFlag,
            itemText: Permissions.Named,
            value: granted,
            onValueChange: Permissions.Apply(granted),
            placeholder: "Any permission",
            width: 200
        );
    }

    [FramePath]
    public static void Disabled(Ui ui)
    {
        var granted = ui.Remember(static () => Permissions.Of(Permission.Write, Permission.Deploy));
        ui.MultiSelect(
            items: Permissions.All,
            itemKey: Permissions.ByFlag,
            itemText: Permissions.Named,
            value: granted,
            disabled: true,
            width: 200
        );
    }
}

[Stories<MultiSelect<PermissionOption>>("Pickers")]
static class OptionMultiSelectStories
{
    [FramePath]
    public static void FlagMember(Ui ui)
    {
        var chosen = ui.Remember(static () => new SelectionSet());
        ui.MultiSelect(
            items: PermissionOption.All,
            itemKey: PermissionOption.ByFlag,
            itemText: PermissionOption.Labelled,
            value: chosen,
            onValueChange: PermissionOption.Apply(chosen),
            placeholder: "Any permission",
            sublabel: "Access",
            width: 200
        );
    }

    [FramePath]
    public static void Open(Ui ui)
    {
        var granted = ui.Remember(static () =>
            PermissionOption.Of(Permission.Write, Permission.Deploy)
        );
        using (ui.Box(direction: Direction.Column, width: 320, height: 260))
            ui.MultiSelect(
                items: PermissionOption.All,
                itemKey: PermissionOption.ByFlag,
                itemText: PermissionOption.Labelled,
                value: granted,
                onValueChange: PermissionOption.Apply(granted),
                defaultOpen: true,
                sublabel: "Access",
                width: 220
            );
    }
}

/// <summary>The permissions a role grants.</summary>
[Flags]
enum Permission
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
    Review = 1 << 2,
    Deploy = 1 << 3,
    Admin = 1 << 4,
}

static class Permissions
{
    static readonly string[] Names = Enum.GetNames<Permission>();
    static readonly Permission[] Values = Enum.GetValues<Permission>();

    public static readonly IItemsSource<Permission> All = ItemsSource.From(
        new[]
        {
            Permission.Read,
            Permission.Write,
            Permission.Review,
            Permission.Deploy,
            Permission.Admin,
        }
    );

    public static readonly KeyOf<Permission> ByFlag = static flag => (int)flag;

    public static readonly TextOf<Permission> Named = static flag =>
        Names[Array.IndexOf(Values, flag)];

    public static SelectionSet Of(params ReadOnlySpan<Permission> flags)
    {
        var set = new SelectionSet();
        foreach (var flag in flags)
            set.Apply(new SelectionChange(SelectionAction.Toggle, (int)flag, default), All, ByFlag);
        return set;
    }

    [FramePath]
    public static Callback<SelectionChange> Apply(SelectionSet set) =>
        Callback<SelectionChange>.Of(set, static (s, change) => s.Apply(change, All, ByFlag));
}

/// <summary>A permission beside the label a picker shows for it.</summary>
sealed record PermissionOption(Permission Flag, string Label)
{
    public static readonly IItemsSource<PermissionOption> All = ItemsSource.From(
        new PermissionOption[]
        {
            new(Permission.Read, "Read"),
            new(Permission.Write, "Write"),
            new(Permission.Review, "Review"),
            new(Permission.Deploy, "Deploy / Roll back"),
            new(Permission.Admin, "Admin"),
        }
    );

    public static readonly KeyOf<PermissionOption> ByFlag = static option => (int)option.Flag;

    public static readonly TextOf<PermissionOption> Labelled = static option => option.Label;

    public static SelectionSet Of(params ReadOnlySpan<Permission> flags)
    {
        var set = new SelectionSet();
        foreach (var flag in flags)
            set.Apply(new SelectionChange(SelectionAction.Toggle, (int)flag, default), All, ByFlag);
        return set;
    }

    [FramePath]
    public static Callback<SelectionChange> Apply(SelectionSet set) =>
        Callback<SelectionChange>.Of(set, static (s, change) => s.Apply(change, All, ByFlag));
}
