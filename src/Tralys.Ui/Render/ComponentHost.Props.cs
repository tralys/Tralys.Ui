namespace Tralys.Ui;

internal abstract partial class ComponentHost
{
    // A store writes into the buffer the current props don't use, so a text it copies is never one it overwrites.
    char[]? _textFront;
    char[]? _textBack;
    int _textUsed;

    internal abstract bool DescribeProps(IPropsVisitor visitor);

    internal TextValue KeepText(TextValue text)
    {
        if (!text.IsBorrowed)
            return text;
        var chars = text.Span;
        if (chars.IsEmpty)
            return "";
        var at = _textUsed;
        if (_textBack is null || _textBack.Length - at < chars.Length)
        {
            _textBack = new char[
                Math.Max(Math.Max(chars.Length, 32), (_textBack?.Length ?? 0) * 2)
            ];
            at = 0;
        }
        chars.CopyTo(_textBack.AsSpan(at));
        _textUsed = at + chars.Length;
        return new TextValue(_textBack, at, chars.Length);
    }

    private protected void BeginKeep() => _textUsed = 0;

    private protected void EndKeep() => (_textFront, _textBack) = (_textBack, _textFront);

    private protected void WriteDisabled(bool disabled)
    {
        var inherit = Node.Inherit;
        if (inherit.Disabled == disabled)
            return;
        inherit.Disabled = disabled;
        if (Node.WriteInherit(inherit))
            View.Stats.NodeWrites++;
    }
}

internal sealed partial class ComponentHost<TProps>
{
    internal void Store(in TProps props)
    {
        Value = props;
        if (PropsShape<TProps>.Current is not { } shape)
            return;
        BeginKeep();
        shape.Retain(ref Value, new TextKeeper(this));
        EndKeep();
        if (shape.Disabled(in Value) is { } disabled)
            WriteDisabled(disabled);
    }

    internal override bool DescribeProps(IPropsVisitor visitor)
    {
        if (PropsShape<TProps>.Current is not { } shape)
            return false;
        shape.Describe(in Value, visitor);
        return true;
    }
}
