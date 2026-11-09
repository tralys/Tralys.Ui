using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using static Tralys.Ui.Tests.AnimatedLooks;

namespace Tralys.Ui.Tests;

public sealed class ToastTests
{
    [Test]
    public async Task A_shown_toast_reports_its_dismissal_its_fade_before_its_time_is_up_and_is_gone_once_it_is()
    {
        var toasts = new ObservableList<string>();
        using var driver = Show(toasts, duration: 1f, own: "Lost", seconds: 2f);
        toasts.Add("Saved");
        toasts.Add("Lost");

        driver.Frame();
        var first = Opacity(Toast(driver, "Saved"));
        driver.Frame(0.84f);
        var early = (Toast(driver, "Saved").Exiting, Opacity(Toast(driver, "Saved")));
        driver.Frame(0.02f);
        var leaving = (toasts.Count, Toast(driver, "Saved").Exiting);
        driver.Frame(0.13f);
        var last = Opacity(Toast(driver, "Saved"));
        driver.Frame(0.03f);
        var kept = (driver.All<Toast>().Count, Toast(driver, "Lost").Exiting);
        driver.Frame(1f);
        driver.Frame(0.2f);

        await Assert.That(first).IsEqualTo(1f);
        await Assert.That(early).IsEqualTo((false, 1f));
        await Assert.That(leaving).IsEqualTo((1, true));
        await Assert.That(last).IsGreaterThan(0f).And.IsLessThan(1f);
        await Assert.That(kept).IsEqualTo((1, false));
        await Assert.That(toasts.Count).IsEqualTo(0);
        await Assert.That(driver.All<Toast>().Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_toast_shown_before_any_toaster_gets_its_whole_default_4_seconds_from_when_one_first_shows_it()
    {
        var toasts = new ObservableList<string> { "Saved" };
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            if (model.Remember)
                using (ui.Toaster())
                    for (var i = 0; i < toasts.Count; i++)
                        ui.Toast(
                            title: toasts[i],
                            onDismiss: Dismiss(toasts, toasts[i]),
                            key: toasts[i]
                        );
        });

        driver.Frame(10f);
        model.Remember = true;
        driver.Frame();
        driver.Frame(3.84f);
        var showing = (toasts.Count, Toast(driver, "Saved").Exiting);
        driver.Frame(0.02f);

        await Assert.That(showing).IsEqualTo((1, false));
        await Assert.That(toasts.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Hovering_or_clicking_a_toast_reaches_what_lies_beneath_it_and_neither_pauses_nor_dismisses_it()
    {
        var toasts = new ObservableList<string>();
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box())
            {
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 0,
                        top: 0,
                        width: 800,
                        height: 600,
                        onClick: Callback.Of(clicks, static c => c.Value++),
                        id: "under"
                    )
                ) { }
                using (ui.Toaster(duration: 1f))
                    for (var i = 0; i < toasts.Count; i++)
                        ui.Toast(
                            title: toasts[i],
                            onDismiss: Dismiss(toasts, toasts[i]),
                            key: toasts[i]
                        );
            }
        });
        toasts.Add("Saved");
        driver.Frame();
        var toast = Root(Toast(driver, "Saved"));
        var bounds = toast.ViewBounds;

        driver.Hover(toast, settle: false);
        var hovered = (driver.Get("under").States & InputStates.Hovered) != 0;
        driver.Click(
            new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f),
            settle: false
        );
        var shown = toasts.Count;
        driver.Frame(1f);

        await Assert.That(hovered).IsTrue();
        await Assert.That(clicks.Value).IsEqualTo(1);
        await Assert.That(shown).IsEqualTo(1);
        await Assert.That(toasts.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_toaster_stacks_its_toasts_16_in_from_the_side_and_end_its_placement_names()
    {
        var placement = PlacementMode.TopEnd;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Toaster(placement: placement))
            {
                ui.Toast(title: "Saved", key: 1);
                ui.Toast(title: "Lost", key: 2);
            }
        });

        Rect Stack()
        {
            driver.Rerender();
            return Toast(driver, "Saved").Parent!.ViewBounds;
        }

        var topEnd = Stack();
        placement = PlacementMode.BottomStart;
        var bottomStart = Stack();

        await Assert.That((topEnd.Y, topEnd.Right)).IsEqualTo((16f, 784f));
        await Assert.That((bottomStart.Bottom, bottomStart.X)).IsEqualTo((584f, 16f));
    }

    [Test]
    public async Task A_toaster_or_toast_given_no_time_to_show_refuses_it()
    {
        var faults = new List<Exception>();
        float? toaster = 1f;
        float? toast = null;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Toaster(duration: toaster))
                ui.Toast(title: "Saved", duration: toast);
        });
        driver.View.Context.OnFault = faults.Add;

        toaster = 0f;
        driver.Rerender();
        toaster = null;
        toast = -1f;
        driver.Rerender();

        await Assert
            .That(faults.Select(f => (f.GetType(), ((ArgumentException)f).ParamName ?? "")))
            .IsEquivalentTo([
                (typeof(ArgumentOutOfRangeException), "Duration"),
                (typeof(ArgumentOutOfRangeException), "Duration"),
            ]);
    }

    [Test]
    public async Task A_toast_on_the_skins_toast_frame_sits_inside_its_slice_with_no_card_of_its_own()
    {
        using var driver = UiDriver.Render(ui => ui.Toast(title: "Saved", id: "toast")).WithIcons();
        driver.View.Context.Skin = new Skin([
            new(
                SkinPart.ToastFrame,
                new SkinPartImage(new TestTexture(24, 24), new Thickness(6), Scale: 2f)
            ),
        ]);
        driver.Settle();
        var toast = driver.Get("toast");
        var card = Nodes.Find(toast, NodeKind.Box);

        await Assert
            .That(Nodes.Find(toast, NodeKind.Image).Bounds.Size)
            .IsEqualTo(toast.Bounds.Size);
        await Assert.That((card.Bounds.X, card.Bounds.Y)).IsEqualTo((12f, 12f));
        await Assert
            .That(
                (
                    card.Visual.Background,
                    card.Visual.BorderPaint,
                    card.Visual.Radius,
                    card.Visual.Shadow
                )
            )
            .IsEqualTo((default(Paint), default(Paint), default(CornerRadius), default(BoxShadow)));
    }

    static UiDriver Show(
        ObservableList<string> toasts,
        float duration,
        string own,
        float seconds
    ) =>
        UiDriver.Render(ui =>
        {
            using (ui.Toaster(duration: duration))
                for (var i = 0; i < toasts.Count; i++)
                    ui.Toast(
                        title: toasts[i],
                        duration: toasts[i] == own ? seconds : null,
                        onDismiss: Dismiss(toasts, toasts[i]),
                        key: toasts[i]
                    );
        });

    static Callback Dismiss(ObservableList<string> toasts, string title) =>
        Callback.Of(toasts, title, static (all, title) => all.Remove(title));

    static Node Toast(UiDriver driver, string title)
    {
        foreach (var toast in driver.All<Toast>())
        {
            if (toast.Props<ToastProps>().Title.Span.SequenceEqual(title))
                return toast;
        }
        throw new InvalidOperationException($"No toast tells '{title}'.");
    }

    static float Opacity(Node toast) => Root(toast).Visual.Opacity ?? 1f;
}
