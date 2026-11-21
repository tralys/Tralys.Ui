using System.Numerics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Bgfx.Net;
using Tralys.Ui.Bgfx;
using Tralys.Ui.Components;
using Tralys.Ui.DevTools;
using Tralys.Ui.Docking;
using Tralys.Ui.Fonts;
using Tralys.Ui.FreeType;
using Tralys.Ui.Mvvm;
using Tralys.Ui.PackageSmoke.ViewModels;
using Tralys.Ui.Testing;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.PackageSmoke;

// Generated beside Tralys.Ui's generators, so a generator both packages shipped would emit Counter twice.
sealed partial class ShellViewModel : ObservableObject
{
    [ObservableProperty]
    public partial CounterViewModel? Counter { get; set; }
}

readonly record struct CounterProps
{
    public required ShellViewModel Shell { get; init; }
}

sealed class CounterView : Component<CounterProps>
{
    protected override void Render(Ui ui, in CounterProps p)
    {
        if (p.Shell.Counter is not { } counter)
            return;
        using (ui.Box(direction: Direction.Column, gap: 8, padding: 8))
        {
            ui.Tally(count: counter.Count, id: "count");
            ui.Progress(value: counter.Count, max: 10);
            ui.Button(text: "Add", onClick: counter.IncrementCommand, id: "add");
        }
    }
}

readonly record struct TallyProps
{
    public float Count { get; init; }
}

sealed class Tally : Component<TallyProps>
{
    protected override void Render(Ui ui, in TallyProps p) => ui.Text(ui.Format($"{p.Count}"));
}

static class Program
{
    static readonly TimeSpan FrameTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    static unsafe int Main()
    {
        Bx.RenderFrame(0);
        Init init;
        Bx.InitCtor(&init);
        init.Type = RendererType.Noop;
        if (!Bx.Init(&init))
        {
            Console.Error.WriteLine("bgfx could not start its no-op renderer.");
            return 1;
        }

        var shown = ShownAfterOneClick();
        Bx.Shutdown();
        if (shown != "1")
        {
            Console.Error.WriteLine($"The bound count shows {shown}, not 1.");
            return 1;
        }
        if (DrivenAfterOneClick() is var driven and not "1")
        {
            Console.Error.WriteLine($"The driven count shows {driven}, not 1.");
            return 1;
        }
        if (!DockRoundTrips())
        {
            Console.Error.WriteLine("The dock's layout did not save and load back.");
            return 1;
        }
        if (CallerPathFault() is { } fault)
        {
            Console.Error.WriteLine(fault);
            return 1;
        }
        Console.WriteLine(
            "A view built from the packages bound, commanded, drew and was inspected, and names no source file."
        );
        return 0;
    }

    static string ShownAfterOneClick()
    {
        using var backend = new FreeTypeBackend();
        using var fonts = new FontCollection(backend);
        using var text = new TextServices(
            fonts,
            fonts.Add(Embedded("Inter-Regular.otf")),
            new BgfxTextureFactory()
        );
        using var device = new BgfxRenderDevice();
        using var renderer = new BgfxViewRenderer(device);
        using var context = new UiContext(text);
        using var view = new UiView(context, new SmokeHost()) { Size = new Size(320, 120) };
        var shell = new ShellViewModel { Counter = new CounterViewModel() };
        var now = TimeSpan.Zero;
        context.Advance(now);
        view.Mount<CounterView, CounterProps>(new CounterProps { Shell = shell });

        void Frame()
        {
            now += FrameTime;
            context.Advance(now);
            view.Update();
            device.BeginFrame();
            renderer.Render(view, 0);
            Bx.Frame(0);
        }

        Frame();
        if (Find(view.Root, "add") is not { ViewBounds: var add })
            return "no Add button";
        var at = new Vector2(add.X + add.Width / 2f, add.Y + add.Height / 2f);
        view.PointerMove(Pointer.Mouse, at, MouseButtons.None, false, ModifierKeys.None, now);
        view.PointerDown(Pointer.Mouse, at, MouseButton.Left, ModifierKeys.None, now);
        view.PointerUp(Pointer.Mouse, at, MouseButton.Left, ModifierKeys.None, now);
        Frame();

        using var inspector = new Inspector(view, new SmokeHost());
        inspector.Update();
        return renderer.DrawList.QuadCount > 0 && HasText(inspector.View.Root)
            ? Find(view.Root, "count") is { } count
                ? Shown(count)
                : "no count"
            : "nothing drawn or inspected";
    }

    static string DrivenAfterOneClick()
    {
        var shell = new ShellViewModel { Counter = new CounterViewModel() };
        using var driver = UiDriver.Render(ui => ui.CounterView(shell: shell));
        driver.Click(driver.Get("add"));
        return Shown(driver.Get("count"));
    }

    static bool DockRoundTrips()
    {
        var dock = new DockManager();
        var tool = new DockElement("tool", "Tool");
        dock.Register(tool);
        dock.Dock(tool, DockPosition.Left);
        var layout = dock.SaveLayout();
        dock.LoadLayout(layout);
        return dock.SaveLayout() == layout && dock.LeftGroup.Elements.Count == 1;
    }

    static string? CallerPathFault()
    {
        // Both built at run time, so no literal of this method is what they find.
        var source = typeof(Program).Name + ".cs";
        var token = typeof(Program).Assembly.GetName().Name + "#";

        var image = File.ReadAllBytes(typeof(Program).Assembly.Location);
        if (image.AsSpan().IndexOf(MemoryMarshal.AsBytes(source.AsSpan())) >= 0)
            return $"The assembly still names {source}.";

        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        for (
            var handle = metadata.GetNextHandle(default(UserStringHandle));
            !handle.IsNil;
            handle = metadata.GetNextHandle(handle)
        )
        {
            if (metadata.GetUserString(handle).StartsWith(token, StringComparison.Ordinal))
                return null;
        }

        return $"No {token}N token reads back in place of {source}.";
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

    static string Shown(Node node)
    {
        if (node.Kind == NodeKind.Text)
            return node.Text.ToString();
        foreach (var child in node.Children)
        {
            if (Shown(child) is { Length: > 0 } text)
                return text;
        }
        return "";
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

    static byte[] Embedded(string name)
    {
        using var stream =
            typeof(Program).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"{name} is not embedded.");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}

sealed class SmokeHost : IViewHost
{
    public void SetCursor(Cursor? cursor) { }

    public string? GetClipboardText() => null;

    public void SetClipboardText(ReadOnlySpan<char> text) { }

    public void StartTextInput(TextInputKind kind, Rect area, bool byContact) { }

    public void StopTextInput() { }
}
