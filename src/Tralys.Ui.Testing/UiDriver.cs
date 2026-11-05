using System.Collections.Concurrent;
using System.Numerics;
using Tralys.Ui.Fonts;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Testing;

/// <summary>Mounts UI on a view of its own and drives it as a host would: frames, input through real hit testing, and
/// queries over the node tree. It belongs to the thread that made it.</summary>
public sealed partial class UiDriver : IDisposable
{
    readonly UiContext _context;
    readonly TestHost _host = new();
    readonly GradientRampAtlas _ramps = new();
    readonly DrawList _list;
    TimeSpan _time;
    int _frame;

    UiDriver(
        Action<Ui> root,
        Size size,
        Theme? theme,
        Density density,
        float scale,
        ITextureProvider? textures,
        TextServices? text
    )
    {
        _context = new UiContext(text ?? TestFonts.ForThisThread(), textures, theme, density);
        View = new UiView(_context, _host, scale)
        {
            Size = size == default ? new Size(800f, 600f) : size,
        };
        _list = new DrawList(_ramps);
        _context.Advance(_time);
        View.Mount(Slot.Of(root));
    }

    /// <summary>Mounts <paramref name="root"/>, lays it out and settles before returning.</summary>
    /// <param name="root">The root's content; it may capture, since it runs only on mount and on <see cref="Rerender"/>.</param>
    /// <param name="size">The view's size; 800 × 600 by default.</param>
    /// <param name="theme">The context's theme; dark by default.</param>
    /// <param name="density">The context's density.</param>
    /// <param name="scale">Device pixels per layout unit.</param>
    /// <param name="textures">What the context resolves image keys through; none by default.</param>
    /// <param name="text">The app's fonts, so text lays out to its metrics; by default the test face, Inter. The driver
    /// uses it on its own thread and leaves it undisposed.</param>
    public static UiDriver Render(
        Action<Ui> root,
        Size size = default,
        Theme? theme = null,
        Density density = default,
        float scale = 1f,
        ITextureProvider? textures = null,
        TextServices? text = null
    )
    {
        ArgumentNullException.ThrowIfNull(root);
        var driver = new UiDriver(root, size, theme, density, scale, textures, text);
        driver.Settle();
        return driver;
    }

