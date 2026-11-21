using System.Numerics;

namespace Tralys.Ui;

public sealed partial class UiView
{
    readonly List<PointerState> _pointers = [];
    readonly Stack<PointerState> _spareStates = new();
    ValueList<Notice> _notices;
    ValueList<Node> _scratchChain;
    ValueList<KeyTarget> _keyTargets;
    ValueList<Key> _heldKeys;
    ValueList<Node> _landed;
    Node? _focused;
    int _focusedGeneration;
    ValueList<Node> _focusAncestors;
    ValueList<int> _focusAncestorGenerations;
    bool _focusLost;
    bool _keyboardLast;
    Node? _spacePressed;
    Cursor? _cursor;

    /// <summary>The focused node, if any.</summary>
    public Node? Focused => _focused;

    /// <summary>A press landing at <paramref name="at"/>, in view pixels.</summary>
    /// <returns>Whether the UI took it.</returns>
    public bool PointerDown(
        Pointer p,
        Vector2 at,
        MouseButton b,
        ModifierKeys m,
        TimeSpan t,
        int clicks = 1
    )
    {
        BeforeInput();
        if (p.Kind != PointerKind.Mouse)
            b = MouseButton.Left;
        var state = StateOf(p);
        var first = (state.Buttons & ~state.DismissedButtons) == 0;
        state.Position = at / _scale;
        _pointerAt = state.Position;
        state.Inside = true;
        state.Modifiers = m;
        if (p.Kind != PointerKind.Mouse)
        {
            if (!state.InContact && clicks == 1)
                clicks = CountTap(state, t);
            state.InContact = true;
        }
        state.Buttons |= 1 << (int)b;
        _keyboardLast = false;
        UseDevice(DeviceOf(p));
        HideFocusRing();
        if (DragDropDrives(state))
            return true;
        var primary = b == MouseButton.Left || p.Kind != PointerKind.Mouse;

        var hit = HitAt(state.Position);
        if (DismissOnPress(state, b, hit))
        {
            UpdateHover(state);
            return true;
        }
        var target = Live(state.Captor) ?? hit;
        if (target is null)
        {
            if (primary)
                MoveFocus(null, FocusCause.Pointer, visible: false);
            UpdateHover(state);
            return false;
        }
        if ((target._states & InputStates.Disabled) != 0)
        {
            UpdateHover(state);
            return !IsViewRoot(target);
        }
        if (primary)
            ArmPan(p, hit ?? target, state.Position, t);
        if (StopCoasts(target))
        {
            UpdateHover(state);
            return true;
        }

        var e = new PointerEvent(PointerPhase.Down, p, b, m, t, state.Position, clicks)
        {
            Buttons = HeldButtons(state),
            IsInContact = InContact(state),
        };
        var focusBefore = _focused;
        RecordLanded(target);
        Preview(state, target, ref e);
        Bubble(state, target, ref e, PointerPhase.Down);
        var taken = e.Stopped || e.Prevented || !IsViewRoot(target);
        if (!e.Prevented)
        {
            if (first)
                BeginGesture(state, target, primary, b);
            if (primary)
            {
                taken |= BeginPress(state, target, b);
                if (_focused == focusBefore)
                    taken |= FocusOnPress();
                if (clicks == 2)
                    taken |= DoubleClick(target);
            }
        }
        UpdateHover(state);
        return taken;
    }

    /// <summary>A pointer moving to <paramref name="at"/>, in view pixels, holding <paramref name="buttons"/>, and
    /// for a touch or pen touching while <paramref name="inContact"/>. A held button or contact gone missing ends its
    /// press, drag or pan without a click, as its lost release would have.</summary>
    /// <returns>Whether the UI took it.</returns>
    public bool PointerMove(
        Pointer p,
        Vector2 at,
        MouseButtons buttons,
        bool inContact,
        ModifierKeys m,
        TimeSpan t
    )
    {
        BeforeInput();
        PointerState state;
        if (p.Kind == PointerKind.Touch)
        {
            if (FindState(p) is not { InContact: true } touch)
                return false;
            if (!inContact)
            {
                LoseContact(touch);
                return false;
            }
            state = touch;
        }
        else
        {
            state = StateOf(p);
            if (p.Kind == PointerKind.Pen)
            {
                if (state.InContact && !inContact)
                    LoseContact(state);
            }
            else if ((state.Buttons & ~(int)buttons) is not 0 and var lost)
                LoseButtons(state, lost);
            UseDevice(InputDevice.KeyboardMouse);
        }
        state.Position = at / _scale;
        _pointerAt = state.Position;
        state.Inside = true;
        state.Modifiers = m;

        var held = state.Pressing && state.Press.Hold == HoldPhase.Started;
        if (state.Pressing)
        {
            if (
                p.Kind != PointerKind.Mouse
                && Vector2.Distance(state.Position, state.Press.Start) > GestureSlop.Tap
            )
            {
                state.Press.Strayed = true;
                state.Press.Hold = HoldPhase.None;
            }
            UpdatePressed(state);
        }

        if (DragDropDrives(state) || MoveGesture(state, held))
        {
            DragDropMove(state);
            UpdateHover(state);
            return true;
        }
        if (PanMoved(state, t))
        {
            UpdateHover(state);
            return true;
        }

        var target = Live(state.Captor) ?? HitAt(state.Position);
        var taken = false;
        if (target is not null && (target._states & InputStates.Disabled) == 0)
        {
            var e = new PointerEvent(PointerPhase.Move, p, default, m, t, state.Position, 0)
            {
                Buttons = HeldButtons(state),
                IsInContact = InContact(state),
            };
            Bubble(state, target, ref e, PointerPhase.Move);
            taken = e.Stopped || e.Prevented;
        }
        UpdateHover(state);
        return taken;
    }

