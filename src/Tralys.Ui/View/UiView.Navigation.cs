namespace Tralys.Ui;

public sealed partial class UiView
{
    ValueList<OnceMemory> _onceMemory;

    bool TabNavigate(bool backward)
    {
        var root = FocusScopeRoot();
        var focused = _windowInactive ? null : _focused;
        Node? next;
        if (focused is null || !root.IsAncestorOrSelfOf(focused))
            next = backward ? LastIn(root, root) : FirstIn(root, root);
        else
            next = backward ? Previous(root, focused) : Next(root, focused);
        if (next is null)
            return false;
        MoveFocus(next, FocusCause.Tab, visible: true);
        return true;
    }

    bool ArrowNavigate(NavDirection direction)
    {
        if (_windowInactive || _focused is not { } focused)
            return false;
        var root = FocusScopeRoot();
        if (!root.IsAncestorOrSelfOf(focused) || Move(root, focused, direction) is not { } next)
            return false;
        MoveFocus(next, FocusCause.Arrow, visible: true);
        return true;
    }

    /// <summary>Runs the arrow keys' search from <paramref name="from"/>: within <paramref name="within"/>'s subtree
    /// only when given, else through the arrow groups around it as an arrow key would.</summary>
    /// <returns>The stop the search lands on, <paramref name="from"/> when a contained group keeps it, or null.</returns>
    public Node? Navigate(Node from, NavDirection direction, Node? within = null)
    {
        ArgumentNullException.ThrowIfNull(from);
        VerifyAccess();
        if (within is null)
            return Move(FocusScopeRoot(), from, direction);
        var aim = new Aim(from, from.ViewBounds, direction, wrap: false);
        Sweep(within, ref aim);
        return aim.Best;
    }

    bool Navigable(Node node) =>
        node.IsMounted
        && node.View == this
        && !node.Place.Hidden
        && !node.Place.Collapsed
        && !node.FaultHidden
        && !node.LayoutHidden
        && !node.Exiting
        && (node._states & InputStates.Disabled) == 0;

    static bool IsStop(Node node) =>
        !node.IsTransparent
        && node.IsFocusable
        && node.TabIndex >= 0
        && (node._states & InputStates.Disabled) == 0;

    static int TabKey(Node node) => node.TabIndex is > 0 and var index ? index : int.MaxValue;

    static Navigation TabModeOf(Node node) =>
        node.IsTransparent ? Navigation.Continue : node.Interaction?.Props.TabNavigation ?? default;

    static Navigation ArrowModeOf(Node node) =>
        node.IsTransparent ? Navigation.Continue
        : node.Interaction?.Props.ArrowNavigation is { } mode && mode != Navigation.Once ? mode
        : Navigation.Continue;

    static bool IsGroup(Node node, Node root) =>
        node == root || TabModeOf(node) != Navigation.Continue;

    static Navigation ModeOf(Node group, Node root)
    {
        var mode = TabModeOf(group);
        return group == root && mode == Navigation.Continue ? Navigation.Cycle : mode;
    }

    static Node? GroupOf(Node node, Node root)
    {
        if (node == root)
            return null;
        for (var parent = node._parent; parent is not null; parent = parent._parent)
        {
            if (IsGroup(parent, root))
                return parent;
        }
        return null;
    }

    Node? Next(Node root, Node current)
    {
        if (
            current != root
            && IsGroup(current, root)
            && TabModeOf(current) is not (Navigation.None or Navigation.Once)
            && FirstIn(current, root) is { } inner
        )
            return inner;

        var x = current;
        while (GroupOf(x, root) is { } group)
        {
            var mode = ModeOf(group, root);
            if (mode is not (Navigation.None or Navigation.Once))
            {
                var found = Seek(group, root, Seeking.After, TabKey(x), OrderOf(group, root, x));
                if (EnterFrom(group, root, found, forward: true) is { } target)
                    return target;
                if (mode == Navigation.Cycle)
                    return FirstIn(group, root) ?? current;
                if (mode == Navigation.Contained)
                    return current;
            }
            x = group;
        }
        return FirstIn(x, root);
    }

    Node? Previous(Node root, Node current)
    {
        var x = current;
        while (GroupOf(x, root) is { } group)
        {
            var mode = ModeOf(group, root);
            if (mode is not (Navigation.None or Navigation.Once))
            {
                var found = Seek(group, root, Seeking.Before, TabKey(x), OrderOf(group, root, x));
                if (EnterFrom(group, root, found, forward: false) is { } target)
                    return target;
                if (mode == Navigation.Cycle)
                    return LastIn(group, root) ?? current;
                if (mode == Navigation.Contained)
                    return current;
            }
            if (mode != Navigation.Once && group != root && IsStop(group))
                return group;
            x = group;
        }
        return LastIn(x, root);
    }

