using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<ListBox<int>>("Data")]
static class ScrollToLastStories
{
    static readonly KeyOf<int> ByNumber = static n => n;
    static readonly TextOf<int> Entry = static n =>
        n.ToString(System.Globalization.CultureInfo.InvariantCulture);
    static readonly Slot<int> Row = Slot<int>.Of(static (ui, n, _) => ui.Text($"Entry {n}"));

    sealed class Log
    {
        public readonly ObservableList<int> Entries = new();
        public readonly IItemsSource<int> Items;
        public readonly ScrollHandle Scroll = new();
        public int Logged;

        public Log()
        {
            for (var i = 1; i <= 30; i++)
                Entries.Add(i);
            Logged = 30;
            Items = ItemsSource.From(Entries);
        }

        [FramePath]
        public void Next()
        {
            Entries.RemoveAt(0);
            Entries.Add(++Logged);
            Scroll.ScrollToEnd();
        }
    }

    [FramePath]
    public static void ScrollToLast(Ui ui)
    {
        var log = ui.Remember(static () => new Log());
        ui.Effect(log, 0, static (l, _, _) => l.Scroll.ScrollToEnd());
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                width: 280,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "Each entry logged takes the oldest row to the end, and the list scrolls to it after"
            );
            ui.Button(
                text: "Log an entry",
                onClick: Callback.Of(log, static l => l.Next()),
                alignSelf: AlignSelf.Start
            );
            ui.ListBox(
                items: log.Items,
                itemKey: ByNumber,
                item: Row,
                scroll: log.Scroll,
                height: 160
            );
        }
    }
}