    /// <summary>A press lifting at <paramref name="at"/>, in view pixels.</summary>
    /// <returns>Whether the UI took it.</returns>
    public bool PointerUp(Pointer p, Vector2 at, MouseButton b, ModifierKeys m, TimeSpan t)
    {
        BeforeInput();
        if (p.Kind != PointerKind.Mouse)
            b = MouseButton.Left;
        var state = FindState(p);
        var bit = 1 << (int)b;
        if (state is null || (state.Buttons & bit) == 0)
            return false;
        state.Position = at / _scale;
        state.Buttons &= ~bit;
        state.Modifiers = m;

        var taken = false;
        var dismissed = ReleaseDismissed(state, b);
        if (DragDropDrives(state))
        {
            if (state.Buttons == 0)
                DragDropDrop();
            taken = true;
        }
        else if (dismissed)
            taken = true;
        else
        {
            var gestured =
                b == MouseButton.Left && PanClaimed(p)
                || state.Gesture.Dragging is not null && state.Gesture.Button == b;
            EndGestureDrag(state, b);
            var hit = HitAt(state.Position);
            var target = Live(state.Captor) ?? hit;
            if (target is not null && (target._states & InputStates.Disabled) == 0)
            {
                var e = new PointerEvent(PointerPhase.Up, p, b, m, t, state.Position, 0)
                {
                    Buttons = HeldButtons(state),
                    IsInContact = p.Kind == PointerKind.Mouse && state.Buttons != 0,
                };
                Bubble(state, target, ref e, PointerPhase.Up);
                taken = e.Stopped || e.Prevented;
                if (!e.Prevented)
                {
                    if (HeldLift(state, b, hit, out var ran))
                        taken |= ran;
                    else
                        taken |= Release(state, b, hit, overClicks: !gestured);
                    if (b == MouseButton.Right && !e.Stopped)
                        taken |= ContextMenu(state, target);
                }
            }
            else if (target is not null)
                taken = !IsViewRoot(target);
        }
        if (b == MouseButton.Left && EndPan(p, t, fling: true))
            taken = true;

        var released = state.Pressing && state.Press.Button == b;
        if (released)
            EndPress(state);
        if (state.Buttons == 0)
            state.Gesture = default;
        if (p.Kind != PointerKind.Mouse || state.Buttons == 0)
            SetCaptor(state, null);
        if (released)
            LetGoOfPressFocus();
        if (p.Kind != PointerKind.Mouse && state.Buttons == 0)
        {
            state.InContact = false;
            UpdateHover(state);
            Forget(state);
            return taken;
        }
        UpdateHover(state);
        return taken;
    }

    /// <summary>Cancels what <paramref name="p"/> was doing: no release, no click.</summary>
    public void PointerCancel(Pointer p)
    {
        BeforeInput();
        if (FindState(p) is not { } state)
            return;
        if (DragDropDrives(state))
            CancelDragDrop();
        CancelGestureDrag(state);
        EndPan(p, default, fling: false);
        var target = Live(state.Captor) ?? (state.Pressing ? Live(state.Press.Target) : null);
        if (target is not null)
            Cancel(state, target);
        var pressed = state.Pressing;
        if (pressed)
            EndPress(state);
        if (Live(state.Captor) is { } captor)
            SetCapturing(captor, false);
        state.Captor = null;
        state.Buttons = 0;
        state.Gesture = default;
        state.InContact = false;
        state.Inside = false;
        UpdateHover(state);
        if (p.Kind != PointerKind.Mouse)
            Forget(state);
        if (pressed)
            LetGoOfPressFocus();
    }