    Node? FirstIn(Node group, Node root) =>
        EnterFrom(group, root, Seek(group, root, Seeking.First, 0, 0), forward: true);

    Node? LastIn(Node group, Node root) =>
        EnterFrom(group, root, Seek(group, root, Seeking.Last, 0, 0), forward: false);

    Node? EnterFrom(Node group, Node root, Found found, bool forward)
    {
        var seeking = forward ? Seeking.After : Seeking.Before;
        while (found.Item is { } item)
        {
            if (Enter(item, root, forward) is { } target)
                return target;
            found = Seek(group, root, seeking, found.TabKey, found.Order);
        }
        return null;
    }

    Node? Enter(Node item, Node root, bool forward)
    {
        if (!IsGroup(item, root))
            return item;
        var mode = TabModeOf(item);
        if (mode == Navigation.None)
            return IsStop(item) ? item : null;
        if (mode == Navigation.Once)
            return SelectedIn(item)
                ?? RememberedIn(item)
                ?? (IsStop(item) ? item : FirstIn(item, root));
        if (forward)
            return IsStop(item) ? item : FirstIn(item, root);
        return LastIn(item, root) ?? (IsStop(item) ? item : null);
    }

    int OrderOf(Node group, Node root, Node node)
    {
        var scan = new Scan(root, Seeking.None, node, 0, 0);
        Walk(group, ref scan);
        return scan.FoundOrder;
    }

    Found Seek(Node group, Node root, Seeking seeking, int tabKey, int order)
    {
        var scan = new Scan(root, seeking, null, tabKey, order);
        Walk(group, ref scan);
        return new Found(scan.Best, scan.BestTabKey, scan.BestOrder);
    }

    void Walk(Node container, ref Scan scan)
    {
        for (var child = container._first; child is not null; child = child._next)
        {
            if (!Navigable(child))
                continue;
            if (child.IsTransparent)
            {
                Walk(child, ref scan);
                continue;
            }
            var order = scan.Order++;
            if (child == scan.Find)
                scan.FoundOrder = order;
            if (IsGroup(child, scan.Root))
            {
                scan.Consider(child, order);
                continue;
            }
            if (IsStop(child))
                scan.Consider(child, order);
            Walk(child, ref scan);
        }
    }

    Node? SelectedIn(Node container)
    {
        for (var child = container._first; child is not null; child = child._next)
        {
            if (!Navigable(child))
                continue;
            if (
                !child.IsTransparent
                && IsStop(child)
                && (child._states & InputStates.Selected) != 0
            )
                return child;
            if (TabModeOf(child) != Navigation.None && SelectedIn(child) is { } inner)
                return inner;
        }
        return null;
    }

    Node? RememberedIn(Node group)
    {
        for (var i = 0; i < _onceMemory.Count; i++)
        {
            var memory = _onceMemory[i];
            if (memory.Group != group || memory.GroupGeneration != group.Generation)
                continue;
            var active = memory.Active;
            return
                active.Generation == memory.ActiveGeneration
                && CanFocus(active)
                && IsStop(active)
                && group.IsAncestorOrSelfOf(active)
                ? active
                : null;
        }
        return null;
    }

    void RememberFocusInGroups(Node focused)
    {
        for (var group = focused._parent; group is not null; group = group._parent)
        {
            if (TabModeOf(group) != Navigation.Once)
                continue;
            var found = false;
            for (var i = _onceMemory.Count - 1; i >= 0; i--)
            {
                var memory = _onceMemory[i];
                if (!memory.Group.IsMounted || memory.Group.Generation != memory.GroupGeneration)
                {
                    _onceMemory.RemoveAtSwap(i);
                    continue;
                }
                if (memory.Group != group)
                    continue;
                _onceMemory[i] = new OnceMemory(
                    group,
                    group.Generation,
                    focused,
                    focused.Generation
                );
                found = true;
            }
            if (!found)
                _onceMemory.Add(
                    new OnceMemory(group, group.Generation, focused, focused.Generation)
                );
        }
    }

    Node? Move(Node root, Node current, NavDirection direction)
    {
        var origin = current.ViewBounds;
        for (var group = ArrowGroupOf(current, root); ; group = ArrowGroupOf(group, root))
        {
            var mode = ArrowModeOf(group);
            if (mode != Navigation.None)
            {
                var aim = new Aim(current, origin, direction, wrap: false);
                Sweep(group, ref aim);
                if (aim.Best is { } best)
                    return best;
            }
            if (mode == Navigation.Contained)
                return current;
            if (mode == Navigation.Cycle)
            {
                var aim = new Aim(current, origin, direction, wrap: true);
                Sweep(group, ref aim);
                if (aim.Best is { } wrapped)
                    return wrapped;
            }
            if (group == root)
                return null;
        }
    }

