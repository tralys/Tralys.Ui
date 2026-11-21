using Tralys.Ui.Mvvm;
using TUnit.Assertions.Enums;

namespace Tralys.Ui.Engine.Tests;

public sealed class ItemsTests
{
    [Test]
    public async Task AListenerSwappedForAnotherAsItIsTold_LeavesTheRestToldOnce_AndTheNewcomerFromTheNextChange()
    {
        var list = new ObservableList<string>();
        var items = ItemsSource.From(list);
        var newcomer = new Counter(null);
        var leaving = new Counter(newcomer);
        var staying = new Counter(null);
        items.AddListener(leaving);
        items.AddListener(staying);

        list.Add("first");
        list.Add("second");

        await Assert.That(leaving.Told).IsEqualTo(1);
        await Assert.That(staying.Told).IsEqualTo(2);
        await Assert.That(newcomer.Told).IsEqualTo(1);
    }

    sealed class Counter(Counter? successor) : IItemsSourceListener
    {
        Counter? _successor = successor;

        public int Told { get; private set; }

        public void OnItemsChanged(IItemsSource source, in ItemsChange change)
        {
            Told++;
            if (_successor is not { } next)
                return;

            _successor = null;
            source.RemoveListener(this);
            source.AddListener(next);
        }
    }

    [Test]
    public async Task AListenerChangingItsSourceWhileToldOfAChange_Throws()
    {
        var list = new ObservableList<string>();
        var items = ItemsSource.From(list);
        items.AddListener(new Meddler(() => list.Add("meddled")));

        await Assert.That(() => list.Add("first")).Throws<InvalidOperationException>();
    }

    sealed class Meddler(Action meddle) : IItemsSourceListener
    {
        Action? _meddle = meddle;

        public void OnItemsChanged(IItemsSource source, in ItemsChange change)
        {
            var meddle = _meddle;
            _meddle = null;
            meddle?.Invoke();
        }
    }

    [Test]
    public async Task Syncing_into_a_new_order_moves_only_the_rows_out_of_place()
    {
        var list = new ObservableList<string> { "a", "b", "c", "d", "e", "f" };
        var kinds = new List<ListChangeKind>();
        using var subscription = list.Subscribe((_, in change) => kinds.Add(change.Kind));

        list.Sync(["b", "a", "c", "e", "d", "f"], static s => s);

        await Assert
            .That(list)
            .IsEquivalentTo(["b", "a", "c", "e", "d", "f"], CollectionOrdering.Matching);
        await Assert.That(kinds).IsEquivalentTo([ListChangeKind.Move, ListChangeKind.Move]);
    }

    [Test]
    public async Task A_row_whose_match_flips_enters_and_leaves_the_matching_source_at_its_place()
    {
        var flags = new ObservableList<Flag>();
        foreach (var on in (bool[])[true, false, true, true, false, true])
            flags.Add(new Flag { On = on });
        using var rows = flags.ToView().Filter(static flag => flag.On);
        var shown = ItemsSource.Matching<Flag>(rows);
        var heard = new Recorder();
        shown.AddListener(heard);

        flags[1].On = true;
        flags[3].On = false;

        await Assert
            .That(Shown(shown))
            .IsEquivalentTo([flags[0], flags[1], flags[2], flags[5]], CollectionOrdering.Matching);
        await Assert
            .That(heard.Changes)
            .IsEquivalentTo(
                [(ItemsChangeKind.Add, 1), (ItemsChangeKind.Remove, 3)],
                CollectionOrdering.Matching
            );
    }