    /// <summary>The pointer left the view: nothing is hovered, and a capture is kept.</summary>
    public void PointerLeave(Pointer p)
    {
        BeforeInput();
        if (FindState(p) is not { } state)
            return;
        state.Inside = false;
        UpdateHover(state);
    }

    /// <summary>A key going down, sent to the focused node and its ancestors.</summary>
    /// <returns>Whether the UI took it.</returns>
    public bool KeyDown(Key k, ModifierKeys m, bool repeat)
    {
        BeforeInput();
        UseDevice(InputDevice.KeyboardMouse);
        _keyboardLast = true;
        if (!IsKeyDown(k))
            _heldKeys.Add(k);
        if (k == Key.Escape && _dragDrop.Active)
        {
            CancelDragDrop();
            return true;
        }
        var target = KeyReceiver();
        if (target is null)
            return false;
        if (!repeat)
            KeyWentTo(k, target);
        var e = new KeyEvent(k, m, repeat);
        BubbleKey(target, ref e, down: true);
        _textBlocked = e.Prevented;
        if (e.Prevented || e.Typed)
            return true;
        if (PressFocused(k, m, repeat))
            return true;
        return AfterKeyDown(k, m, repeat) || e.Stopped;
    }

    bool PressFocused(Key k, ModifierKeys m, bool repeat, bool pad = false)
    {
        if (
            m != ModifierKeys.None
            || _windowInactive
            || _focused is not { } focused
            || focused.Interaction?.Props.IsPressable != true
        )
            return false;
        if (k == Key.Enter)
        {
            if (
                pad
                    ? !focused.Interaction.Props.SpacePresses
                    : !focused.Interaction.Props.EnterPresses
            )
                return false;
            Click(focused);
            return true;
        }
        if (k == Key.Space)
        {
            if (!focused.Interaction.Props.SpacePresses)
                return false;
            if (!repeat && _spacePressed is null)
            {
                _spacePressed = focused;
                SetPressed(focused, 1);
            }
            return _spacePressed == focused;
        }
        return false;
    }

    /// <summary>Whether <paramref name="key"/> is held: it went down and hasn't come up, and the window hasn't lost
    /// the keyboard focus since, whichever node it went to or none. Untracked: read it as input arrives.</summary>
    public bool IsKeyDown(Key key)
    {
        for (var i = 0; i < _heldKeys.Count; i++)
        {
            if (_heldKeys[i] == key)
                return true;
        }
        return false;
    }

    /// <summary>A key coming up, sent where its key-down went.</summary>
    /// <returns>Whether the UI took it.</returns>
    public bool KeyUp(Key k, ModifierKeys m)
    {
        BeforeInput();
        _textBlocked = false;
        for (var i = 0; i < _heldKeys.Count; i++)
        {
            if (_heldKeys[i] == k)
            {
                _heldKeys.RemoveAtSwap(i);
                break;
            }
        }
        var target = TakeKeyTarget(k);
        var taken = false;
        if (target is { IsMounted: true })
        {
            var e = new KeyEvent(k, m, false);
            BubbleKey(target, ref e, down: false);
            taken = e.Stopped || e.Prevented;
        }
        if (k == Key.Space && _spacePressed is { } pressed)
        {
            _spacePressed = null;
            SetPressed(pressed, -1);
            if (pressed.IsMounted && pressed == _focused)
                Click(pressed);
            taken = true;
        }
        return taken;
    }

    Node? ContentRoot()
    {
        if (KeyScopeRoot() is { } scope)
            return scope;
        for (var child = _root._first; child is not null; child = child._next)
        {
            if (child.Host?.Root is { } root)
                return root;
        }
        return null;
    }

    void KeyWentTo(Key key, Node target)
    {
        for (var i = 0; i < _keyTargets.Count; i++)
        {
            if (_keyTargets[i].Key == key)
            {
                _keyTargets[i] = new KeyTarget(key, target);
                return;
            }
        }
        _keyTargets.Add(new KeyTarget(key, target));
    }

    Node? TakeKeyTarget(Key key)
    {
        for (var i = 0; i < _keyTargets.Count; i++)
        {
            if (_keyTargets[i].Key != key)
                continue;
            var target = _keyTargets[i].Target;
            _keyTargets.RemoveAtSwap(i);
            return target;
        }
        return null;
    }