    static Node ArrowGroupOf(Node node, Node root)
    {
        for (var parent = node._parent; parent is not null; parent = parent._parent)
        {
            if (parent == root || ArrowModeOf(parent) != Navigation.Continue)
                return parent;
        }
        return root;
    }

    void Sweep(Node container, ref Aim aim)
    {
        for (var child = container._first; child is not null; child = child._next)
        {
            if (!Navigable(child))
                continue;
            if (IsStop(child))
                aim.Consider(child);
            if (ArrowModeOf(child) != Navigation.None)
                Sweep(child, ref aim);
        }
    }

    enum Seeking : byte
    {
        None,
        First,
        Last,
        After,
        Before,
    }

    readonly record struct Found(Node? Item, int TabKey, int Order);

    readonly record struct OnceMemory(
        Node Group,
        int GroupGeneration,
        Node Active,
        int ActiveGeneration
    );

    struct Scan(Node root, Seeking seeking, Node? find, int fromTabKey, int fromOrder)
    {
        public readonly Node Root = root;
        public readonly Node? Find = find;
        public int Order;
        public int FoundOrder = -1;
        public Node? Best;
        public int BestTabKey;
        public int BestOrder;

        public void Consider(Node item, int itemOrder)
        {
            var itemKey = TabKey(item);
            var take = seeking switch
            {
                Seeking.First => Best is null || Before(itemKey, itemOrder, BestTabKey, BestOrder),
                Seeking.Last => Best is null || Before(BestTabKey, BestOrder, itemKey, itemOrder),
                Seeking.After => Before(fromTabKey, fromOrder, itemKey, itemOrder)
                    && (Best is null || Before(itemKey, itemOrder, BestTabKey, BestOrder)),
                Seeking.Before => Before(itemKey, itemOrder, fromTabKey, fromOrder)
                    && (Best is null || Before(BestTabKey, BestOrder, itemKey, itemOrder)),
                _ => false,
            };
            if (!take)
                return;
            Best = item;
            BestTabKey = itemKey;
            BestOrder = itemOrder;
        }

        static bool Before(int key, int order, int otherKey, int otherOrder) =>
            key < otherKey || (key == otherKey && order < otherOrder);
    }

    struct Aim(Node skip, Rect origin, NavDirection direction, bool wrap)
    {
        // Rounded layout can overlap neighbours by a fraction of a unit; they still lie beyond.
        const float Slack = 0.5f;

        public Node? Best;
        bool _bestInBeam;
        float _bestDistance;
        float _bestAlignment;

        public void Consider(Node candidate)
        {
            if (candidate == skip)
                return;
            var target = candidate.ViewBounds;
            var horizontal = direction is NavDirection.Left or NavDirection.Right;
            var side = horizontal
                ? MathF.Max(target.Y - origin.Bottom, origin.Y - target.Bottom)
                : MathF.Max(target.X - origin.Right, origin.X - target.Right);
            var inBeam = side < 0f;
            float distance;
            if (wrap)
            {
                if (!inBeam)
                    return;
                distance = direction switch
                {
                    NavDirection.Left => -target.Right,
                    NavDirection.Right => target.X,
                    NavDirection.Up => -target.Bottom,
                    _ => target.Y,
                };
            }
            else
            {
                var gap = direction switch
                {
                    NavDirection.Left => origin.X - target.Right,
                    NavDirection.Right => target.X - origin.Right,
                    NavDirection.Up => origin.Y - target.Bottom,
                    _ => target.Y - origin.Bottom,
                };
                if (gap < -Slack)
                    return;
                gap = MathF.Max(gap, 0f);
                distance = inBeam ? gap : gap * gap + side * side;
            }

            var alignment = horizontal
                ? MathF.Abs(target.Y + target.Height * 0.5f - (origin.Y + origin.Height * 0.5f))
                : MathF.Abs(target.X + target.Width * 0.5f - (origin.X + origin.Width * 0.5f));
            if (
                Best is not null
                && (
                    (_bestInBeam && !inBeam)
                    || (
                        _bestInBeam == inBeam
                        && (
                            distance > _bestDistance
                            || (distance == _bestDistance && alignment >= _bestAlignment)
                        )
                    )
                )
            )
                return;
            Best = candidate;
            _bestInBeam = inBeam;
            _bestDistance = distance;
            _bestAlignment = alignment;
        }
    }
}
