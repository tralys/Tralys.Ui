using System.Numerics;

namespace Tralys.Ui;

public sealed partial class Node
{
    internal ScrollHandle? Scroller;
    internal ScrollProps ScrollProps;
    internal bool PansFromPressables;
    internal Vector2 ScrollApplied;
    internal bool RowFocusable;
    internal bool RowSelected;
    internal bool OwnSelected;
    internal bool SpareRow;

    internal bool ScrollsX => Layout.Overflow is Overflow.ScrollX or Overflow.Scroll;

    internal bool ScrollsY => Layout.Overflow is Overflow.ScrollY or Overflow.Scroll;

    internal bool IsFocusable => RowFocusable || Interaction?.Props.IsFocusable == true;

    internal bool WriteScroll(in ScrollProps props, ScrollHandle? handle, bool scrolls)
    {
        var view = View!;
        var changed = false;
        if (!scrolls && handle is null)
        {
            if (Scroller is not null)
            {
                view.DropScroller(this);
                changed = true;
            }
        }
        else if (handle is not null ? Scroller != handle : Scroller is not { Implicit: true })
        {
            view.BindScroller(this, handle);
            changed = true;
        }
        if (ScrollProps != props)
        {
            if (!ReferenceEquals(ScrollProps.FollowScroll, props.FollowScroll))
            {
                view.Followed(this, props.FollowScroll is not null);
                view.InvalidateArrange(this);
            }
            ScrollProps = props;
            changed = true;
        }
        return changed;
    }

    internal void SetRowSelected(bool selected)
    {
        if (RowSelected == selected)
            return;
        RowSelected = selected;
        var shown = selected || OwnSelected;
        SetStates(shown ? _states | InputStates.Selected : _states & ~InputStates.Selected);
    }

    void ResetScroll()
    {
        Scroller = null;
        ScrollProps = default;
        PansFromPressables = false;
        ScrollApplied = default;
        RowFocusable = false;
        RowSelected = false;
        OwnSelected = false;
        SpareRow = false;
    }
}
