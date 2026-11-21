using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components.Tests;

public sealed partial class OverlayFrameTests
{
    [Test]
    public async Task A_frame_with_no_inset_lays_its_content_at_its_edge_and_clips_it()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (ui.OverlayFrame(inset: OverlayInset.None))
            using (ui.Box(width: 100, height: 50, id: "body")) { }
        });
        var frame = driver.RootOf<Overlay.Frame>();
        var body = driver.Get("body").ViewBounds;

        await Assert
            .That((body.X - frame.ViewBounds.X, frame.Layout.Overflow))
            .IsEqualTo((1f, Overflow.Clip));
    }

    [Test]
    public async Task A_frame_fills_with_the_overlay_fill_inside_a_1_unit_overlay_edge_and_casts_the_themes_shadow_unless_given_its_own()
    {
        var model = new FrameModel();
        var own = new BoxShadow(0f, 8f, 24f, 0f, Color.FromArgb(64, 0, 0, 0));
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.OverlayFrame(shadow: model.Own ? own : null))
                ui.Text("Saved");
        });
        var t = Themes.Dark;
        var frame = driver.RootOf<Overlay.Frame>();
        var look = (
            frame.Visual.Background,
            frame.Layout.Border,
            frame.Visual.BorderPaint,
            frame.Visual.Radius,
            frame.Visual.Shadow
        );

        model.Own = true;
        driver.Settle();

        await Assert
            .That(look)
            .IsEqualTo(
                (
                    (Paint)t.Color(ColorToken.BgOverlay),
                    new Thickness(1f),
                    (Paint)t.Color(ColorToken.BorderOverlay),
                    t.Dimensions(Density.Pointer).Radius(RadiusToken.Md),
                    new BoxShadow(0f, 2f, 10f, 0f, t.Color(ColorToken.BgShadow).WithOpacity(0.6f))
                )
            );
        await Assert.That(frame.Visual.Shadow).IsEqualTo(own);
    }

    [Test]
    public async Task A_frame_pads_rows_by_4_text_as_a_control_of_its_rung_and_a_block_by_its_rungs_inset_and_tells_its_rows_its_rung()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 24))
            {
                using (ui.OverlayFrame())
                    ui.RungProbe(id: "list");
                using (ui.OverlayFrame(inset: OverlayInset.Text, size: ComponentSize.Sm))
                    ui.RungProbe(id: "text");
                using (ui.OverlayFrame(inset: OverlayInset.Block, size: ComponentSize.Lg))
                    ui.RungProbe(id: "block");
            }
        });
        var sizes = Themes.Dark.Dimensions(Density.Pointer);

        await Assert.That(Offset(driver, "list")).IsEqualTo(new Thickness(5f));
        await Assert
            .That(Offset(driver, "text"))
            .IsEqualTo(Grown(sizes.Size(ComponentSize.Sm).Padding));
        await Assert
            .That(Offset(driver, "block"))
            .IsEqualTo(Grown(sizes.Size(ComponentSize.Lg).Inset));
        await Assert.That(driver.GetText(nameof(ComponentSize.Md))).IsNotNull();
        await Assert.That(driver.GetText(nameof(ComponentSize.Sm))).IsNotNull();
        await Assert.That(driver.GetText(nameof(ComponentSize.Lg))).IsNotNull();
    }

    static Thickness Grown(Thickness padding) =>
        new(padding.Left + 1f, padding.Top + 1f, padding.Right + 1f, padding.Bottom + 1f);

    static Thickness Offset(UiDriver driver, string id)
    {
        var probe = driver.Get(id);
        var edged = probe.Parent;
        while (edged is not null && !(edged.Kind == NodeKind.Box && edged.Layout.Border.Left > 0f))
            edged = edged.Parent;
        if (edged is null)
            throw new InvalidOperationException($"No edged box holds '{id}'.");
        var frame = edged.ViewBounds;
        var inner = probe.ViewBounds;
        return new Thickness(
            inner.X - frame.X,
            inner.Y - frame.Y,
            frame.Right - inner.Right,
            frame.Bottom - inner.Bottom
        );
    }

    sealed partial class FrameModel : ObservableObject
    {
        [ObservableProperty]
        public partial bool Own { get; set; }
    }
}

internal readonly record struct RungProbeProps;

internal sealed class RungProbe : Component<RungProbeProps>
{
    protected override void Render(Ui ui, in RungProbeProps p)
    {
        var size = ui.Use(Overlay.Frame.Context).Size;
        using (ui.Box(width: 40, height: 20))
            ui.Text(Names[(int)size]);
    }

    static readonly string[] Names = Enum.GetNames<ComponentSize>();
}
