using System.Reflection;
using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ComponentLeakTests
{
    static readonly MethodInfo MountOf = typeof(UiView)
        .GetMethods()
        .Single(static m => m.Name == nameof(UiView.Mount) && m.IsGenericMethodDefinition);

    [Test]
    public async Task Every_exported_component_mounts_with_default_props_but_a_text_to_edit_and_its_view_is_collected_once_it_unmounts_and_closes()
    {
        var components = typeof(Component<>)
            .Assembly.GetExportedTypes()
            .Where(static t =>
                !t.IsAbstract
                && !t.IsGenericTypeDefinition
                && t.BaseType is { IsGenericType: true } b
                && b.GetGenericTypeDefinition() == typeof(Component<>)
                && t.GetConstructor(Type.EmptyTypes) is not null
            )
            .OrderBy(static t => t.FullName, StringComparer.Ordinal)
            .ToList();
        var threw = new List<string>();
        var views = new List<(string Name, WeakReference View)>();
        var texts = new List<ObservableText>();

        foreach (var component in components)
        {
            try
            {
                views.Add((component.Name, MountedAndClosed(component, texts)));
            }
            catch (Exception e)
            {
                threw.Add($"{component.Name}: {e.GetBaseException().Message}");
            }
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Assert.That(components.Count).IsGreaterThan(50);
        await Assert.That(threw).IsEmpty();
        await Assert
            .That(views.Where(static v => v.View.IsAlive).Select(static v => v.Name))
            .IsEmpty();
        GC.KeepAlive(texts);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference MountedAndClosed(Type component, List<ObservableText> texts)
    {
        var props = component.BaseType!.GetGenericArguments()[0];
        var value = Activator.CreateInstance(props)!;
        foreach (var property in props.GetProperties())
        {
            if (property.PropertyType != typeof(ObservableText))
                continue;
            var text = new ObservableText();
            texts.Add(text);
            property.SetValue(value, text);
        }
        using var driver = UiDriver.Render(static _ => { });
        MountOf.MakeGenericMethod(component, props).Invoke(driver.View, [value]);
        driver.Settle();
        driver.View.Mount(Slot.Of(static _ => { }));
        driver.Settle();
        return new WeakReference(driver.View);
    }
}
