namespace Tralys.Ui;

internal interface ITabIndexed
{
    [FramePath]
    int TabIndex { get; }
}

public sealed partial class Node
{
    internal int TabIndex =>
        Payload is ITabIndexed indexed ? indexed.TabIndex : Interaction?.Props.TabIndex ?? 0;
}
