namespace Tralys.Ui;

/// <summary>Registers each <c>public static void Name(Ui ui)</c> of the class it marks as the story <c>T.Name</c>, where
/// T's name joins its containing types' with dots (<c>Button.Variants</c>, <c>Dialog.Title.Name</c>), listed under
/// <paramref name="category"/>. The generated <c>StoryRegistry</c> in the assembly's root namespace lists them; a key
/// registered twice is a build error.</summary>
/// <param name="category">The category a gallery lists the class's stories under, such as <c>Buttons</c>.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StoriesAttribute<T>(string category) : Attribute
{
    /// <summary>The category a gallery lists the class's stories under.</summary>
    public string Category { get; } = category;
}

/// <summary>A registered story: its key, the category it's listed under, the type it shows, and the render a gallery
/// mounts as its root.</summary>
public readonly record struct Story(string Key, string Category, Type Component, Action<Ui> Render);
