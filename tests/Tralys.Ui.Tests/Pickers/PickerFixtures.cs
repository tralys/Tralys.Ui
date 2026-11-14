using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Components.Tests;

internal static class Towns
{
    public static readonly KeyOf<string> ByName = static name => name;

    public static readonly TextOf<string> Named = static name => name;

    public static IItemsSource<string> Make(int count, string stem = "Town")
    {
        var names = new string[count];
        for (var i = 0; i < count; i++)
            names[i] = $"{stem} {i + 1}";
        return ItemsSource.From(names);
    }
}

internal sealed partial class Server : ObservableObject
{
    static readonly string[] Pings = Spell();

    public Server(string name, int ping)
    {
        Name = name;
        Ping = ping;
    }

    public string Name { get; }

    [ObservableProperty]
    public partial int Ping { get; set; }

    public static readonly KeyOf<Server> ByName = static s => s.Name;

    public static readonly TextOf<Server> Named = static s => s.Name;

    public static readonly TextOf<Server> PingOf = static s => Pings[s.Ping];

    static string[] Spell()
    {
        var all = new string[1000];
        for (var i = 0; i < all.Length; i++)
            all[i] = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return all;
    }
}

internal sealed class Account(Forgets owner, string name)
{
    public string Name { get; } = name;

    Forgets Owner { get; } = owner;

    public Callback Forget => Callback.Of(this, static a => a.Owner.Accounts.Remove(a));

    public static readonly KeyOf<Account> ByName = static a => a.Name;

    public static readonly TextOf<Account> Named = static a => a.Name;
}

internal sealed class Forgets
{
    public readonly ObservableList<Account> Accounts = new();
    public readonly IItemsSource<Account> Items;

    public Forgets()
    {
        foreach (var name in (string[])["Ada", "Brook", "Cleo", "Dara"])
            Accounts.Add(new Account(this, name));
        Items = ItemsSource.From(Accounts);
    }

    public string[] Names => [.. Accounts.Select(a => a.Name)];
}
