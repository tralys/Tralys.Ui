namespace Tralys.Ui;

public sealed partial class UiView
{
    ValueList<FocusScope> _modalBoxes;
    ValueList<FocusScope> _activeModals;
    ValueList<FocusScope> _entering;
    ValueList<Node> _returnAncestors;
    ValueList<int> _returnGenerations;
    FocusReturn _return;
    bool _layersInactive;

    internal void ModalWritten(Node node)
    {
        for (var i = 0; i < _modalBoxes.Count; i++)
        {
            var known = _modalBoxes[i];
            if (known.Node == node && known.Generation == node.Generation)
                return;
        }
        var scope = Pool<FocusScope>.TryRent() ?? new FocusScope();
        scope.Init(this, node, null);
        _modalBoxes.Add(scope);
    }

    internal Node FocusScopeRoot()
    {
        for (var n = _focused; n is not null; n = n._parent)
        {
            if (HoldingScope(n) is not null)
                return n;
        }
        return NewestModal() ?? _root;
    }

    internal Node? CommandTarget()
    {
        if (_focused is { Payload: IEditCommandTarget })
            return _focused;
        for (var n = _focused; n is not null; n = n._parent)
        {
            if (n.Kind != NodeKind.Layer)
                continue;
            var scope = PayloadOf(n).Scope;
            if (
                scope.Opener is { Payload: IEditCommandTarget } opener
                && opener.Generation == scope.OpenerGeneration
            )
                return opener;
        }
        return null;
    }

    internal void LayersWindowFocus(bool active)
    {
        _layersInactive = !active;
        if (active)
            CheckFocusOutside();
    }

    Node? NewestModal()
    {
        for (var i = _activeModals.Count - 1; i >= 0; i--)
        {
            if (_activeModals[i].IsLive && Reaches(_activeModals[i].Node))
                return _activeModals[i].Node;
        }
        return null;
    }

    Node? KeyScopeRoot()
    {
        if (NewestModal() is { } modal)
            return modal;
        for (var tier = (int)LayerTier.Tooltip; tier >= 0; tier--)
        {
            for (var i = _layers.Count - 1; i >= 0; i--)
            {
                var layer = _layers[i];
                ref readonly var props = ref PayloadOf(layer).Props;
                if (
                    (int)props.Tier == tier
                    && props.Hit == LayerHit.Content
                    && Drawn(layer)
                    && !ExitingLayer(layer)
                    && Reaches(layer)
                )
                    return layer;
            }
        }
        return null;
    }

    FocusScope? HoldingScope(Node node)
    {
        if (node.Kind == NodeKind.Layer)
            return PayloadOf(node).Scope;
        if (node.Interaction?.Props.Modal != true)
            return null;
        for (var i = 0; i < _activeModals.Count; i++)
        {
            var scope = _activeModals[i];
            if (scope.Node == node && scope.Layer is null && scope.IsLive)
                return scope;
        }
        return null;
    }

    bool InFocusScope(Node node)
    {
        for (var n = node; n is not null; n = n._parent)
        {
            if (HoldingScope(n) is not null)
                return true;
        }
        return false;
    }

    bool FocusInside(Node node) => _focused is { } focused && node.IsAncestorOrSelfOf(focused);

    internal Node? FirstStop(Node within) =>
        IsStop(within) && CanFocus(within) ? within : FirstIn(within, within);

    internal Node? LastStop(Node within) =>
        LastIn(within, within) ?? (IsStop(within) && CanFocus(within) ? within : null);

    void SyncScopes()
    {
        for (var i = 0; i < _modalBoxes.Count; i++)
        {
            var scope = _modalBoxes[i];
            var live = scope.IsLive;
            var modal = live && scope.Node.Interaction?.Props.Modal == true;
            SyncModal(scope, modal && Drawn(scope.Node), live);
            if (modal)
                continue;
            _modalBoxes.RemoveAt(i--);
            scope.Clear();
            Pool<FocusScope>.Return(scope);
        }

        for (var i = 0; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            var payload = PayloadOf(layer);
            var scope = payload.Scope;
            var shown = Drawn(layer) && !ExitingLayer(layer);
            SyncModal(scope, shown && layer.Interaction?.Props.Modal == true, live: true);
            if (shown == scope.Shown)
                continue;
            scope.Shown = shown;
            if (shown)
            {
                if (payload.Props.AutoFocus && !FocusInside(layer))
                    AutoFocus(layer, payload);
            }
            else if (FocusInside(layer))
                ReturnFocus(scope);
        }
    }

    void SyncModal(FocusScope scope, bool active, bool live)
    {
        if (active == scope.Active)
            return;
        scope.Active = active;
        _scopeActions.Changed();
        if (active)
        {
            Activate(scope);
            return;
        }
        _activeModals.RemoveAt(_activeModals.IndexOf(scope));
        if (live && scope.Layer is null && FocusInside(scope.Node))
            ReturnFocus(scope);
    }

