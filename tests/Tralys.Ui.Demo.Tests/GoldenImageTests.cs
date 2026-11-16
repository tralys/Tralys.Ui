namespace Tralys.Ui.Demo.Tests;

public sealed class GoldenImageTests
{
    [Test]
    public async Task A_pixel_one_past_the_tolerance_differs_and_one_at_it_does_not()
    {
        var golden = new StoryImage(2, 1, [10, 10, 10, 255, 10, 10, 10, 255]);
        var past = new StoryImage(
            2,
            1,
            [10, 10 + StoryImage.Tolerance + 1, 10, 255, 10, 10, 10, 255]
        );
        var at = new StoryImage(2, 1, [10, 10, 10 + StoryImage.Tolerance, 255, 10, 10, 10, 255]);

        await Assert.That((golden.Differences(past), golden.Differences(at))).IsEqualTo((1, 0));
    }
}
