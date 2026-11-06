using Tralys.Ui.Demo.Workload;

namespace Tralys.Ui.Demo.Tests;

public sealed class WorkloadTests
{
    [Test]
    public async Task The_stats_lines_milliseconds_keep_their_width_however_long_a_frame_took()
    {
        Span<char> fast = stackalloc char[24];
        Span<char> slow = stackalloc char[24];

        var quick = FrameStats.Milliseconds(0.42, fast).Length;
        var stalled = FrameStats.Milliseconds(123456.0, slow).Length;

        await Assert.That(stalled).IsEqualTo(quick);
    }
}