    [Test]
    public async Task A_disposed_view_reads_empty_to_its_matching_source_whatever_its_items_do_after()
    {
        var flags = new ObservableList<Flag>();
        foreach (var on in (bool[])[true, true])
            flags.Add(new Flag { On = on });
        var rows = flags.ToView().Filter(static flag => flag.On);
        var shown = ItemsSource.Matching<Flag>(rows);

        rows.Dispose();
        flags.Clear();

        await Assert.That(shown.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_filtered_view_shows_the_matching_rows_after_inserts_removes_moves_and_replaces()
    {
        var flags = new ObservableList<Flag>();
        foreach (var on in (bool[])[true, false, true, false, true])
            flags.Add(new Flag { On = on });
        using var rows = flags.ToView().Filter(static flag => flag.On);
        var shown = ItemsSource.Matching<Flag>(rows);
        var wrong = new List<string>();
        void Check(string step)
        {
            if (!Shown(shown).SequenceEqual(flags.Where(static flag => flag.On)))
                wrong.Add(step);
        }

        flags.Insert(1, new Flag { On = true });
        Check("insert");
        flags.InsertRange(3, [new Flag { On = false }, new Flag { On = true }]);
        Check("insert range");
        flags.RemoveAt(0);
        Check("remove");
        flags.Move(5, 1);
        Check("move a hidden row");
        flags.Move(4, 0);
        Check("move a shown row");
        flags[2] = new Flag { On = true };
        Check("replace");
        flags.RemoveRange(3, 2);
        Check("remove range");

        await Assert.That(wrong).IsEmpty();
    }

    [Test]
    public async Task Syncing_with_removals_insertions_and_moves_at_once_lands_on_the_desired_order()
    {
        var list = new ObservableList<string> { "a", "b", "c", "d", "e" };
        var kinds = new List<ListChangeKind>();
        using var subscription = list.Subscribe((_, in change) => kinds.Add(change.Kind));

        list.Sync(["x", "c", "a", "e", "y"], static s => s);

        await Assert
            .That(list)
            .IsEquivalentTo(["x", "c", "a", "e", "y"], CollectionOrdering.Matching);
        await Assert.That(kinds).DoesNotContain(ListChangeKind.Reset);
    }

    [Test]
    public async Task Syncing_into_a_reversed_order_keeps_and_updates_the_original_items()
    {
        Box a = new("a", 0),
            b = new("b", 0),
            c = new("c", 0),
            d = new("d", 0);
        var list = new ObservableList<Box> { a, b, c, d };
        var intact = true;

        list.Sync(
            [new("d", 4), new("c", 3), new("b", 2), new("a", 1)],
            static box => box.Key,
            (kept, desired) =>
            {
                intact &= list.All(static box => box is not null);
                kept.Value = desired.Value;
            }
        );

        await Assert
            .That(list)
            .IsEquivalentTo([d, c, b, a], CollectionOrdering.Matching)
            .Using(EqualityComparer<Box>.Default);
        await Assert
            .That(list.Select(static box => box.Value))
            .IsEquivalentTo([4, 3, 2, 1], CollectionOrdering.Matching);
        await Assert.That(intact).IsTrue();
    }

    [Test]
    public async Task A_row_rechecked_alone_keeps_the_filter_hearing_what_other_rows_read()
    {
        var gate = new Gate { OwnedOnly = true };
        var a = new Flag { On = true };
        var b = new Flag { On = true };
        var flags = new ObservableList<Flag> { a, b };
        using var rows = flags
            .ToView()
            .Filter(flag => !(gate.OwnedOnly && !flag.On) && gate.Query.Length == 0);

        b.On = false;
        gate.Query = "zzz";

        await Assert.That(rows.IsShown(a)).IsFalse();
    }

    [Test]
    public async Task A_filter_reading_its_own_list_follows_an_insert()
    {
        var first = new Flag();
        var flags = new ObservableList<Flag> { first };
        using var rows = flags.ToView().Filter(flag => flags.IndexOf(flag) % 2 == 0);

        var added = new Flag();
        flags.Insert(0, added);

        await Assert.That((rows.IsShown(added), rows.IsShown(first))).IsEqualTo((true, false));
    }

    [Test]
    public async Task Replacing_every_item_raises_one_reset_carrying_the_old_and_the_new()
    {
        var list = new ObservableList<string> { "a", "b" };
        var heard = new List<(ListChangeKind Kind, string Old, string New)>();
        using var subscription = list.Subscribe(
            (_, in change) =>
                heard.Add(
                    (
                        change.Kind,
                        string.Join(",", change.OldItems.ToArray()),
                        string.Join(",", change.NewItems.ToArray())
                    )
                )
        );

        list.ReplaceAll(["c", "d", "e"]);

        await Assert.That(heard).IsEquivalentTo([(ListChangeKind.Reset, "a,b", "c,d,e")]);
    }

    [Test]
    public async Task An_observer_changing_a_list_while_it_raises_throws()
    {
        var list = new ObservableList<string>();
        var meddler = new Meddling(list);
        using (Observation.Track(meddler))
            _ = list.Count;

        await Assert.That(() => list.Add("first")).Throws<InvalidOperationException>();
    }

    static List<Flag> Shown(IItemsSource<Flag> source)
    {
        var shown = new List<Flag>();
        for (var i = 0; i < source.Count; i++)
            shown.Add(source.ItemAt(i));
        return shown;
    }

    sealed class Flag : IObservable
    {
        ObserverSet _observers;
        bool _on;

        ref ObserverSet IObservable.Observers => ref _observers;

        public bool On
        {
            get
            {
                Observation.Read(this, 0);
                return _on;
            }
            set
            {
                _on = value;
                _observers.Notify(this, 0);
            }
        }
    }

    sealed class Recorder : IItemsSourceListener
    {
        public List<(ItemsChangeKind, int)> Changes { get; } = [];

        public void OnItemsChanged(IItemsSource source, in ItemsChange change) =>
            Changes.Add(
                (
                    change.Kind,
                    change.Kind == ItemsChangeKind.Remove ? change.OldIndex : change.NewIndex
                )
            );
    }

    sealed class Box(string key, int value)
    {
        public string Key => key;

        public int Value { get; set; } = value;
    }

    sealed class Gate : IObservable
    {
        ObserverSet _observers;
        bool _ownedOnly;
        string _query = "";

        ref ObserverSet IObservable.Observers => ref _observers;

        public bool OwnedOnly
        {
            get
            {
                Observation.Read(this, 0);
                return _ownedOnly;
            }
            set
            {
                _ownedOnly = value;
                _observers.Notify(this, 0);
            }
        }

        public string Query
        {
            get
            {
                Observation.Read(this, 1);
                return _query;
            }
            set
            {
                _query = value;
                _observers.Notify(this, 1);
            }
        }
    }

    sealed class Meddling(ObservableList<string> list) : IChangeObserver
    {
        Dependencies _dependencies;

        public ref Dependencies Dependencies => ref _dependencies;

        public void OnChanged(object source, int property) => list.Clear();
    }
}
