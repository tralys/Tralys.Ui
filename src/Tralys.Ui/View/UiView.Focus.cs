using Tralys.Ui.Theming;

namespace Tralys.Ui;

public sealed partial class UiView
{
    const int MaxFocusMoves = 64;

    bool _windowInactive;
    bool _focusVisible;
    int _focusMoves;
    int _focusNotifying;
    ValueList<NodeEntry> _autoFocus;

    internal bool FocusReturning;

    /// <summary>Focuses <paramref name="node"/> from code, as <see cref="NodeRef.Focus"/> does, for a node found
    /// outside a render.</summary>
    /// <returns>False, leaving focus alone, when it can't take focus.</returns>
    public bool Focus(Node node, bool visible = false)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Focus(node, FocusCause.Code, visible);
    }

    /// <summary>Leaves nothing focused, from code: the host's way to leave a field, on an Escape the view didn't take
    /// or a soft keyboard put away. The node that had focus is told with <see cref="FocusCause.Code"/>.</summary>
    public void ClearFocus()
    {
        VerifyAccess();
        MoveFocus(null, FocusCause.Code, visible: false);
    }

    internal Node? ReadFocusedWithin(Builder builder)
    {
        if (builder.CurrentHost is not { } host)
            return null;
        ReadStateSet(builder, InputStates.Focused | InputStates.FocusWithin);
        host.EnvReads |= EnvReads.FocusedWithin;
        return
            host.Root is { } root
            && (root._states & (InputStates.Focused | InputStates.FocusWithin)) != 0
            ? _focused
            : null;
    }

    // A move between two nodes inside a component leaves its root's states as they were.
    static void FocusedWithinMoved(Node focused)
    {
        for (var n = focused; n is not null; n = n._parent)
        {
            if (n.Host is { } host && (host.EnvReads & EnvReads.FocusedWithin) != 0)
                host.Invalidate();
        }
    }

    internal void ClearFocusByPointer()
    {
        BeforeInput();
        if (_focused is not null)
            MoveFocus(null, FocusCause.Pointer, visible: false);
    }

    /// <summary>The window gained or lost the keyboard focus; a view starts active. Losing it cancels drags, ends
    /// presses and holds, releases captures, forgets touches and lifts held keys; the focused node stays tracked but
    /// stops showing focused until the window comes back.</summary>
    public void WindowFocus(bool focused)
    {
        BeforeInput();
        if (focused != _windowInactive)
            return;
        if (focused)
        {
            _windowInactive = false;
            ValidateFocus();
            if (_focused is { } back && (back._states & InputStates.Focused) == 0)
            {
                ShowFocus(back, on: true, _focusVisible, keep: null);
                if (back.Interaction?.Props.OnFocusChange is { IsEmpty: false } gained)
                {
                    FocusReturning = true;
                    try
                    {
                        Invoke(gained, new FocusChange(true, FocusCause.Code));
                    }
                    finally
                    {
                        FocusReturning = false;
                    }
                }
            }
            LayersWindowFocus(true);
            return;
        }

        LayersWindowFocus(false);

        CancelDragDrop();
        EndPans();
        for (var i = _pointers.Count - 1; i >= 0; i--)
        {
            var state = _pointers[i];
            CancelGestureDrag(state);
            if (state.Pressing)
                EndPress(state);
            if (Live(state.Captor) is { } captor)
            {
                Cancel(state, captor);
                if (Live(state.Captor) is { } still)
                    SetCapturing(still, false);
                state.Captor = null;
            }
            state.Buttons = 0;
            state.Gesture = default;
            if (state.Pointer.Kind != PointerKind.Mouse)
            {
                state.InContact = false;
                UpdateHover(state);
                Forget(state);
            }
        }
        _dpadHeld = null;
        if (_spacePressed is { } pressed)
        {
            _spacePressed = null;
            SetPressed(pressed, -1);
        }
        _heldKeys.Clear();
        while (_keyTargets.Count > 0)
        {
            var held = _keyTargets[_keyTargets.Count - 1];
            _keyTargets.Truncate(_keyTargets.Count - 1);
            if (!held.Target.IsMounted)
                continue;
            var e = new KeyEvent(held.Key, ModifierKeys.None, false);
            BubbleKey(held.Target, ref e, down: false);
        }

        _windowInactive = true;
        if (_focused is { } lost)
        {
            ShowFocus(lost, on: false, visible: false, keep: null);
            if (lost.Interaction?.Props.OnFocusChange is { IsEmpty: false } handler)
                Invoke(handler, new FocusChange(false, FocusCause.Code));
        }
    }

    void CountFocusMove()
    {
        if (_focusNotifying == 0)
            _focusMoves = 0;
        if (++_focusMoves > MaxFocusMoves)
            throw new InvalidOperationException(
                "Focus handlers moved the focus more than 64 times in one settle."
            );
    }

    void LetGoOfPressFocus()
    {
        if (
            Context.PressKeepsFocus
            || _focused is not { } focused
            || focused.Kind == NodeKind.EditableText
            || focused.Interaction?.Props.IsPressable != true
            || InFocusScope(focused)
        )
            return;
        MoveFocus(null, FocusCause.Pointer, visible: false);
    }

    internal void QueueAutoFocus(Node node)
    {
        for (var i = 0; i < _autoFocus.Count; i++)
        {
            if (_autoFocus[i].Node == node && _autoFocus[i].Generation == node.Generation)
                return;
        }
        _autoFocus.Add(new NodeEntry(node, node.Generation));
    }

    void RunAutoFocus()
    {
        var focusedOne = false;
        for (var i = 0; i < _autoFocus.Count; )
        {
            var node = _autoFocus[i].Node;
            if (
                !node.IsMounted
                || node.Generation != _autoFocus[i].Generation
                || node.Interaction?.Props.AutoFocus != true
                || DensityAt(node) == Density.Touch
            )
            {
                _autoFocus.RemoveAt(i);
                continue;
            }
            if (!CanFocus(node))
            {
                i++;
                continue;
            }
            _autoFocus.RemoveAt(i);
            if (focusedOne || _focused == node)
                continue;
            focusedOne = true;
            if (node.Payload is EditorPayload { Props.KeepSelectionOnFocus: true } editor)
                editor.CaretToEnd(this);
            Focus(node, FocusCause.Code, visible: false);
        }
    }

    internal void InteractionChanged(Node node, in InteractionProps old, in InteractionProps now)
    {
        if (now.AutoFocus && !old.AutoFocus)
            QueueAutoFocus(node);
        if (now.Action != old.Action && now.Action.Name is not null)
            Register(ref _actionBoxes, node);
        if (now.ExcludeSystemGestures && !old.ExcludeSystemGestures)
            Register(ref _gestureNodes, node);
    }

    internal void ShownAgain(Node node)
    {
        if (node.Interaction?.Props.AutoFocus == true)
            QueueAutoFocus(node);
        if (node._parent is { Kind: NodeKind.Component, Host: { } host } && host.Root == node)
            QueueOwnAutoFocus(node);
        WakeResting();
    }

    // A component's root showing again is the component showing again, but not the components inside it.
    void QueueOwnAutoFocus(Node within)
    {
        for (var child = within._first; child is not null; child = child._next)
        {
            if (child.Kind == NodeKind.Component)
                continue;
            if (child.Interaction?.Props.AutoFocus == true)
                QueueAutoFocus(child);
            QueueOwnAutoFocus(child);
        }
    }

    static void Register(ref ValueList<NodeEntry> list, Node node)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var entry = list[i];
            if (entry.Node == node && entry.Generation == node.Generation)
                return;
            if (!entry.Node.IsMounted || entry.Node.Generation != entry.Generation)
                list.RemoveAt(i);
        }
        list.Add(new NodeEntry(node, node.Generation));
    }

    readonly record struct NodeEntry(Node Node, int Generation);
}
