using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;

namespace Tralys.Ui.DevTools.Tests;

public sealed class InspectorAllocationTests
{
    [Test]
    public async Task An_inspector_beside_an_idle_target_allocates_nothing()
    {
        var gauge = new Gauge();
        using var app = UiDriver.Render(ui => ui.Dial(gauge: gauge));
        using var host = Host.Over(app, gauge);
        host.Inspector.Selected = app.GetText("tick 0");

        var bytes = app.WorstFrameBytes(host, static (d, h) => h.Frame(d));

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task An_inspector_following_a_target_that_rerenders_remounts_and_repaints_every_frame_allocates_nothing()
    {
        var gauge = new Gauge();
        using var app = UiDriver.Render(ui => ui.Dial(gauge: gauge));
        using var host = Host.Over(app, gauge);
        host.Inspector.Selected = app.GetText("tick 0");
        host.Inspector.Selected = app.Get<Meter>();
        host.Inspector.Model.Watch("Level");
        host.Inspector.Model.Watching = true;

        var bytes = app.WorstFrameBytes(
            host,
            static (d, h) =>
            {
                h.Gauge.Level = (h.Gauge.Level + 1f) % 40f;
                h.Gauge.Tick++;
                h.Gauge.Extra = !h.Gauge.Extra;
                h.Frame(d);
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(host.Inspector.Model.WatchCount).IsGreaterThan(0);
        await Assert.That(host.Inspector.Model.Flashes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task An_inspector_showing_a_selected_boxs_gaps_and_environment_as_its_items_come_and_go_every_frame_allocates_nothing()
    {
        var gauge = new Gauge();
        using var app = UiDriver.Render(ui => ui.Dial(gauge: gauge));
        using var host = Host.Over(app, gauge);
        host.Inspector.Selected = app.Get<Dial>();
        var revision = host.Inspector.Model.GapRevision;

        var bytes = app.WorstFrameBytes(
            host,
            static (d, h) =>
            {
                h.Gauge.Extra = !h.Gauge.Extra;
                h.Frame(d);
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(host.Inspector.Model.GapRevision - revision).IsGreaterThan(100);
        await Assert.That(host.Inspector.Model.SelectedEnvironment).IsNotNull();
    }

    sealed class Host : IDisposable
    {
        readonly GradientRampAtlas _ramps = new();
        readonly DrawList _list;

        Host(Inspector inspector, Gauge gauge)
        {
            Inspector = inspector;
            Gauge = gauge;
            _list = new DrawList(_ramps);
        }

        public Inspector Inspector { get; }

        public Gauge Gauge { get; }

        public static Host Over(UiDriver app, Gauge gauge)
        {
            var inspector = new Inspector(app.View, new NullHost());
            inspector.Resize(new Size(800f + Inspector.PanelWidth, 600f));
            app.Settle();
            return new Host(inspector, gauge);
        }

        public void Frame(UiDriver app)
        {
            app.Frame();
            Inspector.Update();
            _ramps.BeginFrame();
            _list.ClearPatchedRanges();
            Inspector.Render(_list);
        }

        public void Dispose() => Inspector.Dispose();
    }
}
