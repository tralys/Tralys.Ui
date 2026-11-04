using System.Numerics;

namespace Tralys.Ui;

public sealed partial class UiView
{
    /// <summary>How long a touch or pen stays still, within <see cref="GestureSlop.Tap"/>, before it holds: what a long
    /// press waits for, so a host's own long press beside the view waits as long.</summary>
    public static readonly TimeSpan HoldTime = TimeSpan.FromSeconds(0.5);
    static readonly TimeSpan DoubleTapTime = TimeSpan.FromSeconds(0.5);

    int _taps;
    PointerKind _tapKind;
    TimeSpan _tapTime;
    Vector2 _tapAt;

    enum HoldPhase : byte
    {
        None,
        Waiting,
        Started,
    }

    struct GestureState
    {
        public Vector2 DownAt;
        public Node? Draggable;
        public int DraggableGeneration;
        public object? Data;
        public DragEffect Effects;
        public Slot Preview;
        public Callback<DragEffect> OnDragEnd;
        public Node? Dragging;
        public int DraggingGeneration;
        public bool DraggingDeparted;
        public MouseButton Button;
        public Vector2 Last;
        public bool Held;
    }

    bool InputNeedsFrame
    {
        get
        {
            if (_dpadHeld is not null)
                return true;
            for (var i = 0; i < _pointers.Count; i++)
            {
                var state = _pointers[i];
                if (state.Pressing && state.Press.Hold == HoldPhase.Waiting)
                    return true;
            }
            return false;
        }
    }

    static MouseButtons HeldButtons(PointerState state) =>
        state.Pointer.Kind == PointerKind.Mouse ? (MouseButtons)state.Buttons
        : state.InContact ? MouseButtons.Left
        : MouseButtons.None;

    static bool InContact(PointerState state) =>
        state.Pointer.Kind == PointerKind.Mouse ? state.Buttons != 0 : state.InContact;

    static InputDevice DeviceOf(Pointer pointer) =>
        pointer.Kind == PointerKind.Touch ? InputDevice.Touch : InputDevice.KeyboardMouse;

    int CountTap(PointerState state, TimeSpan time)
    {
        if (state.Pointer.Kind == PointerKind.Touch && AnotherTouchDown(state))
            return 1;
        var again =
            _taps > 0
            && state.Pointer.Kind == _tapKind
            && time - _tapTime <= DoubleTapTime
            && Vector2.Distance(state.Position, _tapAt) <= GestureSlop.DoubleTap;
        _taps = again ? _taps + 1 : 1;
        _tapKind = state.Pointer.Kind;
        _tapTime = time;
        _tapAt = state.Position;
        return _taps;
    }

    bool PressedByAnother(PointerState state, Node owner)
    {
        for (var i = 0; i < _pointers.Count; i++)
        {
            var other = _pointers[i];
            if (other != state && other.Pressing && other.Press.Owner == owner)
                return true;
        }
        return false;
    }

    bool AnotherTouchDown(PointerState state)
    {
        for (var i = 0; i < _pointers.Count; i++)
        {
            var other = _pointers[i];
            if (other != state && other.Pointer.Kind == PointerKind.Touch && other.InContact)
                return true;
        }
        return false;
    }

    void HideFocusRing()
    {
        if (_focused is { } shown && (shown._states & InputStates.FocusVisible) != 0)
            shown.SetStates(shown._states & ~InputStates.FocusVisible);
        _focusVisible = false;
    }

    // Only a press can start an onDrag, while any button can carry dragData.
    void BeginGesture(PointerState state, Node target, bool primary, MouseButton button)
    {
        ref var g = ref state.Gesture;
        g = default;
        g.DownAt = state.Position;
        g.Button = button;
        for (var i = 0; i < _landed.Count; i++)
        {
            var node = _landed[i];
            if (node.Interaction is not { Props.Draggable: true } interaction)
                continue;
            ref readonly var props = ref interaction.Props;
            if (props.DragData is null && !primary)
                continue;
            g.Draggable = node;
            g.DraggableGeneration = node.Generation;
            g.Data = props.DragData;
            g.Effects = props.DragEffects;
            g.Preview = props.DragPreview;
            g.OnDragEnd = props.OnDragEnd;
            return;
        }
    }

