using Tralys.Ui.Testing;

namespace Tralys.Ui.Engine.Tests;

public sealed class ItemsAllocationTests
{
    [Test]
    public async Task AnArraysSource_KeepsNoListeners_SoTheThreadsShowingItShareNothingToChange()
    {
        var source = ItemsSource.From(new[] { "first", "second", "third" });
        var listener = new Deaf();

        var worst = AllocationGate.WorstFrameBytes(
            () => source.AddListener(listener),
            frames: 3,
            windows: 1,
            warmup: 0
        );

        await Assert.That(worst).IsEqualTo(0L);
    }

    sealed class Deaf : IItemsSourceListener
    {
        public void OnItemsChanged(IItemsSource source, in ItemsChange change) { }
    }
}
