using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Generators.Tests;

public sealed class WatchTests
{
    [Test]
    public async Task AReactionsOwnReads_AreNotWatched()
    {
        var gauge = new Gauge();
        var selects = 0;
        using var watch = Observation.Watch(
            () =>
            {
                selects++;
                return gauge.Value;
            },
            _ => _ = gauge.Limit
        );

        gauge.Limit = 3;

        await Assert.That(selects).IsEqualTo(1);
    }

    [Test]
    public async Task AChangeThatLeavesTheResultEqual_DoesNotReact()
    {
        var gauge = new Gauge { Value = 6 };
        var seen = new List<bool>();
        using var watch = Observation.Watch(() => gauge.Value > 5, seen.Add);

        gauge.Value = 7;
        gauge.Value = 2;

        await Assert
            .That(seen)
            .IsEquivalentTo([true, false], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ASourceTheSelectorStoppedReading_IsUnlinked()
    {
        var gate = new Gauge { Value = 1 };
        var left = new Gauge();
        var right = new Gauge();
        using var watch = Observation.Watch(
            () => gate.Value > 0 ? left.Value : right.Value,
            _ => { }
        );

        gate.Value = 0;

        await Assert.That(ObserversOf(left)).IsEqualTo(0);
        await Assert.That(ObserversOf(right)).IsEqualTo(1);
    }

    [Test]
    public async Task AWatchDisposedByItsOwnReaction_LeavesTheSourcesOtherObserversNotified()
    {
        var gauge = new Gauge();
        IDisposable? first = null;
        first = Observation.Watch(() => gauge.Value, _ => first?.Dispose(), now: false);
        var seen = new List<int>();
        using var second = Observation.Watch(() => gauge.Value, seen.Add, now: false);
        using var third = Observation.Watch(() => gauge.Value, seen.Add, now: false);

        gauge.Value = 4;

        await Assert.That(seen).IsEquivalentTo([4, 4]);
        await Assert.That(ObserversOf(gauge)).IsEqualTo(2);
    }

    [Test]
    public async Task AFaultingReaction_GoesToOnFault_AndTheSourcesOtherObserversStillHear()
    {
        var gauge = new Gauge();
        var seen = new List<int>();
        var faults = new List<Exception>();
        using var recorder = Observation.Watch(() => gauge.Value, seen.Add, now: false);
        using var faulting = Observation.Watch(
            () => gauge.Value,
            _ => throw new InvalidOperationException(),
            now: false,
            onFault: faults.Add
        );

        gauge.Value = 4;

        await Assert.That(seen).IsEquivalentTo([4]);
        await Assert.That(faults.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Disposing_UnlinksTheWatchFromEverythingItRead()
    {
        var gauge = new Gauge();
        var watch = Observation.Watch(() => gauge.Value + gauge.Limit, _ => { });

        watch.Dispose();

        await Assert.That(ObserversOf(gauge)).IsEqualTo(0);
    }

    static int ObserversOf(IObservable source) => source.Observers.Count;
}

sealed partial class Gauge : ObservableObject
{
    [ObservableProperty]
    public partial int Value { get; set; }

    [ObservableProperty]
    public partial int Limit { get; set; }
}
