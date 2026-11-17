using System.Numerics;
using Bgfx.Net;
using Tralys.Ui.Bgfx;
using Tralys.Ui.DevTools;
using Tralys.Ui.Testing;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.AotSmoke;

static class Program
{
    const int Width = 1280;
    const int Height = 720;
    const float Scale = 1.25f;

    static readonly TimeSpan FrameTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    static unsafe int Main()
    {
        Bx.RenderFrame(0);
        Init init;
        Bx.InitCtor(&init);
        init.Type = RendererType.Noop;
        init.SwapChain.Width = 0;
        init.SwapChain.Height = 0;
        if (!Bx.Init(&init))
        {
            Console.Error.WriteLine("bgfx could not start its no-op renderer.");
            return 1;
        }

        var device = new BgfxRenderDevice();
        var renderer = new BgfxViewRenderer(device);
        var fonts = new SmokeFonts(new BgfxTextureFactory());
        var context = new UiContext(fonts.Services);
        var view = new UiView(context, new SmokeHost(), Scale)
        {
            Size = new Size(Width / Scale, Height / Scale),
        };
        var model = new SmokeModel();
        var now = TimeSpan.Zero;
        context.Advance(now);
        view.Mount<SmokeRoot, SmokeProps>(new SmokeProps { Model = model });
        view.Update();

        var failures = 0;
        void Require(bool ok, string why)
        {
            if (ok)
                return;
            Console.Error.WriteLine(why);
            failures++;
        }

        var toggle = Find(view.Root, "switch");
        Require(toggle is not null, "The switch was not mounted.");
        var button = Find(view.Root, "button");
        Require(button is not null, "The button was not mounted.");
        Require(
            view.Layers.Length == 3,
            "The combobox or the tooltip did not open its popup, or the suggestion input did not mount its own."
        );
        var bounds = toggle?.ViewBounds ?? default;
        var at = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f) * Scale;
        var saveBounds = button?.ViewBounds ?? default;
        var save =
            new Vector2(saveBounds.X + saveBounds.Width / 2f, saveBounds.Y + saveBounds.Height / 2f)
            * Scale;
        var tick = 0;
        var flips = 0;
        var animating = 0;

        void Frame()
        {
            now += FrameTime;
            context.Advance(now);
            if (++tick % 20 == 0)
            {
                var before = model.On;
                view.PointerMove(
                    Pointer.Mouse,
                    at,
                    MouseButtons.None,
                    false,
                    ModifierKeys.None,
                    now
                );
                view.PointerDown(Pointer.Mouse, at, MouseButton.Left, ModifierKeys.None, now);
                view.PointerUp(Pointer.Mouse, at, MouseButton.Left, ModifierKeys.None, now);
                flips += model.On != before ? 1 : 0;
            }
            else if (tick % 20 == 10)
            {
                view.PointerMove(
                    Pointer.Mouse,
                    save,
                    MouseButtons.None,
                    false,
                    ModifierKeys.None,
                    now
                );
                view.PointerDown(Pointer.Mouse, save, MouseButton.Left, ModifierKeys.None, now);
                view.PointerUp(Pointer.Mouse, save, MouseButton.Left, ModifierKeys.None, now);
            }
            model.Tick++;
            view.Update();
            animating += view.IsIdle ? 0 : 1;
            device.BeginFrame();
            renderer.Render(view, 0);
            Bx.Frame(0);
        }

        var worst = AllocationGate.WorstFrameBytes(Frame);

        Require(flips > 0, "Clicking the switch did not report its flipped value.");
        Require(model.Clicks > 0, "Clicking the button did not run its onClick.");
        Require(animating > 0, "The switch's thumb and the box's colour never animated.");
        Require(
            model.HighestPulse - model.LowestPulse > 0.5f,
            "The loop did not run its phase through the cycle."
        );
        Require(model.Draws > 0, "The canvas never drew.");
        Require(renderer.DrawList.QuadCount > 0, "The view drew nothing.");
        var layout = model.Dock.SaveLayout();
        model.Dock.LoadLayout(layout);
        Require(
            model.Dock.SaveLayout() == layout && model.Dock.LeftGroup.Elements.Count == 1,
            "The dock's layout did not save and load back."
        );

        using (var inspector = new Inspector(view, new SmokeHost()))
        {
            inspector.Update();
            inspector.Selected = toggle;
            inspector.Update();
            Require(HasText(inspector.View.Root), "The inspector did not show the target's tree.");
        }

        view.Dispose();
        context.Dispose();
        fonts.Dispose();
        renderer.Dispose();
        device.Dispose();
        Bx.Shutdown();

        Console.WriteLine(
            $"{renderer.DrawList.QuadCount} quads a frame; the worst frame allocated {worst} B."
        );
        return worst == 0 && failures == 0 ? 0 : 1;
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

    static bool HasText(Node node)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind == NodeKind.Text || HasText(child))
                return true;
        }
        return false;
    }
}
