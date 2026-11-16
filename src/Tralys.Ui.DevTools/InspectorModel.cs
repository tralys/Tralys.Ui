using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.DevTools;

internal readonly record struct BoxModel(
    Rect Bounds,
    Thickness Margin,
    Thickness Border,
    Thickness Padding
)
{
    public Rect MarginBox =>
        new(
            Bounds.X - Margin.Left,
            Bounds.Y - Margin.Top,
            Bounds.Width + Margin.Left + Margin.Right,
            Bounds.Height + Margin.Top + Margin.Bottom
        );
}

internal readonly record struct ResolvedEnvironment(
    ResolvedStyle Style,
    bool Disabled,
    InputStates Held,
    bool Exiting
)
{
    public Theme Theme => Style.Theme;
}

internal readonly record struct Flash(Node Node, Rect Box, int Age);

internal readonly record struct Shown(
    Rect Bounds,
    Thickness Padding,
    Thickness Border,
    Thickness Margin,
    InputStates States,
    bool Hidden,
    bool Collapsed,
    int Renders,
    int Sources
);

internal sealed partial class InspectorModel : ObservableObject
{
    internal const int FlashFrames = 24;
    internal const int RecentFrames = 60;
    const int MostFlashes = 256;

    internal static readonly float[] Speeds = [1f, 0.5f, 0.25f, 0.1f, 0f];
    internal static readonly string[] SpeedNames =
    [
        "Speed 1×",
        "Speed 0.5×",
        "Speed 0.25×",
        "Speed 0.1×",
        "Paused",
    ];

    readonly UiView? _target;
    readonly NodeRef? _rootRef;
    float _speedBefore = 1f;
    readonly Dictionary<int, string> _stateNames = [];
    readonly Dictionary<int, string> _envNames = [];
    readonly char[] _bits = new char[256];
    readonly long[] _recent = new long[RecentFrames];
    readonly List<Flash> _flashes = [];
    readonly List<Rect> _traced = new(64);
    readonly List<Rect> _gaps = new(64);
    internal readonly List<InspectorRow> ShownRows = [];
    UiView? _tracedOn;
    Node? _tracing;
    int _tracingGeneration;
    int _recentAt;
    int _selectedGeneration;
    Node? _held;
    int _heldGeneration;
    InputStates _heldBefore;
    int _treeStamp = -1;
    Shown _shown;
    Vector2? _pickAt;
    Node? _rowHovered;
    int _rowHoveredGeneration;

    public InspectorModel(UiView target)
        : this()
    {
        _target = target;
    }

    public InspectorModel(NodeRef root)
        : this()
    {
        _rootRef = root;
    }

    InspectorModel()
    {
        Tree = new ElementTree(this);
        Props = new PropertyTable(this);
        TreeHeight = 240f;
        ShowsRepaints = true;
        ShowsFrameBytes = true;
    }

    public UiView Target => _target ?? _rootRef!.Node!.View!;

    public Node Root => _rootRef is { } root ? root.Node! : _target!.Root;

    public ElementTree Tree { get; }

    public PropertyTable Props { get; }

    public ScrollHandle? TreeScroll { get; set; }

    public Action<string>? OpenSource { get; set; }

    public ReadOnlySpan<Flash> Flashes => CollectionsMarshal.AsSpan(_flashes);

    public ReadOnlySpan<Rect> Gaps => CollectionsMarshal.AsSpan(_gaps);

    [ObservableProperty]
    public partial Node? Selected { get; private set; }

    [ObservableProperty]
    public partial bool Picking { get; set; }

    [ObservableProperty]
    public partial InputStates Held { get; private set; }

    [ObservableProperty]
    public partial int Revision { get; private set; }

    [ObservableProperty]
    public partial UiKey TreeValue { get; private set; }

    [ObservableProperty]
    public partial BoxModel? SelectedBox { get; private set; }

    [ObservableProperty]
    public partial ResolvedEnvironment? SelectedEnvironment { get; private set; }

    [ObservableProperty]
    public partial int GapRevision { get; private set; }

    [ObservableProperty]
    public partial Rect? HoverBox { get; private set; }

