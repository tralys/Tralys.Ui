using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Tralys.Ui;

public sealed partial class Node
{
    // The record Equals copies both styles, hundreds of bytes each; Same compares them in place and must agree.
    static bool Same(in LayoutStyle stored, in LayoutStyle written)
    {
        var same = stored.Same(written);
        Debug.Assert(same == stored.Equals(written));
        return same;
    }

    static bool Same(in VisualStyle stored, in VisualStyle written)
    {
        var same = stored.Same(written);
        Debug.Assert(same == stored.Equals(written));
        return same;
    }

    static bool Same(in InheritStyle stored, in InheritStyle written)
    {
        var same = stored.Same(written);
        Debug.Assert(same == stored.Equals(written));
        return same;
    }

    static bool Same(in Placement stored, in Placement written)
    {
        var same = stored.Same(written);
        Debug.Assert(same == stored.Equals(written));
        return same;
    }

    internal bool WriteLayout(in LayoutStyle written)
    {
        if (Same(Layout, written))
            return false;
        var style = written;
        InRange(ref style, Layout);
        if (Layout.Equals(style))
            return false;
        Layout = style;
        var view = MountedView;
        view.InvalidateSize(this);
        view.MarkPaint(this);
        view.MarkComposite();
        return true;
    }

    internal bool WriteVisual(in VisualStyle written)
    {
        if (Same(Visual, written))
            return false;
        var style = written;
        InRange(ref style, Visual);
        if (Visual.Equals(style))
            return false;
        var draws = Visual.DrawsLike(style);
        var composites = Visual.CompositesLike(style);
        var fades = !composites && Visual.CompositesLikeAtAnyOpacity(style);
        var was = Visual.Opacity ?? 1f;
        Visual = style;
        if (!draws)
            MountedView.MarkPaint(this);
        if (fades)
            MountedView.OpacityChanged(this, was, style.Opacity ?? 1f);
        else if (!composites)
            MountedView.MarkComposite();
        return true;
    }

    internal bool WriteInherit(in InheritStyle written)
    {
        if (Same(Inherit, written))
            return false;
        var style = written;
        InRange(ref style, Inherit);
        if (Inherit.Equals(style))
            return false;
        var old = Inherit;
        Inherit = style;
        var view = MountedView;
        if (!old.StylesLike(style))
        {
            Dirty |= Dirty.Env;
            view.MarkEnvBelow(this);
        }

        var changed = EnvReads.None;
        if (!ReferenceEquals(old.Theme, style.Theme))
            changed |= EnvReads.Theme;
        if (old.Density != style.Density)
            changed |= EnvReads.Density;
        if (old.ForceStates != style.ForceStates || old.ForceStatesOn != style.ForceStatesOn)
        {
            view.HeldChanged(this);
            if (old.ForceStatesOn is not null || style.ForceStatesOn is not null)
                view.ScopeChanged(this);
        }
        if (
            old.Disabled != style.Disabled
            || ((old.ForcedInside ^ style.ForcedInside) & InputStates.Disabled) != 0
        )
        {
            changed |= EnvReads.Disabled;
            view.UpdateDisabled(this);
        }
        if (changed != EnvReads.None)
            UiView.InvalidateEnv(this, changed);
        return true;
    }

    internal bool WritePlacement(in Placement written)
    {
        if (Same(Place, written))
            return false;
        var place = written;
        InRange(ref place, Place);
        if (Place.Equals(place))
            return false;
        var lays = Place.LaysOutLike(place);
        var paints = Place.PaintsLike(place);
        var wasShown = !(Place.Hidden || Place.Collapsed);
        var isShown = !(place.Hidden || place.Collapsed);
        Place = place;
        var view = MountedView;
        if (wasShown != isShown)
            UiView.VisibilityFlipped(this);
        if (isShown && !wasShown)
            view.ShownAgain(this);
        if (!lays)
            view.InvalidateMeasure(LayoutParent ?? this);
        if (!paints)
            view.MarkComposite();
        return true;
    }

    internal bool WriteInteraction(in InteractionProps props)
    {
        if (Interaction is null)
        {
            if (props.IsEmpty)
            {
                // A member IsEmpty doesn't check would be dropped from every node it alone is set on.
                Debug.Assert(props == default);
                return false;
            }
            Interaction = MountedView.RentInteraction();
        }
        if (Interaction.Props.Equals(props))
            return false;
        var old = Interaction.Props;
        Interaction.Props = props;
        MountedView.InteractionChanged(this, old, props);
        return true;
    }

    // A text takes only a click and a cursor, so it builds the 680-byte props only when it has one or held one.
    internal bool WriteClick(Callback onClick, Cursor? cursor) =>
        (Interaction is not null || !onClick.IsEmpty || cursor is not null)
        && WriteClickProps(onClick, cursor);

    [MethodImpl(MethodImplOptions.NoInlining)]
    bool WriteClickProps(Callback onClick, Cursor? cursor) =>
        WriteInteraction(new InteractionProps { OnClick = onClick, Cursor = cursor });

    internal bool WriteSelected(bool selected)
    {
        OwnSelected = selected;
        selected |= RowSelected;
        if (((_states & InputStates.Selected) != 0) == selected)
            return false;
        SetStates(selected ? _states | InputStates.Selected : _states & ~InputStates.Selected);
        return true;
    }

    internal bool WriteRef(NodeRef? nodeRef)
    {
        if (Ref == nodeRef)
            return false;
        Ref?.Bind(null);
        nodeRef?.Bind(this);
        return true;
    }
}
