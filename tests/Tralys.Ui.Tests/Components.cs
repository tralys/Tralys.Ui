using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Tests;

public sealed partial class Model : ObservableObject
{
    public Model()
    {
        Name = "";
    }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial int Count { get; set; }

    [ObservableProperty]
    public partial bool Remember { get; set; }
}

public readonly record struct LabelProps
{
    public required Model Model { get; init; }
}

/// <summary>Shows its model's name, so it reads one property.</summary>
public sealed class Label : Component<LabelProps>
{
    protected override void Render(Ui ui, in LabelProps p) => ui.Text(p.Model.Name);
}

public readonly record struct CountLabelProps
{
    public required Model Model { get; init; }
}

/// <summary>Shows its model's count, so it reads another property of the same model.</summary>
public sealed class CountLabel : Component<CountLabelProps>
{
    protected override void Render(Ui ui, in CountLabelProps p) =>
        ui.Text(ui.Format($"{p.Model.Count}"));
}

public readonly record struct TickerProps
{
    public UiKey Value { get; init; }
}

/// <summary>Keeps a click count in local state and shows it.</summary>
public sealed class Ticker : Component<TickerProps>
{
    protected override void Render(Ui ui, in TickerProps p)
    {
        var count = ui.State(0);
        using (ui.Box(onClick: Callback.Of(count, static c => c.Value++), width: 40, height: 20))
            ui.Text(ui.Format($"{count.Value}"));
    }
}

/// <summary>Holds what a <see cref="Kept"/> made last, past its unmount.</summary>
public sealed class Keeper
{
    public Callback Callback;
}

public readonly record struct KeptProps
{
    public required string Name { get; init; }

    public required List<string> Log { get; init; }

    public required Keeper Keeper { get; init; }
}

/// <summary>Hands its keeper a callback on its own host that logs its name.</summary>
public sealed class Kept : Component<KeptProps>
{
    protected override void Render(Ui ui, in KeptProps p) =>
        p.Keeper.Callback = Callback.Of(ui.Self(p), static h => h.Props.Log.Add(h.Props.Name));
}

public readonly record struct FlipProps
{
    public bool Value { get; init; }

    public Callback<bool> OnValueChange { get; init; }
}

/// <summary>A controlled toggle: it shows its value and reports a click as the flipped value.</summary>
public sealed class Flip : Component<FlipProps>
{
    protected override void Render(Ui ui, in FlipProps p)
    {
        using (
            ui.Box(
                width: 20,
                height: 20,
                onClick: Callback.Of(
                    ui.Self(p),
                    static h => h.Props.OnValueChange.Invoke(!h.Props.Value)
                )
            )
        ) { }
    }
}

public readonly record struct RestlessProps
{
    public required Model Model { get; init; }
}

/// <summary>While its model remembers, writes its own state as it renders, so it renders again, up to 100 times.</summary>
public sealed class Restless : Component<RestlessProps>
{
    protected override void Render(Ui ui, in RestlessProps p)
    {
        var renders = ui.State(0);
        if (p.Model.Remember && renders.Peek < 100)
            renders.Value = renders.Peek + 1;
        using (ui.Box(width: 10, height: 10)) { }
    }
}