    [ObservableProperty]
    public partial int FlashRevision { get; private set; }

    [ObservableProperty]
    public partial bool ShowsDefaults { get; set; }

    [ObservableProperty]
    public partial bool ShowsRepaints { get; set; }

    [ObservableProperty]
    public partial bool ShowsFrameBytes { get; set; }

    [ObservableProperty]
    public partial int SpeedStep { get; private set; }

    [ObservableProperty]
    public partial long FrameBytes { get; private set; }

    [ObservableProperty]
    public partial long WorstBytes { get; private set; }

    [ObservableProperty]
    public partial int AtlasResets { get; private set; }

    [ObservableProperty]
    public partial string? WatchedProp { get; private set; }

    [ObservableProperty]
    public partial bool Watching { get; set; }

    [ObservableProperty]
    public partial int WatchCount { get; set; }

    [ObservableProperty]
    public partial float TreeHeight { get; set; }

    public void Select(Node? node, bool reveal = true)
    {
        if (node is not null && (node.View != Target || !IsWithin(node, Root) || node == Root))
            node = null;
        if (node == Selected && node?.Generation == _selectedGeneration)
            return;
        Selected = node;
        _selectedGeneration = node?.Generation ?? 0;
        Held = InputStates.None;
        Hold();
        Props.Forget();
        WatchedProp = null;
        WatchCount = 0;
        Props.Describe(ComponentOf(node));
        TreeValue = node is null ? default(UiKey) : KeyOf(node, reveal);
        _shown = Snapshot(node);
        Revision++;
        UpdateBoxes();
    }

