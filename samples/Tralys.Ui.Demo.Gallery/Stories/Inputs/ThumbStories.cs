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
        var firstAt = ui.State(new Vector2(40f, 40f));
        var secondAt = ui.State(new Vector2(200f, 120f));
        var first = ui.Remember(firstAt, static at => new Placed(at));
        var second = ui.Remember(secondAt, static at => new Placed(at));
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
        internal readonly State<Vector2> At;
        internal Vector2 LaidOut;

        public Placed(State<Vector2> at)
        {
            At = at;
            Moved = Callback<Vector2>.Of(this, static (p, by) => p.At.Value = p.LaidOut + by);
        }
    }
}
