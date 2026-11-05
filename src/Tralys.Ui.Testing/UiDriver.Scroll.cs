using System.Numerics;

namespace Tralys.Ui.Testing;

public sealed partial class UiDriver
{
    /// <summary>Turns the wheel by <paramref name="notches"/> over the centre of <paramref name="over"/>'s visible
    /// bounds: positive Y away from the user, positive X to the right.</summary>
    /// <returns>Whether the UI took the turn, so the host would keep it from the scene behind.</returns>
    public bool Wheel(Node over, Vector2 notches, bool settle = true)
    {
        var visible = VisibleBounds(Acted(over));
        var taken = View.Wheel(
            Pixels(new Vector2(visible.X + visible.Width / 2f, visible.Y + visible.Height / 2f)),
            notches,
            default
        );
        if (settle)
            Settle();
        return taken;
    }
}
