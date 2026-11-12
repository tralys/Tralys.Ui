using System.Numerics;

namespace Tralys.Ui.Components;

/// <summary>An item a list's row was dragged to move: from <see cref="From"/> to <see cref="To"/>, the index it takes
/// once its own place is vacated.</summary>
public readonly record struct ListReorder(UiKey Value, int From, int To);

/// <summary>Where a list shows the insert line, in its own box's space; <see cref="Shown"/> false while it shows
/// none.</summary>
internal readonly record struct ReorderLine(
    bool Shown,
    float X,
    float Y,
    float Length,
    bool Upright = false
)
{
    internal const float Thickness = 1.5f;
}

internal readonly record struct ReorderLineLook(ReorderLine Line, Color Color)
{
    public static readonly CanvasDraw<ReorderLineLook> Draw = static (dc, s, _) =>
        dc.DrawLine(
            new Vector2(s.Line.X, s.Line.Y),
            s.Line.Upright
                ? new Vector2(s.Line.X, s.Line.Y + s.Line.Length)
                : new Vector2(s.Line.X + s.Line.Length, s.Line.Y),
            ReorderLine.Thickness,
            s.Color
        );
}

/// <summary>A list whose rows drag to new places: the rows are the drag's sources and targets, their list the drag's
/// data, so only its own rows land on it.</summary>
[FramePath]
internal abstract class RowReorder
{
    internal readonly Callback Leave;
    internal readonly Callback<DragEffect> End;
    internal NodeRef? Frame;
    internal State<ReorderLine>? Line;
    internal bool Sideways;
    UiKey _carried;
    bool _carrying;

    protected RowReorder()
    {
        Leave = Callback.Of(this, static r => r.Hide());
        End = Callback<DragEffect>.Of(this, static (r, _) => r.Ended());
    }

    internal abstract bool CanMove(in ListReorder move);

    protected void ForgetReorder()
    {
        Frame = null;
        Line = null;
        _carried = default;
        _carrying = false;
    }

    internal abstract int IndexOf(UiKey key);

    /// <summary>Reports the move and the moved item as the selection.</summary>
    internal abstract void Moved(in ListReorder move);

    internal void Dragged(RowSpot spot, ref DragEvent e)
    {
        if (e.Phase != DragPhase.Start)
            return;
        if (
            (e.Pointer.Kind != PointerKind.Mouse && !e.IsHeld)
            || !CanMove(new ListReorder(spot.Key, spot.Index, spot.Index))
        )
        {
            e.PreventDefault();
            return;
        }
        _carried = spot.Key;
        _carrying = true;
    }

    internal void Over(RowSpot spot, ref DropEvent e)
    {
        if (
            !Carries(e.Data)
            || spot.Root?.Node is not { } row
            || Frame?.Node is not { } frame
            || Line is not { } line
        )
        {
            e.Effect = DragEffect.None;
            Hide();
            return;
        }
        e.Effect = DragEffect.Move;
        var box = frame.ViewBounds;
        var at = row.ViewBounds;
        if (Sideways)
        {
            var x = After(row, e.Position) ? at.Right : at.X;
            line.Value = new ReorderLine(
                true,
                Math.Clamp(x - box.X, 0f, box.Width),
                at.Y - box.Y,
                at.Height,
                Upright: true
            );
            return;
        }
        var y = After(row, e.Position) ? at.Bottom : at.Y;
        line.Value = new ReorderLine(
            true,
            at.X - box.X,
            Math.Clamp(y - box.Y, 0f, box.Height),
            at.Width
        );
    }

    internal void Dropped(RowSpot spot, ref DropEvent e)
    {
        Hide();
        var from = Carries(e.Data) ? IndexOf(_carried) : -1;
        if (from < 0 || spot.Root?.Node is not { } row)
        {
            e.Effect = DragEffect.None;
            return;
        }
        var to = After(row, e.Position) ? spot.Index + 1 : spot.Index;
        if (from < to)
            to--;
        var move = new ListReorder(_carried, from, to);
        if (to == from || !CanMove(move))
        {
            e.Effect = DragEffect.None;
            return;
        }
        Moved(move);
    }

    void Ended()
    {
        _carrying = false;
        _carried = default;
        Hide();
    }

    void Hide()
    {
        if (Line is { } line)
            line.Value = default;
    }

    bool Carries(object? data) => _carrying && ReferenceEquals(data, this);

    bool After(Node row, Vector2 position) =>
        Sideways ? position.X > row.Bounds.Width / 2f : position.Y > row.Bounds.Height / 2f;
}

/// <summary>A row of a <see cref="RowReorder"/>: its item's key and index, and the handlers its root drags and takes
/// drops with.</summary>
[FramePath]
internal sealed class RowSpot : IResettable
{
    internal readonly Handler<DragEvent> Drag;
    internal readonly Handler<DropEvent> Over;
    internal readonly Handler<DropEvent> Drop;
    internal RowReorder? List;
    internal UiKey Key;
    internal int Index = -1;
    internal NodeRef? Root;

    public RowSpot()
    {
        Drag = Handler<DragEvent>.Of(this, static (s, ref e) => s.List?.Dragged(s, ref e));
        Over = Handler<DropEvent>.Of(this, static (s, ref e) => s.List?.Over(s, ref e));
        Drop = Handler<DropEvent>.Of(this, static (s, ref e) => s.List?.Dropped(s, ref e));
    }

    public void Reset()
    {
        List = null;
        Key = default;
        Index = -1;
        Root = null;
    }
}
