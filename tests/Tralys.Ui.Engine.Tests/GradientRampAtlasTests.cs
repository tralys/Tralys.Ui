using Tralys.Ui.Rendering;
using TUnit.Assertions.Enums;

namespace Tralys.Ui.Engine.Tests;

public sealed class GradientRampAtlasTests
{
    [Test]
    public async Task ARamp_InterpolatesStraightColour_ThenPremultiplies()
    {
        var atlas = new GradientRampAtlas();
        var stops = new GradientStops([
            new(0f, Color.FromArgb(0, 255, 255, 255)),
            new(1f, Color.FromRgb(255, 0, 0)),
        ]);
        atlas.BeginFrame();

        var row = atlas.Acquire(stops);

        var texel = atlas.Pixels.Slice((row * GradientRampAtlas.Width + 51) * 4, 4).ToArray();
        await Assert
            .That(texel)
            .IsEquivalentTo(new byte[] { 51, 41, 41, 51 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task ARowDrawnThisFrame_IsReusedOnlyInALaterFrame()
    {
        var atlas = new GradientRampAtlas(rows: 2);
        var third = Stops();
        atlas.BeginFrame();
        atlas.Acquire(Stops());
        atlas.Acquire(Stops());

        var sameFrame = atlas.Acquire(third);
        atlas.BeginFrame();
        var nextFrame = atlas.Acquire(third);

        await Assert.That(sameFrame).IsEqualTo(-1);
        await Assert.That(nextFrame).IsGreaterThanOrEqualTo(0);
    }

    static GradientStops Stops() =>
        new([new(0f, Color.FromRgb(0, 0, 0)), new(1f, Color.FromRgb(255, 255, 255))]);
}
