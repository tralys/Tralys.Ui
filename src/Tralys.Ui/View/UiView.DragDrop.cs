using System.Numerics;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

public sealed partial class UiView
{
    const DragEffect AnyEffect = DragEffect.Copy | DragEffect.Move | DragEffect.Link;

    readonly DragDropSession _dragDrop = new();
    Node? _dropRaised;

    /// <summary>Ends a running drag-and-drop without a drop: its target hears it leave, and its source's
    /// <c>onDragEnd</c> gets <see cref="DragEffect.None"/>.</summary>
    public void CancelDrag()
    {
        VerifyAccess();
        CancelDragDrop();
    }

    internal bool DragDropActive => _dragDrop.Active;

    internal Vector2 DragDropPosition => _dragDrop.Position;

    internal bool ReadDragPreview(out Slot preview, out Vector2 at)
    {
        var session = _dragDrop;
        Observation.Read(session, 0);
        preview = session.Preview;
        at = session.Position;
        return session.Active && !preview.IsEmpty;
    }

    bool DragDropDrives(PointerState state) =>
        _dragDrop.Active && _dragDrop.Pointer == state.Pointer;

    void BeginDragDrop(
        PointerState state,
        Node source,
        object data,
        DragEffect effects,
        Slot preview,
        Callback<DragEffect> onDragEnd,
        MouseButton button
    )
    {
        var session = _dragDrop;
        session.Active = true;
        session.Button = button;
        session.Pointer = state.Pointer;
        session.Source = source;
        session.SourceGeneration = source.Generation;
        session.OnDragEnd = onDragEnd;
        session.Data = data;
        session.Allowed = effects == DragEffect.None ? AnyEffect : effects;
        session.Preview = preview;
        session.Target = null;
        session.Effect = DragEffect.None;
        session.Position = state.Position;
        session.Modifiers = state.Modifiers;
        session.Changed();
    }

    void DragDropMove(PointerState state)
    {
        var session = _dragDrop;
        if (!session.Active)
            return;
        if (session.Position != state.Position && !session.Preview.IsEmpty)
            session.Changed();
        session.Position = state.Position;
        session.Modifiers = state.Modifiers;
        var target = DropTargetAt(state.Position);
        if (target != session.Target)
        {
            if (LeaveDropTarget())
                return;
            session.Target = target;
            session.TargetGeneration = target?.Generation ?? 0;
        }
        if (target is null)
            return;
        var e = new DropEvent(
            session.Data,
            session.Allowed,
            session.Allowed,
            LocalPosition(target, session.Position),
            session.Modifiers
        )
        {
            Button = session.Button,
        };
        if (target.Interaction?.Props.OnDragOver is { IsEmpty: false } over)
        {
            try
            {
                over.Invoke(ref e);
            }
            catch (Exception fault) when (Context.OnFault is not null)
            {
                Context.OnFault(fault);
            }
        }
        if (!session.Active || session.Target != target)
            return;
        session.Effect = e.Effect;
        Light(target, e.Effect != DragEffect.None);
    }

    Node? DropTargetAt(Vector2 point)
    {
        var node = HitAt(point);
        while (node is not null && (node._states & InputStates.Disabled) != 0)
            node = node._parent;
        for (; node is not null; node = node._parent)
        {
            if (node.Interaction?.Props.OnDrop.IsEmpty == false)
                return node;
        }
        return null;
    }

    void DragDropDrop()
    {
        var session = _dragDrop;
        if (!session.Active)
            return;
        if (session.Target is not { } target || session.Effect == DragEffect.None)
        {
            if (LeaveDropTarget())
                return;
            EndDragDrop(DragEffect.None);
            return;
        }
        session.Target = null;
        Light(target, false);
        var e = new DropEvent(
            session.Data,
            session.Allowed,
            session.Effect,
            LocalPosition(target, session.Position),
            session.Modifiers
        )
        {
            Button = session.Button,
        };
        try
        {
            target.Interaction?.Props.OnDrop.Invoke(ref e);
        }
        catch (Exception fault) when (Context.OnFault is not null)
        {
            Context.OnFault(fault);
        }
        if (session.Active)
            EndDragDrop(e.Effect);
    }