    bool MoveGesture(PointerState state, bool wasHeld)
    {
        ref var g = ref state.Gesture;
        if (LiveDrag(ref g) is { } dragging)
        {
            var delta = state.Position - g.Last;
            g.Last = state.Position;
            DeliverDrag(dragging, DragPhase.Move, state, delta);
            return false;
        }
        if (g.Draggable is not { } node)
            return false;
        if (!DragThreshold.IsCrossed(state.Pointer.Kind, state.Position - g.DownAt))
            return false;
        g.Draggable = null;
        if (
            node.Generation != g.DraggableGeneration
            || !node.IsMounted
            || (node._states & InputStates.Disabled) != 0
            || !Drawn(node)
            || DragRunning()
        )
            return false;
        var captor = Live(state.Captor);
        if (
            captor is not null
            && captor != node
            && !(state.Pressing && captor == state.Press.Owner)
        )
            return false;

        g.Held = wasHeld;
        if (!node.Interaction!.Props.OnDrag.IsEmpty)
        {
            if (DeliverDrag(node, DragPhase.Start, state, default) || !node.IsMounted)
                return false;
        }

        if (g.Data is not null)
        {
            if ((state.Buttons & ~state.DismissedButtons) == 0)
                return false;
            if (state.Pressing)
                EndPress(state);
            if (Live(state.Captor) is { } held)
            {
                Cancel(state, held);
                if (Live(state.Captor) is { } still)
                    SetCapturing(still, false);
                state.Captor = null;
            }
            BeginDragDrop(state, node, g.Data, g.Effects, g.Preview, g.OnDragEnd, g.Button);
            return true;
        }

        if (state.Pressing)
            EndPress(state);
        g.Dragging = node;
        g.DraggingGeneration = node.Generation;
        g.DraggingDeparted = false;
        g.Last = state.Position;
        SetCaptor(state, node);
        PanCaptured(state.Pointer);
        if (LiveDrag(ref g) is { } started)
            DeliverDrag(started, DragPhase.Move, state, state.Position - g.DownAt);
        return false;
    }

    static Node? LiveDrag(ref GestureState g) =>
        g.Dragging is { IsMounted: true } node
        && node.Generation == g.DraggingGeneration
        && !g.DraggingDeparted
            ? node
            : null;

    void ValidateDrag(PointerState state)
    {
        ref var g = ref state.Gesture;
        if (g.Dragging is not { } node)
            return;
        if (node.Generation != g.DraggingGeneration)
        {
            g.Dragging = null;
            return;
        }
        if (!g.DraggingDeparted && (node._states & InputStates.Disabled) == 0 && Drawn(node))
            return;
        g.Dragging = null;
        g.DraggingDeparted = false;
        DeliverDrag(node, DragPhase.Cancel, state, default);
    }

    bool DragRunning()
    {
        if (_dragDrop.Active)
            return true;
        for (var i = 0; i < _pointers.Count; i++)
        {
            if (LiveDrag(ref _pointers[i].Gesture) is not null)
                return true;
        }
        return false;
    }

    bool DeliverDrag(Node node, DragPhase phase, PointerState state, Vector2 delta)
    {
        var handler = node.Interaction?.Props.OnDrag ?? default;
        if (handler.IsEmpty)
            return false;
        var e = new DragEvent(
            phase,
            state.Pointer,
            state.Modifiers,
            state.Position - state.Gesture.DownAt,
            delta,
            LocalPosition(node, state.Position),
            FrameSize(node)
        )
        {
            IsHeld = state.Gesture.Held,
            Button = state.Gesture.Button,
        };
        try
        {
            handler.Invoke(ref e);
        }
        catch (Exception fault) when (Context.OnFault is not null)
        {
            Context.OnFault(fault);
        }
        return e.Prevented;
    }

    void EndGestureDrag(PointerState state, MouseButton button)
    {
        ref var g = ref state.Gesture;
        if (g.Dragging is null || g.Button != button)
            return;
        var dragging = LiveDrag(ref g);
        g.Dragging = null;
        if (dragging is not null)
            DeliverDrag(dragging, DragPhase.End, state, state.Position - g.Last);
    }

    void CancelGestureDrag(PointerState state)
    {
        ref var g = ref state.Gesture;
        g.Draggable = null;
        if (g.Dragging is null)
            return;
        var dragging = LiveDrag(ref g);
        g.Dragging = null;
        if (dragging is not null)
            DeliverDrag(dragging, DragPhase.Cancel, state, default);
    }

    void CaptureTaken(PointerState state, Node captor)
    {
        if (state.Gesture.Dragging is { } dragging && dragging != captor)
            CancelGestureDrag(state);
    }

    // The buttons in lost came up without a release the view heard: their press, drag or pan ends without a click.
    void LoseButtons(PointerState state, int lost)
    {
        state.Buttons &= ~lost;
        if ((lost & (1 << (int)MouseButton.Left)) != 0)
            DropPan(state.Pointer);
        if (DragDropDrives(state) && state.Buttons == 0)
            CancelDragDrop();
        ref var g = ref state.Gesture;
        if (g.Dragging is not null && (lost & (1 << (int)g.Button)) != 0)
            CancelGestureDrag(state);
        var pressed = state.Pressing && (lost & (1 << (int)state.Press.Button)) != 0;
        if (pressed)
            EndPress(state);
        if (state.Buttons == 0)
        {
            g = default;
            if (Live(state.Captor) is { } captor)
            {
                Cancel(state, captor);
                if (Live(state.Captor) is { } still)
                    SetCapturing(still, false);
                state.Captor = null;
            }
        }
        if (pressed)
            LetGoOfPressFocus();
    }

