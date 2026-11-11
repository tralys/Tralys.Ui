using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Thumb>("Inputs")]
static class ThumbStories
{
    [FramePath]
    public static void Drag(Ui ui)
    {
        var t = ui.Theme;
        var first = ui.Remember(static () => new Placed());
        var second = ui.Remember(static () => new Placed());
        first.At = ui.State(new Vector2(40f, 40f));
        second.At = ui.State(new Vector2(200f, 120f));
        first.LaidOut = first.At.Value;
        second.LaidOut = second.At.Value;
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            ui.Text(
                "A thumb reports how far it is dragged and moves nothing itself; a behaviour moves these across the canvas",
                color: t.Color(ColorToken.FgMuted),
                fontWeight: t.Weight(WeightToken.Interface),
                wrap: true
            );
            using (
                ui.Box(
                    width: 480,
                    height: 240,
                    overflow: Overflow.Clip,
                    background: t.Color(ColorToken.BgSunken),
                    alignSelf: AlignSelf.Start
                )
            )
            {
                using (
                    ui.Thumb(
                        onDragDelta: first.Moved,
                        background: t.Color(ColorToken.BgAccent),
                        border: new Border(1f, t.Color(ColorToken.BorderAccent)),
                        width: 64,
                        height: 64,
                        position: Position.Absolute,
                        left: first.At.Value.X,
                        top: first.At.Value.Y,
                        id: "first"
                    )
                ) { }
                using (
                    ui.Thumb(
                        onDragDelta: second.Moved,
                        background: t.Color(ColorToken.BgRaised),
                        border: new Border(2f, t.Color(ColorToken.Border)),
                        width: 120,
                        height: 48,
                        position: Position.Absolute,
                        left: second.At.Value.X,
                        top: second.At.Value.Y,
                        id: "second"
                    )
                ) { }
            }
        }
    }

    [FramePath]
    sealed class Placed
    {
        internal readonly Callback<Vector2> Moved;
        internal State<Vector2> At = null!;
        internal Vector2 LaidOut;

        public Placed() =>
            Moved = Callback<Vector2>.Of(this, static (p, by) => p.At.Value = p.LaidOut + by);
    }
}
