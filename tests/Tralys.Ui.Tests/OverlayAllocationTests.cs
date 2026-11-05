using System.Numerics;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class OverlayAllocationTests
{
    static readonly Slot Body = Slot.Of(static ui => ui.Text("Only online contacts"));

    [Test]
    public async Task Popovers_and_dialogs_opened_and_dismissed_over_and_over_allocate_nothing()
    {
        using var driver = UiDriver.Render(ui =>
        {
            var popover = ui.State(false);
            var dialog = ui.State(false);
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 10))
            {
                using (ui.Popover(open: popover.Value, onOpenChange: popover.Setter, content: Body))
                {
                    using (ui.Box(width: 60, height: 20, onClick: popover.Toggle(), id: "trigger"))
                    { }
                }
                using (ui.Box(width: 60, height: 20, onClick: dialog.Toggle(), id: "open")) { }
                using (ui.Dialog(open: dialog.Value, onOpenChange: dialog.Setter))
                    ui.DialogClose();
            }
        });
        var churn = new Churn(driver.Get("trigger"), driver.Get("open"));
        for (var i = 0; i < 16; i++)
            Churn.Step(driver, churn);

        var bytes = driver.WorstFrameBytes(churn, Churn.Step);

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_positioner_following_an_anchor_that_moves_every_frame_allocates_nothing()
    {
        var shift = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            var anchor = ui.Ref();
            var step = shift.Count;
            using (
                ui.Box(
                    width: 20 + step / 8 % 2 * 10,
                    height: 20,
                    margin: new Thickness(step / 4 % 5 * 10, 0, 0, 0),
                    transform: Transform.Translate(step % 7, 0),
                    nodeRef: anchor,
                    id: "anchor"
                )
            ) { }
            using (ui.Positioner(anchor: anchor, sideOffset: 2, id: "placed"))
            {
                using (ui.Box(width: 30, height: 30)) { }
            }
        });
        var follow = new Follow(shift, driver.Get("anchor"), driver.Get("placed"));

        var bytes = driver.WorstFrameBytes(
            follow,
            static (d, f) =>
            {
                f.Model.Count++;
                d.Frame();
                if (f.Placed.ViewBounds.X != f.Anchor.ViewBounds.X + f.Model.Count % 7)
                    f.Wrong++;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(follow.Wrong).IsEqualTo(0);
    }

    sealed class Follow(Model model, Node anchor, Node placed)
    {
        public readonly Model Model = model;
        public readonly Node Anchor = anchor;
        public readonly Node Placed = placed;
        public int Wrong;
    }

    sealed class Churn(Node trigger, Node open)
    {
        readonly Node _trigger = trigger;
        readonly Node _open = open;
        int _step;

        public static void Step(UiDriver driver, Churn churn)
        {
            switch (churn._step++ % 8)
            {
                case 0:
                    driver.Click(churn._trigger, settle: false);
                    break;
                case 2:
                    driver.Click(new Vector2(700, 500), settle: false);
                    break;
                case 4:
                    driver.Click(churn._open, settle: false);
                    break;
                case 6:
                    driver.Press(Key.Escape, settle: false);
                    break;
            }
            driver.Frame();
        }
    }
}
