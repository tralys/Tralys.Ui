namespace Tralys.Ui;

[FramePath]
internal sealed class CanRunHook : Hook
{
    public const string Site = "<CanRun>";

    public UiView? View;
    public Node? Refusing;

    [FramePath]
    public void Refuse(UiView view, Node? node)
    {
        View = view;
        if (Refusing == node)
            return;
        if (Refusing is { } old)
            view.SetCommandRefused(old, false);
        Refusing = node;
        if (node is not null)
            view.SetCommandRefused(node, true);
    }

    public override void Discard()
    {
        if (Refusing is { } old)
            View?.SetCommandRefused(old, false);
        Refusing = null;
        View = null;
        Pool<CanRunHook>.Return(this);
    }
}

[FramePath]
internal sealed class ShortcutHook : Hook
{
    public UiView? View;
    public ComponentHost? Host;
    public Chord Chord;
    public Callback OnPress;
    public ShortcutScope Scope;
    public bool Repeat;
    public bool Disabled;

    public override void Discard()
    {
        View?.RemoveShortcut(this);
        View = null;
        Host = null;
        OnPress = default;
        Pool<ShortcutHook>.Return(this);
    }
}

[FramePath]
internal sealed class ActionHook : Hook
{
    char[] _label = [];
    int _labelLength;
    bool _hasLabel;

    public ActionScopeState? Scope;
    public UiAction Action;
    public Callback OnRun;
    public bool Hidden;

    public TextValue Label => _hasLabel ? new TextValue(_label, 0, _labelLength) : Action.Name;

    // Keeps its own copy, since a label from ui.Format lives only until the render ends.
    [FramePath]
    public bool SetLabel(TextValue label)
    {
        if (label.IsNull)
        {
            if (!_hasLabel)
                return false;
            _hasLabel = false;
            return true;
        }
        var text = label.Span;
        if (_hasLabel && text.SequenceEqual(_label.AsSpan(0, _labelLength)))
            return false;
        if (_label.Length < text.Length)
            _label = new char[text.Length];
        text.CopyTo(_label);
        _labelLength = text.Length;
        _hasLabel = true;
        return true;
    }

    public override void Discard()
    {
        Scope?.Remove(this);
        Scope = null;
        OnRun = default;
        Action = default;
        Hidden = false;
        _hasLabel = false;
        Pool<ActionHook>.Return(this);
    }
}

/// <summary>An action scope: the actions registered on it, whether it is active, and its place among the view's
/// scopes.</summary>
[FramePath]
internal sealed class ActionScopeState : NodePayload
{
    public UiView? View;
    public Node? Node;
    public bool Active;
    public ValueList<ActionHook> Actions;

    [FramePath]
    public void Add(ActionHook hook)
    {
        Actions.Add(hook);
        View?.ScopeActionsChanged(this);
    }

    [FramePath]
    public void Remove(ActionHook hook)
    {
        var index = Actions.IndexOf(hook);
        if (index < 0)
            return;
        Actions.RemoveAt(index);
        View?.ScopeActionsChanged(this);
    }

    public override void Release(UiView view)
    {
        view.SetScopeActive(this, false);
        for (var i = 0; i < Actions.Count; i++)
            Actions[i].Scope = null;
        Actions.Clear();
        View = null;
        Node = null;
        Pool<ActionScopeState>.Return(this);
    }
}