    void CancelDragDrop()
    {
        if (!_dragDrop.Active || LeaveDropTarget())
            return;
        EndDragDrop(DragEffect.None);
    }

    // Lets go of the target before telling it, so a handler that cancels the drag isn't told again.
    bool LeaveDropTarget()
    {
        var session = _dragDrop;
        if (session.Target is not { } target)
            return false;
        session.Target = null;
        session.Effect = DragEffect.None;
        Light(target, false);
        if (target.IsMounted && target.Generation == session.TargetGeneration)
            Invoke(target.Interaction?.Props.OnDragLeave ?? default);
        return !session.Active;
    }

    void EndDragDrop(DragEffect effect)
    {
        var session = _dragDrop;
        var onDragEnd =
            session.Source is { IsMounted: true } source
            && source.Generation == session.SourceGeneration
            && source.Interaction is { } interaction
                ? interaction.Props.OnDragEnd
                : session.OnDragEnd;
        session.Active = false;
        session.Source = null;
        session.Data = null;
        session.Preview = default;
        session.OnDragEnd = default;
        session.Target = null;
        session.Changed();
        Invoke(onDragEnd, effect);
    }

    void Light(Node target, bool on)
    {
        target.SetStates(
            on ? target._states | InputStates.DropTarget : target._states & ~InputStates.DropTarget
        );
        var raised = on ? target.LayoutParent : null;
        if (raised == _dropRaised)
            return;
        _dropRaised = raised;
        MarkComposite();
    }

    int ZOf(Node node) => node == _dropRaised ? int.MaxValue : node.Place.ZIndex;

    void DepartedDragDrop(Node node)
    {
        var session = _dragDrop;
        if (session.Active && session.Target is { } target && node.IsAncestorOrSelfOf(target))
        {
            session.Target = null;
            session.Effect = DragEffect.None;
            Light(target, false);
            if (target.Interaction?.Props.OnDragLeave.IsEmpty == false)
                _notices.Add(new Notice(NoticeKind.DragLeft, target, target.Generation, default));
        }
        if (_dropRaised is { } raised && node.IsAncestorOrSelfOf(raised))
        {
            _dropRaised = null;
            MarkComposite();
        }
    }

    sealed class DragDropSession : IObservable
    {
        ObserverSet _observers;

        public ref ObserverSet Observers => ref _observers;

        public void Changed() => _observers.Notify(this, 0);

        public bool Active;
        public Pointer Pointer;
        public Node? Source;
        public int SourceGeneration;
        public Callback<DragEffect> OnDragEnd;
        public object? Data;
        public DragEffect Allowed;
        public Slot Preview;
        public Node? Target;
        public int TargetGeneration;
        public DragEffect Effect;
        public Vector2 Position;
        public ModifierKeys Modifiers;
        public MouseButton Button;
    }
}

internal readonly record struct DragPreviewProps;

internal sealed class DragPreview : Component<DragPreviewProps>
{
    protected override void Render(Ui ui, in DragPreviewProps p)
    {
        if (!ui.Builder.View.ReadDragPreview(out var preview, out var at))
            return;
        var insets = ui.SafeInsets;
        using (ui.Layer(hit: LayerHit.PassThrough, tier: LayerTier.Tooltip))
        {
            using (
                ui.Box(
                    position: Position.Absolute,
                    left: at.X - insets.Left,
                    top: at.Y - insets.Top,
                    width: 0,
                    height: 0,
                    justify: Justify.Center,
                    align: Align.Center
                )
            )
            {
                using (ui.Box(shrink: 0))
                    preview.Render(ui);
            }
        }
    }
}
