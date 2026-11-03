namespace Tralys.Ui;

internal static class NodePath
{
    public static string Of(Node? node)
    {
        if (node is null)
            return "(view)";
        var steps = new Stack<string>();
        for (var n = node; n.Parent is { } parent; n = parent)
            steps.Push(Step(n, parent));
        return string.Join('/', steps);
    }

    public static string Step(Node node, Node parent)
    {
        var name = Name(node);
        if (node.Id is { } id && !SharesId(node, parent, id, name))
            return $"{name}#{id}";
        var index = 0;
        foreach (var sibling in parent.Children)
        {
            if (sibling == node)
                break;
            index++;
        }
        return $"{name}[{index}]";
    }

    public static string Name(Node node) =>
        node.Component is { } type ? WithoutArity(type.Name) : node.Kind.ToString();

    static string WithoutArity(string name) =>
        name.IndexOf('`') is >= 0 and var tick ? name[..tick] : name;

    static bool SharesId(Node node, Node parent, string id, string name)
    {
        foreach (var sibling in parent.Children)
        {
            if (sibling != node && sibling.Id == id && Name(sibling) == name)
                return true;
        }
        return false;
    }
}
