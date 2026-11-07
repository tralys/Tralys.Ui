using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.State>("Features")]
static class StateStories
{
    [FramePath]
    public static void SharedValue(Ui ui)
    {
        var name = ui.Remember(static () => new SharedName("Aria Vell"));
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "Two fields controlled by one state: each edit reaches the state and the other field. The third takes it up only as it commits, on Enter or as it loses the focus."
            );
            ui.Input(text: name.Shared, width: 240);
            ui.Input(text: name.Shared, width: 240);
            ui.Input(text: name.Draft, onCommit: name.Commit, width: 240);
            ui.Text(ui.Format($"The state holds: {name.Shared.Span}"));
        }
    }
}

sealed class SharedName
{
    public readonly ObservableText Shared;
    public readonly ObservableText Draft;
    public readonly Callback Commit;

    public SharedName(string name)
    {
        Shared = new ObservableText(name);
        Draft = new ObservableText(name);
        Shared.Changed += () => Draft.Set(Shared.Span);
        Commit = Callback.Of(this, static n => n.Shared.Set(n.Draft.Span));
    }
}