    public void SelectRow(UiKey key)
    {
        var rows = Tree.Rows;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Key == key)
            {
                Select(rows[i].Peek, reveal: false);
                return;
            }
        }
    }

    public void Watch(string name)
    {
        if (ReferenceEquals(WatchedProp, name))
            return;
        WatchedProp = name;
        WatchCount = 0;
    }

    public void ChangePicking(bool picking)
    {
        Picking = picking;
        if (!picking)
            _pickAt = null;
        RefreshHover();
    }

    public void Open()
    {
        if (Selected is { File: not null } node)
            OpenSource?.Invoke(SourceLink(node));
    }

    public void Toggle(InputStates state)
    {
        Held ^= state;
        Hold();
    }

    public void ChangeHeld(InputStates states)
    {
        Held = states;
        Hold();
    }

    public void CycleSpeed()
    {
        if (SpeedStep == 0)
            _speedBefore = Target.Context.AnimationSpeed;
        SpeedStep = (SpeedStep + 1) % Speeds.Length;
        Target.Context.AnimationSpeed = SpeedStep == 0 ? _speedBefore : Speeds[SpeedStep];
    }

    public Node? Find(string id) => Find(Root, id);

    public void Inspect(string reveal, string? select = null)
    {
        Refresh();
        Select(Find(reveal));
        if (select is not null)
            Select(Find(select));
    }

    public void Refresh()
    {
        if (
            Selected is { } selected
            && (
                !selected.IsMounted
                || selected.Generation != _selectedGeneration
                || !IsWithin(selected, Root)
            )
        )
            Select(null);
        if (Target.TreeStamp != _treeStamp)
        {
            _treeStamp = Target.TreeStamp;
            Tree.Rebuild();
            if (Selected is { } shown)
                TreeValue = KeyOf(shown, reveal: false);
        }
        for (var i = 0; i < ShownRows.Count; i++)
            ElementTree.Recheck(ShownRows[i]);
        var now = Snapshot(Selected);
        var described = Props.Describe(ComponentOf(Selected));
        if (described || now != _shown)
        {
            _shown = now;
            Revision++;
        }
        UpdateBoxes();
        RefreshHover();
    }

    public void Recorded(List<Node> recorded)
    {
        var changed = false;
        if (ShowsRepaints)
        {
            var root = Root;
            for (var i = 0; i < recorded.Count; i++)
            {
                var node = recorded[i];
                if (!node.IsMounted || node.ViewBounds.IsEmpty || !IsWithin(node, root))
                    continue;
                AddFlash(node, Relative(node.ViewBounds));
                changed = true;
            }
        }
        recorded.Clear();
        for (var i = _flashes.Count - 1; i >= 0; i--)
        {
            var flash = _flashes[i];
            if (flash.Age + 1 >= FlashFrames || !ShowsRepaints)
                _flashes.RemoveAt(i);
            else
                _flashes[i] = flash with { Age = flash.Age + 1 };
            changed = true;
        }
        if (changed)
            FlashRevision++;
    }

    public void CountFrame(long bytes, int atlasResets)
    {
        AtlasResets = atlasResets;
        _recent[_recentAt] = Math.Max(0, bytes);
        _recentAt = (_recentAt + 1) % RecentFrames;
        var worst = 0L;
        for (var i = 0; i < RecentFrames; i++)
            worst = Math.Max(worst, _recent[i]);
        FrameBytes = Math.Max(0, bytes);
        WorstBytes = worst;
    }

    public void HoverAt(Vector2? point)
    {
        _pickAt = point;
        RefreshHover();
    }

    public void HoverRow(Node node, bool hovered)
    {
        if (hovered)
        {
            _rowHovered = node;
            _rowHoveredGeneration = node.Generation;
        }
        else if (_rowHovered == node)
            _rowHovered = null;
        RefreshHover();
    }

    public Node? Pick(Vector2 point)
    {
        var picked = Target.PickAt(point);
        return picked is not null && IsWithin(picked, Root) && picked != Root ? picked : null;
    }

    public void Release()
    {
        if (_held is { } held && held.IsMounted && held.Generation == _heldGeneration)
            Target.ForceStates(held, _heldBefore);
        _held = null;
        _tracedOn?.TraceGaps(null, _traced);
        _tracedOn = null;
        _tracing = null;
        if (SpeedStep != 0)
            Target.Context.AnimationSpeed = _speedBefore;
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

    public static bool IsWithin(Node node, Node root)
    {
        for (var n = node; n is not null; n = n.Parent)
        {
            if (n == root)
                return true;
        }
        return false;
    }

    public static Node? ComponentOf(Node? node)
    {
        for (var n = node; n is not null; n = n.Parent)
        {
            if (n.Kind == NodeKind.Component && !ElementTree.Collapses(n))
                return n;
        }
        return null;
    }

    public string SourceLink(Node node)
    {
        var path = new StringBuilder(node.File!.Length + 24).Append("vscode://file");
        var file = node.File.Replace('\\', '/');
        if (!file.StartsWith('/'))
            path.Append('/');
        foreach (var c in file)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '.' or '_' or '~' or ':')
                path.Append(c);
            else
                foreach (var b in Encoding.UTF8.GetBytes(c.ToString()))
                    path.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        return path.Append(':').Append(node.Line.ToString(CultureInfo.InvariantCulture)).ToString();
    }

    public string Names(InputStates states)
    {
        if (!_stateNames.TryGetValue((int)states, out var names))
            _stateNames[(int)states] = names = Spelled(states);
        return names;
    }

    public string Names(EnvReads reads)
    {
        if (!_envNames.TryGetValue((int)reads, out var names))
            _envNames[(int)reads] = names = Spelled(reads);
        return names;
    }

    [ColdPath]
    static string Spelled<T>(T flags)
        where T : struct, Enum => flags.ToString();

    public ReadOnlySpan<char> Bits(ulong mask)
    {
        var length = 0;
        for (var bit = 0; bit < 64; bit++)
        {
            if ((mask & (1UL << bit)) == 0)
                continue;
            if (length > 0)
                _bits[length++] = ' ';
            _bits[length++] = '#';
            bit.TryFormat(
                _bits.AsSpan(length),
                out var written,
                default,
                CultureInfo.InvariantCulture
            );
            length += written;
        }
        return _bits.AsSpan(0, length);
    }

    UiKey KeyOf(Node node, bool reveal)
    {
        if (!reveal)
            return Tree.RowOf(node) is { } row ? row.Key : default(UiKey);
        var index = Tree.Reveal(node);
        if (index < 0)
            return default(UiKey);
        TreeScroll?.ScrollToIndex(index);
        return Tree.Rows[index].Key;
    }

    void Hold()
    {
        var component = ComponentOf(Selected);
        if (_held is { } previous && previous != component)
        {
            if (previous.IsMounted && previous.Generation == _heldGeneration)
                Target.ForceStates(previous, _heldBefore);
            _held = null;
        }
        if (component is null || (_held is null && Held == InputStates.None))
            return;
        if (_held is null)
        {
            _held = component;
            _heldGeneration = component.Generation;
            _heldBefore = component.ForcedByView;
        }
        Target.ForceStates(component, _heldBefore | Held);
    }

    void UpdateBoxes()
    {
        var node = Boxed(Selected);
        Trace(node);
        var shown = node is not null && !Collapsed(node) ? node : null;
        SelectedBox = shown is null
            ? null
            : new BoxModel(
                Relative(shown.ViewBounds),
                shown.Place.Margin,
                shown.Layout.Border,
                shown.Layout.Padding
            );
        SelectedEnvironment = shown is { Env.Theme: not null }
            ? new ResolvedEnvironment(
                shown.Env,
                (shown.States & InputStates.Disabled) != 0,
                UiView.Held(shown),
                Exiting(shown)
            )
            : null;
        ShowGaps(shown is null ? default : CollectionsMarshal.AsSpan(_traced));
    }

    void Trace(Node? node)
    {
        if (node == _tracing && (node is null || node.Generation == _tracingGeneration))
            return;
        _tracedOn?.TraceGaps(null, _traced);
        _tracing = node;
        _tracingGeneration = node?.Generation ?? 0;
        _tracedOn = node?.View;
        _tracedOn?.TraceGaps(node, _traced);
    }

    void ShowGaps(ReadOnlySpan<Rect> traced)
    {
        var same = traced.Length == _gaps.Count;
        for (var i = 0; same && i < traced.Length; i++)
            same = Relative(traced[i]) == _gaps[i];
        if (same)
            return;
        _gaps.Clear();
        foreach (var gap in traced)
            _gaps.Add(Relative(gap));
        GapRevision++;
    }

    static bool Exiting(Node node)
    {
        for (var n = node; n is not null; n = n.Parent)
        {
            if (n.Exiting)
                return true;
        }
        return false;
    }

    void RefreshHover()
    {
        if (_rowHovered is { } row && (!row.IsMounted || row.Generation != _rowHoveredGeneration))
            _rowHovered = null;
        var shown = Boxed(Picking && _pickAt is { } at ? Pick(at) : _rowHovered);
        HoverBox = shown is null || Collapsed(shown) ? null : Relative(shown.ViewBounds);
    }

    void AddFlash(Node node, Rect box)
    {
        for (var i = 0; i < _flashes.Count; i++)
        {
            if (_flashes[i].Node == node)
            {
                _flashes[i] = new Flash(node, box, -1);
                return;
            }
        }
        if (_flashes.Count < MostFlashes)
            _flashes.Add(new Flash(node, box, -1));
    }

    Rect Relative(Rect bounds)
    {
        var origin = Root.ViewBounds;
        return bounds with { X = bounds.X - origin.X, Y = bounds.Y - origin.Y };
    }

    Shown Snapshot(Node? selected)
    {
        if (Boxed(selected) is not { } node)
            return default;
        var host = ComponentOf(node)?.Host;
        return new Shown(
            node.ViewBounds,
            node.Layout.Padding,
            node.Layout.Border,
            node.Place.Margin,
            node.States,
            node.Place.Hidden,
            Collapsed(node),
            host?.RenderCount ?? 0,
            host?.Dependencies.Count ?? 0
        );
    }

    public static Node? Boxed(Node? node)
    {
        while (node?.Host is { } host)
            node = host.Root;
        return node;
    }

    static bool Collapsed(Node node)
    {
        for (var n = node; n is not null; n = n.Parent)
        {
            if (n.Place.Collapsed)
                return true;
        }
        return false;
    }
}
