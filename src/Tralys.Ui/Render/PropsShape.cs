using System.ComponentModel;

namespace Tralys.Ui;

/// <summary>What the generator tells the core about one props type: the texts a host retains past the update's render,
/// the <c>bool Disabled</c> member that disables the component, and how the inspector reads each member. Generated code
/// registers one per props type; nothing else derives from it.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class PropsShape<TProps>
    where TProps : struct, IEquatable<TProps>
{
    static PropsShape<TProps>? s_current;

    internal static PropsShape<TProps>? Current => s_current;

    /// <summary>Registers <paramref name="shape"/> for <typeparamref name="TProps"/>, unless one already is.</summary>
    [FramePath]
    public static void Register(PropsShape<TProps> shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (s_current is null)
            Interlocked.CompareExchange(ref s_current, shape, null);
    }

    /// <summary>The props' <c>bool Disabled</c> member, or null when they have none.</summary>
    [FramePath]
    public virtual bool? Disabled(in TProps props) => null;

    /// <summary>Copies every text <paramref name="props"/> borrows, nested record structs' included, through
    /// <paramref name="text"/>.</summary>
    [FramePath]
    public virtual void Retain(ref TProps props, TextKeeper text) { }

    /// <summary>Hands each member of <paramref name="props"/> to <paramref name="visitor"/>, in declaration order.</summary>
    public abstract void Describe(in TProps props, IPropsVisitor visitor);
}

/// <summary>Copies texts a props value borrows, such as <see cref="Ui.Format"/>'s, into storage its host owns, so they
/// outlive the update's render. Generated code calls it.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[FramePath]
public readonly ref struct TextKeeper
{
    readonly ComponentHost _host;

    internal TextKeeper(ComponentHost host) => _host = host;

    /// <summary><paramref name="text"/>, copied into the host's storage when it borrows its characters.</summary>
    public TextValue Keep(TextValue text) => _host.KeepText(text);
}

/// <summary>Receives a component's props one member at a time, from <see cref="Node.DescribeProps"/>.</summary>
public interface IPropsVisitor
{
    /// <summary>One member: its name as declared, and its value as last rendered.</summary>
    void Prop<T>(string name, in T value);
}
