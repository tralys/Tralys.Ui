using System.Numerics;
using Tralys.Ui.Demo.Panel;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Demo.Tests;

public sealed class PanelTests
{
    [Test]
    public async Task A_click_on_the_turned_image_presses_the_panels_button_under_it()
    {
        using var panel = new TurnedPanel();

        panel.Driver.Click(panel.OnScreen("press"));

        await Assert.That(panel.Image.Model.Presses).IsEqualTo(1);
    }

    [Test]
    public async Task A_slider_dragged_off_the_panel_keeps_following_the_pointer()
    {
        using var panel = new TurnedPanel();

        var held = panel.Driver.Hold(panel.OnScreen("slider"));
        held.MoveBy(new Vector2(500f, 0f));
        held.Release();

        await Assert.That(panel.Image.Model.Value).IsEqualTo(1f);
    }

    [Test]
    public async Task A_right_press_and_release_during_a_drag_leaves_the_drag_running()
    {
        using var panel = new TurnedPanel();
        var thumb = panel.OnScreen("slider");

        var left = panel.Driver.Hold(thumb);
        panel.Driver.Hold(thumb, button: MouseButton.Right).Release();
        left.MoveBy(new Vector2(500f, 0f));
        left.Release();

        await Assert.That(panel.Image.Model.Value).IsEqualTo(1f);
    }

    [Test]
    public async Task The_window_losing_focus_mid_drag_ends_the_panels_drag()
    {
        using var panel = new TurnedPanel();

        var held = panel.Driver.Hold(panel.OnScreen("slider"));
        panel.Driver.WindowFocus(false);
        panel.Driver.WindowFocus(true);
        held.MoveBy(new Vector2(40f, 0f));

        await Assert.That(panel.Image.Model.Value).IsEqualTo(0.5f);
    }

    [Test]
    public async Task The_panels_cursor_shows_on_its_image_through_a_click_until_the_pointer_leaves_it()
    {
        using var panel = new TurnedPanel();
        var press = panel.OnScreen("press");

        MoveTo(panel.Driver, press);
        var over = panel.Driver.Cursor;
        panel.Driver.Click(press);
        var clicked = panel.Driver.Cursor;
        MoveTo(panel.Driver, Vector2.Zero);

        await Assert.That((over, clicked)).IsEqualTo((Cursors.Hand, Cursors.Hand));
        await Assert.That(panel.Driver.Cursor).IsNull();
    }

    static void MoveTo(UiDriver driver, Vector2 at)
    {
        driver.View.PointerMove(Pointer.Mouse, at, MouseButtons.None, false, default, default);
        driver.Settle();
    }

    sealed class TurnedPanel : IDisposable
    {
        public TurnedPanel()
        {
            Driver = UiDriver.Render(static _ => { }, size: new Size(1280, 720));
            Image = new PanelImage(Driver.View.Context, null);
            Driver.View.Mount(
                Slot.Of(Image, static (ui, image) => ui.PanelView(image: image, grow: 1, basis: 0))
            );
            Image.View.Update();
            Image.View.Render(new DrawList(new GradientRampAtlas()));
            Driver.Settle();
        }

        public UiDriver Driver { get; }

        public PanelImage Image { get; }

        public Vector2 OnScreen(string id)
        {
            var inner =
                Find(Image.View.Root, id)?.ViewBounds
                ?? throw new InvalidOperationException($"The panel has no node '{id}'.");
            var image = Driver.Get("panel-image").ViewBounds;
            var centre = new Vector2(image.X + image.Width / 2f, image.Y + image.Height / 2f);
            var unturned = new Vector2(
                image.X + inner.X + inner.Width / 2f,
                image.Y + inner.Y + inner.Height / 2f
            );
            return Vector2.Transform(
                unturned,
                Matrix3x2.CreateRotation(PanelImage.TurnDegrees * MathF.PI / 180f, centre)
            );
        }

        static Node? Find(Node node, string id)
        {
            foreach (var child in node.Children)
            {
                if (child.Id == id)
                    return child;
                if (Find(child, id) is { } found)
                    return found;
            }
            return null;
        }

        public void Dispose()
        {
            Driver.Dispose();
            Image.Dispose();
        }
    }
}