    void BubbleKey(Node target, ref KeyEvent e, bool down)
    {
        for (var node = target; node is not null && !e.Stopped; node = KeyParent(node, e.Key))
        {
            if (node.Interaction is not { } interaction)
                continue;
            var handler = down ? interaction.Props.OnKeyDown : interaction.Props.OnKeyUp;
            if (handler.IsEmpty)
                continue;
            try
            {
                handler.Invoke(ref e);
            }
            catch (Exception fault) when (Context.OnFault is not null)
            {
                Context.OnFault(fault);
            }
        }
    }

    internal bool Focus(Node node, FocusCause cause, bool visible)
    {
        VerifyAccess();
        if (!CanFocus(node))
            return false;
        if (cause == FocusCause.Code && DeferFocus(node))
            return true;
        MoveFocus(node, cause, visible || (cause == FocusCause.Code && _keyboardLast));
        return true;
    }

    bool CanFocus(Node node)
    {
        if (!node.IsMounted || node.View != this || !node.IsFocusable)
            return false;
        if ((node._states & InputStates.Disabled) != 0)
            return false;
        return Drawn(node) && !InExiting(node);
    }

    void MoveFocus(Node? next, FocusCause cause, bool visible)
    {
        var old = _focused;
        if (next is not null && cause is FocusCause.Tab or FocusCause.Arrow)
            RequestReveal(next, ScrollAlign.Nearest, animate: false);
        if (old == next)
        {
            _focusVisible = visible;
            if (next is not null && !_windowInactive)
                SetFocusVisible(next, visible);
            return;
        }
        if (next is not null)
            RememberFocusInGroups(next);
        var shows = !_windowInactive;
        if (shows)
        {
            CountFocusMove();
            if (old is not null)
                ShowFocus(old, on: false, visible: false, keep: next);
        }

        _focused = next;
        _focusVisible = visible;
        _focusLost = false;
        _focusAncestors.Clear();
        _focusAncestorGenerations.Clear();
        if (next is not null)
        {
            _focusedGeneration = next.Generation;
            for (var a = next._parent; a is not null; a = a._parent)
            {
                if (a.IsFocusable)
                {
                    _focusAncestors.Add(a);
                    _focusAncestorGenerations.Add(a.Generation);
                }
            }
        }
        EditStateChanged();
        if (!shows)
        {
            FocusMoved();
            return;
        }
        if (next is not null)
        {
            ShowFocus(next, on: true, visible, keep: null);
            FocusedWithinMoved(next);
        }

        _focusNotifying++;
        try
        {
            if (old?.Interaction?.Props.OnFocusChange is { IsEmpty: false } lost)
                Invoke(lost, new FocusChange(false, cause));
            if (
                next is not null
                && _focused == next
                && next.Interaction?.Props.OnFocusChange is { IsEmpty: false } gained
            )
                Invoke(gained, new FocusChange(true, cause));
        }
        finally
        {
            _focusNotifying--;
        }
        FocusMoved();
    }

    static void ShowFocus(Node node, bool on, bool visible, Node? keep)
    {
        node.SetStates(
            on
                ? node._states | InputStates.Focused | (visible ? InputStates.FocusVisible : 0)
                : node._states & ~(InputStates.Focused | InputStates.FocusVisible)
        );
        for (var a = node._parent; a is not null; a = a._parent)
        {
            if (!on && keep is not null && a.IsAncestorOrSelfOf(keep))
            {
                if (a == keep)
                    a.SetStates(a._states & ~InputStates.FocusWithin);
                break;
            }
            a.SetStates(
                on ? a._states | InputStates.FocusWithin : a._states & ~InputStates.FocusWithin
            );
        }
    }

    static void SetFocusVisible(Node node, bool visible)
    {
        var states = visible
            ? node._states | InputStates.FocusVisible
            : node._states & ~InputStates.FocusVisible;
        node.SetStates(states);
    }

    bool FocusOnPress()
    {
        for (var i = 0; i < _landed.Count; i++)
        {
            var node = _landed[i];
            if (!node.IsMounted)
                continue;
            if (node.IsFocusable)
            {
                if (!CanFocus(node))
                    return false;
                MoveFocus(node, FocusCause.Pointer, visible: false);
                return true;
            }
            if (node.Interaction?.Props.IsPressable == true)
                return false;
        }
        if (_landed.Count > 0 && InFocusScope(_landed[0]))
            return false;
        MoveFocus(null, FocusCause.Pointer, visible: false);
        return false;
    }

    // The mounted component's root stands in for the view root: a press on it alone is left to the scene.
    bool IsViewRoot(Node node)
    {
        if (node.Kind == NodeKind.Layer)
            return false;
        for (var p = node._parent; p is not null; p = p._parent)
        {
            if (p == _root)
                return true;
            if (!p.IsTransparent || (p.Host is { } host && host.Root != node))
                return false;
        }
        return node == _root;
    }