    void LoseContact(PointerState state)
    {
        LoseButtons(state, state.Buttons);
        state.InContact = false;
        UpdateHover(state);
        if (state.Pointer.Kind == PointerKind.Touch)
            Forget(state);
    }

    bool HeldLift(PointerState state, MouseButton button, Node? hit, out bool ran)
    {
        ran = false;
        if (
            !state.Pressing
            || state.Press.Button != button
            || state.Press.Hold != HoldPhase.Started
        )
            return false;
        var press = state.Press;
        if (
            press.Owner is { IsMounted: true } owner
            && (owner._states & InputStates.Disabled) == 0
            && owner.IsAncestorOrSelfOf(hit)
            && owner.Interaction!.Props.OnLongPress is { IsEmpty: false } longPress
        )
        {
            Invoke(longPress);
            ran = true;
        }
        else
        {
            for (var i = 0; i < press.Chain.Count; i++)
            {
                var node = press.Chain[i];
                if (node.IsMounted && node.IsAncestorOrSelfOf(hit))
                {
                    ran = BubbleContextMenu(state, node, button);
                    break;
                }
            }
        }
        if (ran)
            _host.PerformHaptic(HapticFeedback.LongPress);
        return true;
    }

    bool ContextMenu(PointerState state, Node target) =>
        BubbleContextMenu(state, target, MouseButton.Right);

    bool BubbleContextMenu(PointerState state, Node from, MouseButton button)
    {
        var ran = false;
        var e = new PointerEvent(
            PointerPhase.Up,
            state.Pointer,
            button,
            state.Modifiers,
            Context.Now,
            state.Position,
            0
        )
        {
            Buttons = HeldButtons(state),
            IsInContact = InContact(state),
        };
        for (var node = from; node is not null && !e.Stopped; node = node._parent)
        {
            if (
                node.Interaction is not { } interaction
                || interaction.Props.OnContextMenu.IsEmpty
                || (node._states & InputStates.Disabled) != 0
            )
                continue;
            AimAt(ref e, node);
            ran = true;
            try
            {
                interaction.Props.OnContextMenu.Invoke(ref e);
            }
            catch (Exception fault) when (Context.OnFault is not null)
            {
                Context.OnFault(fault);
            }
        }
        return ran;
    }

    // A left double click goes to the innermost pressable it landed on, which may have no double click of its own.
    bool DoubleClick(Node target)
    {
        for (var i = 0; i < _landed.Count; i++)
        {
            var node = _landed[i];
            if (node.Interaction is not { Props.IsPressable: true } interaction)
                continue;
            if (
                !node.IsMounted
                || (node._states & InputStates.Disabled) != 0
                || interaction.Props.OnDoubleClick.IsEmpty
            )
                return false;
            Invoke(interaction.Props.OnDoubleClick);
            return true;
        }
        return false;
    }

    void Invoke(Callback callback)
    {
        try
        {
            callback.Invoke();
        }
        catch (Exception fault) when (Context.OnFault is not null)
        {
            Context.OnFault(fault);
        }
    }

    void DepartedGestures(Node node)
    {
        for (var p = 0; p < _pointers.Count; p++)
        {
            ref var g = ref _pointers[p].Gesture;
            if (g.Dragging is { } dragging && node.IsAncestorOrSelfOf(dragging))
                g.DraggingDeparted = true;
            if (g.Draggable is { } draggable && node.IsAncestorOrSelfOf(draggable))
                g.Draggable = null;
        }
        DepartedDragDrop(node);
    }

    void InputStepGestures()
    {
        var now = Context.Now;
        for (var i = 0; i < _pointers.Count; i++)
        {
            var state = _pointers[i];
            if (
                state.Pressing
                && state.Press.Hold == HoldPhase.Waiting
                && now - state.Press.HoldSince >= HoldTime
            )
                state.Press.Hold = HoldPhase.Started;
            if (
                state.Pointer.Kind == PointerKind.Mouse
                && state.Inside
                && state.Hover.Count > 0
                && !DragDropDrives(state)
            )
            {
                var e = new PointerEvent(
                    PointerPhase.Still,
                    state.Pointer,
                    default,
                    state.Modifiers,
                    now,
                    state.Position,
                    0
                )
                {
                    Buttons = HeldButtons(state),
                    IsInContact = InContact(state),
                };
                Bubble(state, state.Hover[0], ref e, PointerPhase.Still);
            }
        }
        RepeatDPad(now);
        RunAutoFocus();
        SendSystemGestures();
    }
}
