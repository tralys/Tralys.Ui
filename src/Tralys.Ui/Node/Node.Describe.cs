namespace Tralys.Ui;

public sealed partial class Node
{
    /// <summary>Hands a component node's props to <paramref name="visitor"/>, one member at a time in declaration
    /// order, through the describer generated for its props type.</summary>
    /// <returns>False when the node hosts no component, or its props type has no generated describer.</returns>
    public bool DescribeProps(IPropsVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        return Host?.DescribeProps(visitor) ?? false;
    }
}