    void RecordLanded(Node target)
    {
        _landed.Clear();
        for (var node = target; node is not null; node = node._parent)
        {
            if (!node.IsTransparent)
                _landed.Add(node);
        }
    }

    void ValidateFocus()
    {
        if (
            _focused is { } focused
            && (focused.Generation != _focusedGeneration || !CanFocus(focused))
        )
            _focusLost = true;
        if (!_focusLost)
            return;
        _focusLost = false;
        if (ReturnDeparted())
            return;
        Node? fallback = null;
        for (var i = 0; i < _focusAncestors.Count; i++)
        {
            var candidate = _focusAncestors[i];
            if (candidate.Generation == _focusAncestorGenerations[i] && CanFocus(candidate))
            {
                fallback = candidate;
                break;
            }
        }
        if (_focused is { } stale && (stale.Generation != _focusedGeneration || !stale.IsMounted))
            _focused = null;
        MoveFocus(fallback, FocusCause.Code, visible: false);
    }

    bool BeginPress(PointerState state, Node target, MouseButton button)
    {
        var owner = PressableOf(target);
        if (owner is not null && PressedByAnother(state, owner))
            owner = null;
        if (state.Pressing)
            EndPress(state);
        var press = state.Press;
        state.Pressing = true;
        press.Target = target;
        press.Owner = owner;
        press.Start = state.Position;
        press.Button = button;
        press.Strayed = false;
        press.Hold = state.Pointer.Kind == PointerKind.Mouse ? HoldPhase.None : HoldPhase.Waiting;
        press.HoldSince = Context.Now;
        press.Chain.Clear();
        press.Pressed.Clear();
        for (var n = target; n is not null; n = n._parent)
        {
            if (n.IsTransparent)
                continue;
            press.Chain.Add(n);
            press.Pressed.Add(false);
        }
        UpdatePressed(state);

        if (owner is null)
            return false;
        switch (ModeOf(owner, state))
        {
            case PressMode.Press:
                Click(owner);
                break;
            case PressMode.Release when state.Captor is null:
                SetCaptor(state, owner);
                break;
        }
        return true;
    }

    static PressMode ModeOf(Node owner, PointerState state) =>
        owner.Interaction!.Props.PressMode switch
        {
            PressMode.PressOrTap when state.Pointer.Kind == PointerKind.Mouse => PressMode.Press,
            PressMode.PressOrTap => PressMode.Release,
            var mode => mode,
        };

    static Node? PressableOf(Node target)
    {
        for (var n = target; n is not null; n = n._parent)
        {
            if (n.Interaction?.Props.IsPressable == true)
                return n;
        }
        return null;
    }

    void UpdatePressed(PointerState state)
    {
        if (!state.Pressing)
            return;
        var press = state.Press;
        var over = HitAt(state.Position);
        for (var i = 0; i < press.Chain.Count; i++)
        {
            var node = press.Chain[i];
            var now = node.IsMounted && node.IsAncestorOrSelfOf(over);
            if (now == press.Pressed[i])
                continue;
            press.Pressed[i] = now;
            SetPressed(node, now ? 1 : -1);
        }
    }

    static void SetPressed(Node node, int delta)
    {
        node.PressCount = (byte)Math.Max(0, node.PressCount + delta);
        var states =
            node.PressCount > 0
                ? node._states | InputStates.Pressed
                : node._states & ~InputStates.Pressed;
        node.SetStates(states);
    }

    void EndPress(PointerState state)
    {
        if (!state.Pressing)
            return;
        var press = state.Press;
        for (var i = 0; i < press.Chain.Count; i++)
        {
            if (press.Pressed[i])
                SetPressed(press.Chain[i], -1);
        }
        press.Chain.Clear();
        press.Pressed.Clear();
        press.Owner = press.Target = null;
        state.Pressing = false;
    }

    bool Release(PointerState state, MouseButton button, Node? hit, bool overClicks)
    {
        var clicked = false;
        if (state.Pressing && state.Press is { Owner: { } owner } press && press.Button == button)
        {
            var mode = ModeOf(owner, state);
            var tapped = state.Pointer.Kind == PointerKind.Mouse || !press.Strayed;
            if (
                mode == PressMode.Release
                && tapped
                && owner.IsMounted
                && owner.IsAncestorOrSelfOf(hit)
            )
            {
                Click(owner);
                clicked = true;
            }
        }
        if (overClicks && (button == MouseButton.Left || state.Pointer.Kind != PointerKind.Mouse))
        {
            var over = hit is null ? null : PressableOf(hit);
            if (
                over is not null
                && over.Interaction!.Props.PressMode == PressMode.ReleaseOver
                && (over._states & InputStates.Disabled) == 0
            )
            {
                Click(over);
                clicked = true;
            }
        }
        return clicked;
    }