    /// <summary>Runs <paramref name="test"/> with a driver on a thread of its own, which pumps a synchronization
    /// context so every await in the test resumes there. The rest is as <see cref="Render"/> takes it.</summary>
    public static Task Run(
        Action<Ui> root,
        Func<UiDriver, Task> test,
        Size size = default,
        Theme? theme = null,
        Density density = default,
        float scale = 1f,
        ITextureProvider? textures = null,
        TextServices? text = null
    )
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(test);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var pump = new Pump();
            SynchronizationContext.SetSynchronizationContext(pump);
            try
            {
                using (var driver = Render(root, size, theme, density, scale, textures, text))
                {
                    var running = test(driver);
                    running.ContinueWith(
                        static (_, state) => ((Pump)state!).Complete(),
                        pump,
                        TaskScheduler.Default
                    );
                    pump.Run();
                    running.GetAwaiter().GetResult();
                }
                done.SetResult();
            }
            catch (Exception e)
            {
                done.SetException(e);
            }
            finally
            {
                TestFonts.ReleaseThread();
            }
        })
        {
            IsBackground = true,
            Name = "UiDriver",
        };
        thread.Start();
        return done.Task;
    }

    public UiView View { get; }

    public Node Root => View.Root;

    public Node? Focused => View.Focused;

    /// <summary>The cursor the view last asked its host for.</summary>
    public Cursor? Cursor => _host.Cursor;

    /// <summary>The host's clipboard text.</summary>
    public string? Clipboard
    {
        get => _host.Clipboard;
        set => _host.Clipboard = value;
    }

    /// <summary>The text input the view asked its host to run, or null while none runs.</summary>
    public TextInputState? TextInput => _host.TextInput;

    /// <summary>The view-pixel rects the view last asked its host to keep system edge gestures off.</summary>
    public ReadOnlySpan<Rect> SystemGestureExclusions => _host.Exclusions;

    /// <summary>How many times the view has sent its host <see cref="SystemGestureExclusions"/>.</summary>
    public int SystemGestureExclusionsSent => _host.ExclusionsSent;

    /// <summary>What the last frame did.</summary>
    public FrameStats Stats { get; private set; }

    /// <summary>Renders the root again, as when what its delegate captured changed, then settles.</summary>
    public void Rerender()
    {
        View.VerifyAccess();
        View.RerenderRoot();
        Settle();
    }

    /// <summary>One host frame: moves the clock on by <paramref name="seconds"/>, updates the view and renders it.</summary>
    public void Frame(float seconds = 1f / 60f)
    {
        View.VerifyAccess();
        _time += TimeSpan.FromSeconds(seconds);
        _context.Advance(_time);
        View.Stats.Clear();
        View.Update();
        _ramps.BeginFrame();
        _list.ClearPatchedRanges();
        View.Render(_list);
        var stats = View.Stats;
        Stats = new FrameStats(
            stats.Renders,
            stats.NodeWrites,
            stats.Mounts,
            stats.Unmounts,
            stats.Layouts,
            stats.Paints,
            stats.Rebuilt
        )
        {
            View = View,
            Frame = ++_frame,
        };
    }

    /// <summary>Runs frames until two in a row leave nothing to do; a forever loop doesn't hold it up.</summary>
    /// <exception cref="InvalidOperationException">The view was still busy after <paramref name="maxFrames"/>.</exception>
    public void Settle(int maxFrames = 600)
    {
        var idle = 0;
        for (var i = 0; i < maxFrames; i++)
        {
            Frame();
            idle = !View.HasWork ? idle + 1 : 0;
            if (idle == 2)
                return;
        }
        throw new InvalidOperationException($"The view was still busy after {maxFrames} frames.");
    }

    /// <summary>The one node whose id is <paramref name="id"/>.</summary>
    public Node Get(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Single(Root, static (node, id) => node.Id == id, id, $"id '{id}'");
    }

    /// <summary>The one text node whose text is exactly <paramref name="text"/>.</summary>
    public Node GetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Single(
            Root,
            static (node, text) => node.Kind == NodeKind.Text && node.Text.SequenceEqual(text),
            text,
            $"text '{text}'"
        );
    }

    /// <summary>The one component node of <typeparamref name="TComponent"/> whose root has <paramref name="id"/>, or
    /// the one there is.</summary>
    public Node Get<TComponent>(string? id = null) =>
        Single(
            Root,
            static (node, id) => IsComponent<TComponent>(node, id),
            id,
            $"{typeof(TComponent).Name} '{id}'"
        );

    public IReadOnlyList<Node> All<TComponent>()
    {
        var found = new List<Node>();
        Collect(Root, static (node, id) => IsComponent<TComponent>(node, id), (string?)null, found);
        return found;
    }

    /// <summary>An id for a mounted component that stays while it stays mounted; a remount gets a new one.</summary>
    public long MountId(Node componentNode)
    {
        ArgumentNullException.ThrowIfNull(componentNode);
        return componentNode.Host?.MountId
            ?? throw new ArgumentException("The node hosts no component.", nameof(componentNode));
    }

    /// <summary>The worst bytes one call of <paramref name="frame"/> allocates on this thread once warm, as
    /// <see cref="AllocationGate"/> measures; <paramref name="frame"/> calls <see cref="Frame"/> itself.</summary>
    public long WorstFrameBytes(Action<UiDriver> frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return AllocationGate.WorstFrameBytes(() => frame(this));
    }

    public long WorstFrameBytes<TState>(TState state, Action<UiDriver, TState> frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return AllocationGate.WorstFrameBytes(() => frame(this, state));
    }

    /// <summary>Presses and releases <paramref name="button"/> at the centre of <paramref name="target"/>'s visible
    /// bounds, through real hit testing.</summary>
    /// <exception cref="InvalidOperationException">The press would land on a node unrelated to the target.</exception>
    public void Click(
        Node target,
        MouseButton button = MouseButton.Left,
        ModifierKeys modifiers = default,
        bool settle = true
    )
    {
        var node = Acted(target);
        var visible = VisibleBounds(node);
        ClickAt(
            node,
            new Vector2(visible.X + visible.Width / 2f, visible.Y + visible.Height / 2f),
            button,
            modifiers,
            settle
        );
    }

    /// <summary>Clicks <paramref name="offset"/> from the top left of <paramref name="target"/>'s view bounds.</summary>
    public void Click(Node target, Vector2 offset, bool settle = true)
    {
        var node = Acted(target);
        var bounds = node.ViewBounds;
        ClickAt(node, new Vector2(bounds.X, bounds.Y) + offset, MouseButton.Left, default, settle);
    }

    /// <summary>Clicks at <paramref name="viewPoint"/>, in layout units, whatever is there.</summary>
    public void Click(Vector2 viewPoint, bool settle = true) =>
        ClickAt(null, viewPoint, MouseButton.Left, default, settle);

    /// <summary>Moves the mouse to the centre of <paramref name="target"/>'s visible bounds.</summary>
    public void Hover(Node target, bool settle = true)
    {
        var visible = VisibleBounds(Acted(target));
        View.PointerMove(
            Pointer.Mouse,
            Pixels(new Vector2(visible.X + visible.Width / 2f, visible.Y + visible.Height / 2f)),
            MouseButtons.None,
            false,
            default,
            _time
        );
        if (settle)
            Settle();
    }

    /// <summary>Moves the mouse out of the view.</summary>
    public void Unhover(bool settle = true)
    {
        View.PointerLeave(Pointer.Mouse);
        if (settle)
            Settle();
    }

    /// <summary>Sends <paramref name="key"/> down then up.</summary>
    public void Press(Key key, ModifierKeys modifiers = default, bool settle = true)
    {
        View.KeyDown(key, modifiers, false);
        View.KeyUp(key, modifiers);
        if (settle)
            Settle();
    }

    /// <summary>Sends <paramref name="text"/> as one text input, with no key events.</summary>
    public void Type(string text, bool settle = true)
    {
        ArgumentNullException.ThrowIfNull(text);
        View.TextInput(text);
        if (settle)
            Settle();
    }

    /// <summary>Focuses <paramref name="target"/> from code.</summary>
    /// <returns>Whether it took focus.</returns>
    public bool Focus(Node target, bool settle = true)
    {
        var node = Acted(target);
        var focused = View.Focus(node, FocusCause.Code, false);
        if (settle)
            Settle();
        return focused;
    }

    /// <summary>What the view draws as it stands, for goldens and comparisons.</summary>
    public DrawCommandStream Draw()
    {
        View.VerifyAccess();
        return DrawCommandStream.Of(View);
    }

    /// <summary>Holds <paramref name="states"/> on the components at and inside <paramref name="node"/>.</summary>
    public void ForceStates(Node node, InputStates states, bool settle = true)
    {
        View.ForceStates(Acted(node), states);
        if (settle)
            Settle();
    }

    void ClickAt(
        Node? target,
        Vector2 point,
        MouseButton button,
        ModifierKeys modifiers,
        bool settle
    )
    {
        View.VerifyAccess();
        var pixels = Pixels(point);
        if (target is not null && View.HitTest(pixels) is { } hit && !Related(hit, target))
            throw new InvalidOperationException(
                $"A click at {point} on {Describe(target)} would land on {Describe(hit)}, which is neither it nor its ancestor or descendant."
            );
        View.PointerMove(Pointer.Mouse, pixels, MouseButtons.None, false, modifiers, _time);
        View.PointerDown(Pointer.Mouse, pixels, button, modifiers, _time);
        View.PointerUp(Pointer.Mouse, pixels, button, modifiers, _time);
        if (settle)
            Settle();
    }

    Vector2 Pixels(Vector2 point) => point * View.Scale;

    static bool Related(Node hit, Node target) =>
        hit.IsAncestorOrSelfOf(target) || target.IsAncestorOrSelfOf(hit);

    static Node Acted(Node target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var node = target;
        while (node.Kind == NodeKind.Component && node.Host?.Root is { } root)
            node = root;
        return node;
    }

    static Rect VisibleBounds(Node node)
    {
        var visible = node.ViewBounds;
        for (
            var a = node.Kind == NodeKind.Layer ? null : node.Parent;
            a is not null;
            a = a.Kind == NodeKind.Layer ? null : a.Parent
        )
        {
            if (a.IsTransparent || a.Layout.Overflow == Overflow.Visible)
                continue;
            visible = visible.Intersect(a.ViewBounds.Deflate(a.Layout.Border));
        }
        if (visible.IsEmpty)
            throw new InvalidOperationException($"{Describe(node)} shows nothing to click.");
        return visible;
    }

    static string Describe(Node node) =>
        node.Id is { } id ? $"'{id}'"
        : node.Kind == NodeKind.Text ? $"text '{node.Text}'"
        : $"{node.Kind} at {node.ViewBounds}";

    static bool IsComponent<TComponent>(Node node, string? id) =>
        node.Kind == NodeKind.Component
        && node.Component == typeof(TComponent)
        && (id is null || node.Host?.Root?.Id == id);

    static Node Single<TState>(Node root, Func<Node, TState, bool> match, TState state, string what)
    {
        var found = new List<Node>();
        Collect(root, match, state, found);
        return found.Count == 1
            ? found[0]
            : throw new InvalidOperationException(
                $"Expected one node with {what}, found {found.Count}."
            );
    }

    static void Collect<TState>(
        Node node,
        Func<Node, TState, bool> match,
        TState state,
        List<Node> found
    )
    {
        foreach (var child in node.Children)
        {
            if (child.SpareRow)
                continue;
            if (match(child, state))
                found.Add(child);
            Collect(child, match, state, found);
        }
    }

    public void Dispose()
    {
        View.Dispose();
        _context.Dispose();
    }

    sealed class Pump : SynchronizationContext
    {
        readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _work = [];

        public override void Post(SendOrPostCallback d, object? state) => _work.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) =>
            throw new NotSupportedException("The driver's thread runs posted work only.");

        public void Complete() => _work.CompleteAdding();

        public void Run()
        {
            foreach (var (callback, state) in _work.GetConsumingEnumerable())
                callback(state);
        }
    }
}

/// <summary>The text input a view asked its host to run: the keyboard's kind, the caret's area in view pixels, whether a
/// touch or pen asked, and how many times it was started since none ran.</summary>
public readonly record struct TextInputState(
    TextInputKind Kind,
    Rect Area,
    bool ByContact,
    int Starts
);

/// <summary>What one frame did: host renders, nodes whose props changed, mounts and unmounts, nodes laid out without a
/// cache hit, nodes recorded again, and whether the draw list was rebuilt rather than patched or reused.</summary>
public readonly record struct FrameStats(
    int Renders,
    int NodeWrites,
    int Mounts,
    int Unmounts,
    int Layouts,
    int Paints,
    bool DrawListRebuilt
)
{
    internal UiView? View { get; init; }

    internal int Frame { get; init; }

    /// <summary>How many times hosts of <typeparamref name="TComponent"/> rendered.</summary>
    public int RendersOf<TComponent>() => View?.Stats.RendersOf(ComponentType<TComponent>.Id) ?? 0;
}
