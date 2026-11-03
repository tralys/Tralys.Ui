using System.Diagnostics.CodeAnalysis;

namespace Tralys.Ui;

/// <summary>A node held past the frame it was found in, such as a scripted step's target: it lets go once the view
/// releases the node, rather than following the node into the next use the view puts it to.</summary>
[FramePath]
public readonly struct NodeHandle
{
    readonly Node? _node;
    readonly int _generation;

    public NodeHandle(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _node = node;
        _generation = node.Generation;
    }

    /// <summary>The node while it is still the one this handle was taken of and mounted.</summary>
    public bool TryGet([NotNullWhen(true)] out Node? node)
    {
        node = _node is { IsMounted: true } held && held.Generation == _generation ? held : null;
        return node is not null;
    }
}
