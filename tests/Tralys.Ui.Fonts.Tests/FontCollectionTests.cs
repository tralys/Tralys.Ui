namespace Tralys.Ui.Fonts.Tests;

public sealed class FontCollectionTests
{
    [Test]
    public async Task Find_PicksTheWeightCssMatchingWould()
    {
        using var collection = new FontCollection(
            new FakeBackend(Face(300), Face(500), Face(700), Face(900))
        );
        collection.Add(ReadOnlyMemory<byte>.Empty);
        var medium = collection.Add(ReadOnlyMemory<byte>.Empty);
        collection.Add(ReadOnlyMemory<byte>.Empty);
        var black = collection.Add(ReadOnlyMemory<byte>.Empty);

        await Assert.That(collection.Find("Fake", 400)).IsSameReferenceAs(medium);
        await Assert.That(collection.Find("Fake", 800)).IsSameReferenceAs(black);
    }

    static FakeFace Face(int weight) => new([], 1000, weight);
}