    void Activate(FocusScope scope)
    {
        _activeModals.Add(scope);
        if (_focused is { } focused && !scope.Node.IsAncestorOrSelfOf(focused))
        {
            scope.Opened(focused);
            MoveFocus(null, FocusCause.Code, visible: false);
        }
        if (scope.EnterPending)
            return;
        scope.EnterPending = true;
        _entering.Add(scope);
    }

    void EnterModals()
    {
        for (var i = 0; i < _entering.Count; i++)
        {
            var scope = _entering[i];
            if (!scope.EnterPending)
                continue;
            scope.EnterPending = false;
            if (!scope.Active || !scope.IsLive || FocusInside(scope.Node))
                continue;
            if (FirstStop(scope.Node) is { } first)
                MoveFocus(first, FocusCause.Code, _keyboardLast);
        }
        _entering.Clear();
    }

    void AutoFocus(Node layer, LayerPayload payload)
    {
        var content = payload.Props.Content?.Node ?? layer;
        if (FirstStop(content) is { } first)
            Focus(first, FocusCause.Code, visible: false);
    }

    bool DeferFocus(Node node)
    {
        var deferred = false;
        for (var n = _focused; n is not null; n = n._parent)
        {
            if (HoldingScope(n) is not { } scope)
                continue;
            if (n.IsAncestorOrSelfOf(node))
                break;
            scope.Deferred = node;
            scope.DeferredGeneration = node.Generation;
            deferred = true;
        }
        return deferred;
    }

    void FocusMoved()
    {
        _return = default;
        _returnAncestors.Clear();
        _returnGenerations.Clear();
        CheckFocusOutside();
    }

    void CheckFocusOutside()
    {
        if (_layersInactive)
            return;
        for (var i = _layers.Count - 1; i >= 0; i--)
        {
            var layer = _layers[i];
            var payload = PayloadOf(layer);
            var within = FocusInside(layer._parent!);
            var left = payload.FocusWithinOwner && !within;
            payload.FocusWithinOwner = within;
            if (
                left
                && (payload.Props.DismissOn & Dismiss.FocusOutside) != 0
                && Drawn(layer)
                && !ExitingLayer(layer)
            )
                DismissLayer(payload);
        }
    }

    // The tree still holds as a node leaves; what focus returns to is chosen once it has gone.
    void FocusDeparting(Node departing, Node focused)
    {
        FocusScope? outermost = null;
        for (var n = focused; n is not null; n = n._parent)
        {
            if (HoldingScope(n) is { } scope)
                outermost = scope;
            if (n == departing)
                break;
        }
        _return.Pending = outermost is not null;
        if (outermost is not null)
            Snapshot(outermost);
    }

    bool ReturnDeparted()
    {
        if (!_return.Pending)
            return false;
        _return.Pending = false;
        MoveFocus(PickReturn(), FocusCause.Code, _keyboardLast);
        return true;
    }

    void ReturnFocus(FocusScope scope)
    {
        Snapshot(scope);
        _return.Pending = false;
        MoveFocus(PickReturn(), FocusCause.Code, _keyboardLast);
    }

    void Snapshot(FocusScope scope)
    {
        _return.Restore = scope.Layer?.Props.RestoreFocus ?? true;
        _return.Deferred = scope.Deferred;
        _return.DeferredGeneration = scope.DeferredGeneration;
        _returnAncestors.Clear();
        _returnGenerations.Clear();
        if (scope.Opener is { } opener)
        {
            _return.Opener = opener;
            _return.OpenerGeneration = scope.OpenerGeneration;
            for (var i = 0; i < scope.OpenerAncestors.Count; i++)
            {
                _returnAncestors.Add(scope.OpenerAncestors[i]);
                _returnGenerations.Add(scope.OpenerAncestorGenerations[i]);
            }
            return;
        }
        var owner = scope.Node._parent;
        _return.Opener = owner;
        _return.OpenerGeneration = owner?.Generation ?? 0;
        for (var a = owner?._parent; a is not null; a = a._parent)
        {
            if (!a.IsFocusable)
                continue;
            _returnAncestors.Add(a);
            _returnGenerations.Add(a.Generation);
        }
    }

    Node? PickReturn()
    {
        Node? target = null;
        if (_return.Restore && Returnable(_return.Opener, _return.OpenerGeneration))
            target = _return.Opener;
        else if (Returnable(_return.Deferred, _return.DeferredGeneration))
            target = _return.Deferred;
        else
        {
            for (var i = 0; i < _returnAncestors.Count && target is null; i++)
            {
                if (Returnable(_returnAncestors[i], _returnGenerations[i]))
                    target = _returnAncestors[i];
            }
        }
        _return = default;
        _returnAncestors.Clear();
        _returnGenerations.Clear();
        return target;
    }

    bool Returnable(Node? node, int generation) =>
        node is not null && node.Generation == generation && CanFocus(node);

    struct FocusReturn
    {
        public bool Pending;
        public bool Restore;
        public Node? Opener;
        public int OpenerGeneration;
        public Node? Deferred;
        public int DeferredGeneration;
    }
}