    void Click(Node node)
    {
        if (node.Interaction is not { } interaction || (node._states & InputStates.Disabled) != 0)
            return;
        try
        {
            interaction.Props.OnClick.Invoke();
        }
        catch (Exception fault) when (Context.OnFault is not null)
        {
            Context.OnFault(fault);
        }
    }

    void Invoke<T>(Callback<T> callback, T value)
    {
        try
        {
            callback.Invoke(value);
        }
        catch (Exception fault) when (Context.OnFault is not null)
        {
            Context.OnFault(fault);
        }
    }

    ValueList<Node> _previewChain;

    void Bubble(PointerState state, Node target, ref PointerEvent e, PointerPhase phase)
    {
        for (var node = target; node is not null && !e.Stopped; node = node._parent)
        {
            if (node.Interaction is not { } interaction)
                continue;
            var handler = phase switch
            {
                PointerPhase.Down => interaction.Props.OnPointerDown,
                PointerPhase.Move or PointerPhase.Still => interaction.Props.OnPointerMove,
                PointerPhase.Up => interaction.Props.OnPointerUp,
                _ => interaction.Props.OnPointerCancel,
            };
            if (!handler.IsEmpty)
                Handle(state, node, handler, ref e);
        }
    }

    void Preview(PointerState state, Node target, ref PointerEvent e)
    {
        var start = _previewChain.Count;
        for (var node = target; node is not null; node = node._parent)
        {
            if (node.Interaction is { Props.OnPreviewPointerDown.IsEmpty: false })
                _previewChain.Add(node);
        }
        for (var i = _previewChain.Count - 1; i >= start && !e.Stopped; i--)
        {
            var node = _previewChain[i];
            if (node.Interaction is { Props.OnPreviewPointerDown: { IsEmpty: false } handler })
                Handle(state, node, handler, ref e);
        }
        _previewChain.Truncate(start);
    }

    void Handle(PointerState state, Node node, Handler<PointerEvent> handler, ref PointerEvent e)
    {
        AimAt(ref e, node);
        try
        {
            handler.Invoke(ref e);
        }
        catch (Exception fault) when (Context.OnFault is not null)
        {
            Context.OnFault(fault);
        }
        if (e.CaptureRequest is { } captor)
        {
            e.CaptureRequest = null;
            SetCaptor(state, captor);
            PanCaptured(state.Pointer);
        }
        if (e.ReleaseRequest)
        {
            e.ReleaseRequest = false;
            SetCaptor(state, null);
        }
    }

    static void AimAt(ref PointerEvent e, Node node)
    {
        e.Position = LocalPosition(node, e.ViewPosition);
        e.Size = FrameSize(node);
        e.Handler = node;
    }

    internal void PressOn(Node node, Handler<PointerEvent> handler, ref PointerEvent e)
    {
        if (
            e.Phase != PointerPhase.Down
            || !node.IsMounted
            || node.View != this
            || (node._states & InputStates.Disabled) != 0
            || !Drawn(node)
        )
            return;
        var from = e.Handler;
        var position = e.Position;
        var size = e.Size;
        RecordLanded(node);
        AimAt(ref e, node);
        handler.Invoke(ref e);
        e.Handler = from;
        e.Position = position;
        e.Size = size;
    }

    void Cancel(PointerState state, Node target)
    {
        var e = new PointerEvent(
            PointerPhase.Cancel,
            state.Pointer,
            default,
            default,
            Context.Now,
            state.Position,
            0
        );
        Bubble(state, target, ref e, PointerPhase.Cancel);
    }

    void SetCaptor(PointerState state, Node? captor)
    {
        if (captor is not null && ((captor._states & InputStates.Disabled) != 0 || !Drawn(captor)))
            return;
        var old = Live(state.Captor);
        if (old == captor)
            return;
        if (captor is not null)
            CaptureTaken(state, captor);
        if (old is not null)
            SetCapturing(old, false);
        state.Captor = captor;
        if (captor is not null)
            SetCapturing(captor, true);
        if (old is not null && captor is not null)
            Cancel(state, old);
        UpdateHover(state);
    }

    static void SetCapturing(Node captor, bool on)
    {
        for (var n = captor; n is not null; n = n._parent)
            n.SetStates(
                on ? n._states | InputStates.Capturing : n._states & ~InputStates.Capturing
            );
    }

    static Node? Live(Node? node) => node is { IsMounted: true } ? node : null;

