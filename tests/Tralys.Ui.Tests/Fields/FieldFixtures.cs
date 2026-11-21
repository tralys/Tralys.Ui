using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

/// <summary>A text an app holds for a field, which remembers every change the field makes to it or reports.</summary>
public sealed class Entry
{
    public readonly ObservableText Text;

    /// <summary>The text after each change the field made to <see cref="Text"/>, and each string it reported to
    /// <see cref="Set"/>.</summary>
    public readonly List<string> Reports = [];

    bool _setting;

    public Entry(string value = "")
    {
        Text = new ObservableText(value);
        Text.Changed += Changed;
    }

    /// <summary>The text; setting it is the app's change, which <see cref="Reports"/> leaves out.</summary>
    public string Value
    {
        get => Text.ToString();
        set
        {
            _setting = true;
            try
            {
                Text.Set(value);
            }
            finally
            {
                _setting = false;
            }
        }
    }

    /// <summary>Takes each string a field reports without remembering it, for a frame that must allocate
    /// nothing.</summary>
    public Callback<string> Keep =>
        Callback<string>.Of(this, static (entry, value) => entry.Value = value);

    /// <summary>Takes each string a field reports, remembering it in <see cref="Reports"/>.</summary>
    public Callback<string> Set =>
        Callback<string>.Of(
            this,
            static (entry, value) =>
            {
                entry.Reports.Add(value);
                entry.Value = value;
            }
        );

    void Changed()
    {
        if (!_setting)
            Reports.Add(Text.ToString());
    }
}

internal sealed class Boxed<T>(T value)
{
    public readonly T Value = value;
}

internal static class Fields
{
    public static Callback Into(List<string> log, ObservableText text) =>
        Callback.Of(log, text, static (l, t) => l.Add(t.ToString()));

    public static Callback Into(
        List<(string What, string Text)> log,
        string what,
        ObservableText text
    ) =>
        Callback.Of(
            log,
            new Boxed<(string What, ObservableText Text)>((what, text)),
            static (l, w) => l.Add((w.Value.What, w.Value.Text.ToString()))
        );

    public static Node TextOf(this UiDriver driver, string? id = null) =>
        Nodes.Find(driver.Get<Input>(id), NodeKind.EditableText);

    public static Node RootOf(this UiDriver driver, string? id = null) =>
        Nodes.Find(driver.Get<Input>(id), NodeKind.Box);

    public static string Shown(this UiDriver driver, string? id = null) =>
        driver.TextOf(id).Text.ToString();

    public static (int Anchor, int Caret) Selection(this UiDriver driver, string? id = null)
    {
        if (driver.TextOf(id).Payload is not EditorPayload editor)
            throw new InvalidOperationException("The field holds no editor.");
        return (editor.Selection.Anchor, editor.Selection.Caret);
    }

    /// <summary>Focuses the input's text from code, then puts the caret at <paramref name="caret"/> by key.</summary>
    public static void CaretAt(this UiDriver driver, int caret, string? id = null)
    {
        driver.Focus(driver.TextOf(id));
        driver.Press(Key.Home, ModifierKeys.Control);
        for (var i = 0; i < caret; i++)
            driver.Press(Key.Right);
    }
}
