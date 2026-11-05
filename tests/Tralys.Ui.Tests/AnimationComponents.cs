using System.Numerics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Tests;

public sealed class Probe
{
    public float First = float.NaN;

    public float Value;

    public Color Color;

    public bool Exiting;
}

public readonly record struct MoverProps
{
    public float Target { get; init; }

    public Color Color { get; init; }

    public Transition Transition { get; init; }

    public Probe? Probe { get; init; }
}

public sealed class Mover : Component<MoverProps>
{
    protected override void Render(Ui ui, in MoverProps p)
    {
        var value = ui.Animate(p.Target, p.Transition);
        var color = ui.Animate(p.Color, p.Transition);
        var probe = p.Probe!;
        if (float.IsNaN(probe.First))
            probe.First = value;
        probe.Value = value;
        probe.Color = color;
        using (ui.Box(width: 20, height: 20, background: color)) { }
    }
}

public readonly record struct LooperProps
{
    public float Period { get; init; }

    public int Count { get; init; }

    public bool Alternate { get; init; }

    public bool Paused { get; init; }

    public Probe? Probe { get; init; }
}

public sealed class Looper : Component<LooperProps>
{
    protected override void Render(Ui ui, in LooperProps p)
    {
        p.Probe!.Value = ui.Loop(p.Period, p.Count, p.Alternate, paused: p.Paused);
        using (ui.Box(width: 20, height: 20)) { }
    }
}

public readonly record struct BrittleLooperProps
{
    public Model? Model { get; init; }

    public Probe? Probe { get; init; }
}

/// <summary>Holds a looper, then throws while its model's count is 1.</summary>
public sealed class BrittleLooper : Component<BrittleLooperProps>
{
    protected override void Render(Ui ui, in BrittleLooperProps p)
    {
        var count = p.Model!.Count;
        using (ui.Box())
        {
            ui.Looper(period: 1f, probe: p.Probe);
            if (count == 1)
                throw new InvalidOperationException("The render failed.");
        }
    }
}

public readonly record struct WiperProps;

/// <summary>A bar masked by a three-stop gradient whose middle stop fades out and back forever.</summary>
public sealed class Wiper : Component<WiperProps>
{
    static readonly Color Ink = Color.FromRgb(0, 0, 0);

    protected override void Render(Ui ui, in WiperProps p)
    {
        var shown = 1f - ui.Loop(0.3f, alternate: true);
        var stops = ui.Remember(static () =>
            new GradientStops([new(0f, Ink), new(0.5f, Ink), new(1f, Ink)])
        );
        stops.Set([new(0f, Ink), new(0.5f, Ink.WithOpacity(shown)), new(1f, Ink)]);
        using (
            ui.Box(
                width: 200,
                height: 16,
                background: Color.FromRgb(40, 160, 80),
                mask: Paint.Linear(stops, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f))
            )
        ) { }
    }
}

public readonly record struct NoticeProps
{
    public int Number { get; init; }

    public Transition Exit { get; init; }

    public Probe? Probe { get; init; }

    public Handler<DragEvent> OnDrag { get; init; }
}

public sealed class Notice : Component<NoticeProps>
{
    protected override void Render(Ui ui, in NoticeProps p)
    {
        var shown = ui.Presence(exit: p.Exit);
        if (p.Probe is { } probe)
        {
            if (float.IsNaN(probe.First))
                probe.First = shown;
            probe.Value = shown;
            probe.Exiting = ui.IsExiting;
        }
        using (
            ui.Box(
                width: 60,
                height: 20,
                opacity: shown,
                background: Color.FromRgb(40, 40, 40),
                onClick: Callback.Of(static () => { }),
                onDrag: p.OnDrag
            )
        )
            ui.Text(ui.Format($"notice {p.Number}"));
    }
}