    void UpdateHover(PointerState state)
    {
        var hovers = state.Pointer.Kind == PointerKind.Mouse ? state.Inside : state.InContact;
        var deepest = hovers ? HitAt(state.Position) : null;
        var captor = Live(state.Captor);
        if (captor is not null && deepest is not null && !captor.IsAncestorOrSelfOf(deepest))
            deepest = null;

        ref var chain = ref _scratchChain;
        chain.Clear();
        for (var n = deepest; n is not null; n = n._parent)
        {
            if (captor is not null && !captor.IsAncestorOrSelfOf(n))
                break;
            if (!n.IsTransparent && (n._states & InputStates.Disabled) == 0)
                chain.Add(n);
        }

        var old = state.Hover;
        var exits = _notices.Count;
        for (var i = 0; i < old.Count; i++)
        {
            var node = old[i];
            if (chain.IndexOf(node) < 0)
                Unhover(node, notify: true);
        }
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var node = chain[i];
            if (old.IndexOf(node) >= 0)
                continue;
            node.HoverCount++;
            if (node.HoverCount == 1)
            {
                node.SetStates(node._states | InputStates.Hovered);
                if (node.Interaction?.Props.OnHoverChange is { IsEmpty: false })
                    _notices.Add(
                        new Notice(NoticeKind.Entered, node, node.Generation, state.Pointer)
                    );
            }
        }
        state.Hover.Clear();
        for (var i = 0; i < chain.Count; i++)
            state.Hover.Add(chain[i]);
        DeliverHover(exits);
        if (state.Pointer.Kind == PointerKind.Mouse)
            UpdateCursor(state);
    }

    void Unhover(Node node, bool notify)
    {
        if (node.HoverCount == 0)
            return;
        node.HoverCount--;
        if (node.HoverCount > 0)
            return;
        node.SetStates(node._states & ~InputStates.Hovered);
        if (notify && node.Interaction?.Props.OnHoverChange is { IsEmpty: false })
            _notices.Add(new Notice(NoticeKind.Exited, node, node.Generation, default));
    }

    // Hover changes of live nodes are told at once, exits before enters.
    void DeliverHover(int from)
    {
        if (_notices.Count == from)
            return;
        for (var pass = 0; pass < 2; pass++)
        {
            var kind = pass == 0 ? NoticeKind.Exited : NoticeKind.Entered;
            for (var i = from; i < _notices.Count; i++)
            {
                var notice = _notices[i];
                if (notice.Kind == kind && notice.Node.Generation == notice.Generation)
                    Invoke(
                        notice.Node.Interaction!.Props.OnHoverChange,
                        kind == NoticeKind.Entered
                    );
            }
        }
        _notices.Truncate(from);
    }

    void UpdateCursor(PointerState state)
    {
        Cursor? cursor = null;
        if (state.Inside)
        {
            var from = Live(state.Captor) ?? (state.Hover.Count > 0 ? state.Hover[0] : null);
            for (var n = from; n is not null && cursor is null; n = n._parent)
                cursor = n.Interaction?.Props.Cursor;
            if (MousePanning())
                cursor = Cursors.ScrollAll;
        }
        if (cursor == _cursor)
            return;
        _cursor = cursor;
        _host.SetCursor(cursor);
    }

    void Departed(Node node)
    {
        for (var p = 0; p < _pointers.Count; p++)
        {
            var state = _pointers[p];
            if (state.Captor is { } captor && node.IsAncestorOrSelfOf(captor))
            {
                SetCapturing(captor, false);
                state.Captor = null;
                _notices.Add(
                    new Notice(
                        NoticeKind.CaptureLost,
                        captor,
                        captor.Generation,
                        state.Pointer,
                        state.Position
                    )
                );
            }
            if (
                state.Pressing
                && state.Press.Target is { } target
                && node.IsAncestorOrSelfOf(target)
            )
                EndPress(state);
            for (var i = state.Hover.Count - 1; i >= 0; i--)
            {
                var hovered = state.Hover[i];
                if (!node.IsAncestorOrSelfOf(hovered))
                    continue;
                Unhover(hovered, notify: true);
                state.Hover.RemoveAt(i);
            }
        }
        if (_spacePressed is { } pressed && node.IsAncestorOrSelfOf(pressed))
        {
            SetPressed(pressed, -1);
            _spacePressed = null;
        }
        if (_focused is { } focused && node.IsAncestorOrSelfOf(focused))
        {
            FocusDeparting(node, focused);
            focused.SetStates(focused._states & ~(InputStates.Focused | InputStates.FocusVisible));
            for (var a = focused._parent; a is not null; a = a._parent)
                a.SetStates(a._states & ~InputStates.FocusWithin);
            if (!_windowInactive && focused.Interaction?.Props.OnFocusChange is { IsEmpty: false })
                _notices.Add(
                    new Notice(NoticeKind.FocusLost, focused, focused.Generation, default)
                );
            _focused = null;
            _focusLost = true;
        }
        DepartedGestures(node);
    }

    void DeliverNotices()
    {
        DeliverEditorNotices();
        if (_notices.Count == 0)
            return;
        for (var kind = NoticeKind.CaptureLost; kind <= NoticeKind.DragLeft; kind++)
        {
            for (var i = 0; i < _notices.Count; i++)
            {
                var notice = _notices[i];
                if (notice.Kind != kind || notice.Node.Generation != notice.Generation)
                    continue;
                var props = notice.Node.Interaction?.Props ?? default;
                switch (kind)
                {
                    case NoticeKind.CaptureLost when !props.OnPointerCancel.IsEmpty:
                        var e = new PointerEvent(
                            PointerPhase.Cancel,
                            notice.Pointer,
                            default,
                            default,
                            Context.Now,
                            notice.At,
                            0
                        );
                        AimAt(ref e, notice.Node);
                        try
                        {
                            props.OnPointerCancel.Invoke(ref e);
                        }
                        catch (Exception fault) when (Context.OnFault is not null)
                        {
                            Context.OnFault(fault);
                        }
                        break;
                    case NoticeKind.Exited:
                        Invoke(props.OnHoverChange, false);
                        break;
                    case NoticeKind.FocusLost:
                        Invoke(props.OnFocusChange, new FocusChange(false, FocusCause.Code));
                        break;
                    case NoticeKind.DragLeft:
                        Invoke(props.OnDragLeave);
                        break;
                }
            }
        }
        _notices.Clear();
    }

    void BeforeInput()
    {
        VerifyAccess();
        DeliverNotices();
        ValidateFocus();
        ValidateCaptures();
    }

    void ValidateCaptures()
    {
        for (var i = 0; i < _pointers.Count; i++)
        {
            var state = _pointers[i];
            if (
                state.Captor is { } captor
                && (
                    !captor.IsMounted
                    || (captor._states & InputStates.Disabled) != 0
                    || !Drawn(captor)
                )
            )
                SetCaptor(state, null);
            ValidateDrag(state);
        }
    }

    void InputStep()
    {
        FollowLayout();
        InputStepGestures();
        DeliverNotices();
    }

    void InputSettle()
    {
        FollowLayout();
        RunAutoFocus();
        EnterModals();
        DeliverNotices();
    }

    void FollowLayout()
    {
        DeliverNotices();
        ValidateFocus();
        ValidateCaptures();
        for (var i = 0; i < _pointers.Count; i++)
        {
            var state = _pointers[i];
            if (state.Pressing)
                UpdatePressed(state);
            UpdateHover(state);
        }
    }

    PointerState StateOf(Pointer pointer)
    {
        if (FindState(pointer) is { } found)
            return found;
        if (!_spareStates.TryPop(out var state))
        {
            PoolMisses.Add();
            state = new PointerState();
        }
        state.Pointer = pointer;
        _pointers.Add(state);
        return state;
    }

    PointerState? FindState(Pointer pointer)
    {
        for (var i = 0; i < _pointers.Count; i++)
        {
            if (_pointers[i].Pointer == pointer)
                return _pointers[i];
        }
        return null;
    }

    void Forget(PointerState state)
    {
        _pointers.Remove(state);
        state.Hover.Clear();
        state.Captor = null;
        EndPress(state);
        state.Buttons = 0;
        state.Gesture = default;
        state.InContact = state.Inside = false;
        _spareStates.Push(state);
    }

    sealed class PointerState
    {
        public Pointer Pointer;
        public Vector2 Position;
        public bool Inside;
        public bool InContact;
        public int Buttons;
        public int DismissedButtons;
        public Node? Captor;
        public bool Pressing;
        public readonly Press Press = new();
        public ValueList<Node> Hover;
        public ModifierKeys Modifiers;
        public GestureState Gesture;
    }

    sealed class Press
    {
        public Node? Target;
        public Node? Owner;
        public Vector2 Start;
        public MouseButton Button;
        public bool Strayed;
        public HoldPhase Hold;
        public TimeSpan HoldSince;
        public ValueList<Node> Chain;
        public ValueList<bool> Pressed;
    }

    enum NoticeKind : byte
    {
        CaptureLost,
        Exited,
        FocusLost,
        DragLeft,
        Entered,
    }

    readonly record struct Notice(
        NoticeKind Kind,
        Node Node,
        int Generation,
        Pointer Pointer,
        Vector2 At = default
    );

    readonly record struct KeyTarget(Key Key, Node Target);
}
