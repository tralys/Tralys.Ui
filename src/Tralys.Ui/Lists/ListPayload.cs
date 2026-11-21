using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

internal abstract class ListPayload : NodePayload
{
    internal abstract Size Measure(Avail width, Avail height);

    internal abstract bool Arrange(Size size);

    internal abstract void UnmountRows();
}

[FramePath]
internal sealed class ListPayload<TItem> : ListPayload, IItemsSourceListener, IChangeObserver
{
    const float FirstEstimate = 25f;
    static readonly TimeSpan TypeaheadPause = TimeSpan.FromSeconds(1);

    readonly Dictionary<UiKey, Node> _byKey = [];
    readonly char[] _typed = new char[32];
    readonly Handler<PointerEvent> _onDown;
    readonly Handler<PointerEvent> _onUp;
    readonly Handler<PointerEvent> _onCancel;
    readonly Handler<KeyEvent> _onKey;
    UiView? _view;
    Node? _node;
    ComponentHost? _owner;
    string _file = "";
    int _line;
    ListProps<TItem> _props;
    IItemsSource<TItem>? _listening;
    Dependencies _dependencies;
    bool _selectionStale = true;

    int _first = -1;
    float _startU;
    ValueList<float> _sizes;
    ValueList<Node?> _cells;
    ValueList<float> _nextSizes;
    ValueList<Node?> _nextCells;
    ValueList<Node> _loose;
    ValueList<Node> _spares;
    ValueList<Node> _rowNodes;
    Node? _kept;
    Node? _empty;
    float _estimate = FirstEstimate;
    float _widest;
    float _nextWidest;
    bool _crossAuto;
    bool _reportedUnbounded;
    int _poolKeeps;
    float _poolWindow;
    int _heldLine = -1;
    float _heldU;
    UiKey _pendingFocus;
    bool _pendingFocusVisible;
    bool _pendingHandoff;
    UiKey _cursor;
    UiKey _anchor;
    bool _tapping;
    Pointer _tapPointer;
    UiKey _tapKey;
    Vector2 _tapStart;
    int _typedLength;
    TimeSpan _typedAt;

    public ListPayload()
    {
        _onDown = Handler<PointerEvent>.Of(this, static (list, ref e) => list.PointerDown(ref e));
        _onUp = Handler<PointerEvent>.Of(this, static (list, ref e) => list.PointerUp(ref e));
        _onCancel = Handler<PointerEvent>.Of(
            this,
            static (list, ref e) => list.PointerCanceled(ref e)
        );
        _onKey = Handler<KeyEvent>.Of(this, static (list, ref e) => list.KeyDown(ref e));
    }

    public ref Dependencies Dependencies => ref _dependencies;

    UiView View => _view ?? throw Detached();

    Node ListNode => _node ?? throw Detached();

    ScrollHandle Handle =>
        ListNode.Scroller
        ?? throw new InvalidOperationException(
            "A virtual list scrolls with its own handle, which another scroll container has taken."
        );

    static InvalidOperationException Detached() =>
        new("A virtual list's payload is used only while attached to its node.");

    bool Horizontal => _props.Horizontal;

    int Columns => _props.Columns;

    float Gap => ListNode.Layout.RowGap;

    internal void Attach(UiView view, Node node, ComponentHost? owner, string file, int line)
    {
        _view = view;
        _node = node;
        _owner = owner;
        _file = file;
        _line = line;
        if (!ReferenceEquals(_dependencies.Dispatcher, view.Context.Dispatcher))
            _dependencies = new Dependencies(view.Context.Dispatcher);
    }

    internal InteractionProps Interaction(Cursor? cursor, bool autoFocus, int tabIndex) =>
        new()
        {
            Focusable = true,
            AutoFocus = autoFocus,
            TabIndex = tabIndex,
            TabNavigation = Navigation.Once,
            OnPointerDown = _onDown,
            OnPointerUp = _onUp,
            OnPointerCancel = _onCancel,
            OnKeyDown = _onKey,
            Cursor = cursor,
        };

    internal bool Write(in ListProps<TItem> props)
    {
        if (_props == props)
            return false;
        var old = _props;
        _props = props;
        if (!ReferenceEquals(old.Items, props.Items))
            NewSource(props.Items);
        else if (old.ItemKey != props.ItemKey)
            Loosen(stale: true);
        if (
            old.Selection != props.Selection
            || old.SelectionMode != props.SelectionMode
            || _listening is null
        )
            _selectionStale = true;
        View.InvalidateMeasure(ListNode);
        return true;
    }

    void NewSource(IItemsSource<TItem> items)
    {
        _listening?.RemoveListener(this);
        _listening = items;
        items.AddListener(this);
        Loosen(stale: true);
        _heldLine = -1;
        _widest = 0f;
        if (ListNode.Scroller is { } handle)
        {
            handle.StopMotion();
            handle.ClearRequests();
            handle.ToEnd = false;
            handle.Current = default;
        }
    }

    void Loosen(bool stale)
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            if (_cells[i] is { } row)
                _loose.Add(row);
        }
        _cells.Clear();
        _sizes.Clear();
        _first = -1;
        if (!stale)
            return;
        for (var i = 0; i < _loose.Count; i++)
            Unplace(HostOf(_loose[i]));
        if (_kept is { } kept)
            Unplace(HostOf(kept));
    }

    static void Unplace(RowHost<TItem> host)
    {
        host.Stale = true;
        host.At = -1;
    }

    static RowHost<TItem> HostOf(Node row) => row.HostOf<RowHost<TItem>>();

    UiKey KeyAt(int index) => _props.ItemKey(_props.Items.ItemAt(index));

    float MainOffset() => Horizontal ? Handle.Current.X : Handle.Current.Y;

    void SetMainOffset(float value)
    {
        var current = Handle.Current;
        Handle.Current = Horizontal ? new Vector2(value, current.Y) : new Vector2(current.X, value);
    }

    void ShiftOffset(float by)
    {
        if (by == 0f || ListNode.Scroller is not { } handle)
            return;
        var shift = Horizontal ? new Vector2(by, 0f) : new Vector2(0f, by);
        handle.Current = Vector2.Max(default, handle.Current + shift);
        if (handle.Motion == ScrollMotion.Glide)
        {
            handle.GlideFrom += shift;
            handle.GlideTo += shift;
        }
    }

    Geometry Measuring(float viewportU, float viewportV, bool crossExact, bool widest)
    {
        var layout = ListNode.Layout;
        var pad = layout.Padding;
        var count = _props.Items.Count;
        var columns = Columns;
        var g = new Geometry
        {
            Horizontal = Horizontal,
            Columns = columns,
            Count = count,
            Lines = (count + columns - 1) / columns,
            Gap = Gap,
            BorderU = Horizontal ? layout.Border.Left : layout.Border.Top,
            BorderV = Horizontal ? layout.Border.Top : layout.Border.Left,
            PadU = Horizontal ? pad.Left : pad.Top,
            PadEndU = Horizontal ? pad.Right : pad.Bottom,
            PadV = Horizontal ? pad.Top : pad.Left,
            ViewportU = viewportU,
            ViewportV = viewportV,
            CrossExact = crossExact,
            Widest = widest,
        };
        var innerV = MathF.Max(0f, viewportV - g.PadV - (Horizontal ? pad.Bottom : pad.Right));
        g.CellV = MathF.Max(0f, (innerV - (columns - 1) * g.Gap) / columns);
        g.Bounded = !_props.RealizeAll && float.IsFinite(viewportU);
        g.Cache = g.Bounded ? MathF.Max(0f, _props.CacheLength ?? viewportU / 2f) : 0f;
        return g;
    }

    internal override Size Measure(Avail width, Avail height)
    {
        var horizontal = Horizontal;
        var main = horizontal ? width : height;
        var cross = horizontal ? height : width;
        var layout = ListNode.Layout;
        var border = layout.Border;
        var pad = layout.Padding;
        var pbU = horizontal
            ? border.Left + border.Right + pad.Left + pad.Right
            : border.Top + border.Bottom + pad.Top + pad.Bottom;
        var pbV = horizontal
            ? border.Top + border.Bottom + pad.Top + pad.Bottom
            : border.Left + border.Right + pad.Left + pad.Right;

        float mainSize;
        float crossSize;
        var unbounded = !main.IsBounded && main.Kind != AvailKind.MinContent;
        if (unbounded && !_props.RealizeAll && !_reportedUnbounded)
        {
            _reportedUnbounded = true;
            View.Context.Report(Faults.UnboundedList(horizontal, _file, _line, _owner));
        }
        if (!unbounded || !_props.RealizeAll)
        {
            mainSize = main.IsBounded ? main.Value : pbU;
            crossSize = cross.IsExact ? cross.Value : pbV + _widest;
            _crossAuto = !cross.IsExact;
        }
        else
        {
            // Only the arranged rows set _widest; setting it here flips it against the arrange every layout.
            var borderV = horizontal ? border.Top + border.Bottom : border.Left + border.Right;
            var g = Measuring(
                float.PositiveInfinity,
                cross.IsExact ? cross.Value - borderV : float.NaN,
                cross.IsExact,
                widest: !cross.IsExact
            );
            mainSize = pbU;
            _nextWidest = 0f;
            if (g.Count > 0)
            {
                Realize(ref g, anchor: 0, anchorU: 0f);
                mainSize += RunEnd(g.Gap);
            }
            crossSize = cross.IsExact ? cross.Value : pbV + _nextWidest;
        }
        return horizontal ? new Size(mainSize, crossSize) : new Size(crossSize, mainSize);
    }

    internal override bool Arrange(Size size)
    {
        var horizontal = Horizontal;
        var border = ListNode.Layout.Border;
        var viewport = new Size(
            MathF.Max(0f, size.Width - border.Left - border.Right),
            MathF.Max(0f, size.Height - border.Top - border.Bottom)
        );
        var g = Measuring(
            horizontal ? viewport.Width : viewport.Height,
            horizontal ? viewport.Height : viewport.Width,
            crossExact: true,
            widest: _crossAuto
        );
        if (_selectionStale)
            TrackSelection();
        var handle = Handle;
        var widest = _widest;
        _nextWidest = 0f;

        if (g.Count == 0)
        {
            Loosen(stale: false);
            if (_kept is { } kept)
            {
                _loose.Add(kept);
                _kept = null;
            }
            ShowEmpty(viewport);
        }
        else
        {
            HideEmpty();
            var estimatedMax = MathF.Max(0f, ExtentU(g) - g.ViewportU);
            View.ResolveRequests(ListNode, handle, MainVector(estimatedMax));
            if (!ScrollToIndex(ref g))
                Realize(ref g);
            for (var round = 0; round < 3; round++)
            {
                var max = MathF.Max(0f, ExtentU(g) - g.ViewportU);
                var offset = MainOffset();
                var wanted = handle.ToEnd ? max : Math.Clamp(offset, 0f, max);
                if (wanted == offset)
                    break;
                SetMainOffset(wanted);
                Realize(ref g);
            }
            if (_crossAuto)
                _widest = MathF.Max(_widest, _nextWidest);
        }

        FocusPending();
        SettleLoose();
        var extentU = g.Count == 0 ? g.ViewportU : ExtentU(g);
        handle.ViewportSize = viewport;
        handle.ExtentSize = horizontal
            ? new Size(extentU, viewport.Height)
            : new Size(viewport.Width, extentU);
        View.SettleOffset(ListNode, handle, MainVector(MathF.Max(0f, extentU - g.ViewportU)));
        ArrangeRows(ref g, viewport);
        Order();
        Trim(ref g);
        return _crossAuto && _widest != widest;
    }

    Vector2 MainVector(float value) => Horizontal ? new Vector2(value, 0f) : new Vector2(0f, value);

    float Estimate()
    {
        if (_props.FixedRowHeight || _props.RowHeight > 0f)
            return _props.RowHeight;
        var total = 0f;
        var lines = 0;
        var columns = Columns;
        for (var i = 0; i < _sizes.Count; i++)
        {
            if (_cells[i * columns] is null || _sizes[i] <= 0f)
                continue;
            total += _sizes[i];
            lines++;
        }
        if (lines > 0)
            _estimate = total / lines;
        return _estimate;
    }

    float RunEnd(float gap)
    {
        if (_first < 0 || _sizes.Count == 0)
            return 0f;
        var u = _startU;
        for (var i = 0; i < _sizes.Count; i++)
            u += _sizes[i] + gap;
        return u - gap;
    }

    float ExtentU(in Geometry g)
    {
        var stride = Estimate() + g.Gap;
        if (_first < 0 || _sizes.Count == 0)
            return g.PadU + MathF.Max(0f, g.Lines * stride - g.Gap) + g.PadEndU;
        var last = _first + _sizes.Count - 1;
        return g.PadU + RunEnd(g.Gap) + (g.Lines - 1 - last) * stride + g.PadEndU;
    }

    float LineStart(int line, float gap)
    {
        var u = _startU;
        for (var i = 0; i < line - _first; i++)
            u += _sizes[i] + gap;
        return u;
    }

    int LinePast(float u, float gap, out float lineU)
    {
        lineU = _startU;
        for (var i = 0; i < _sizes.Count; i++)
        {
            var end = lineU + _sizes[i];
            if (end > u)
                return _first + i;
            lineU = end + gap;
        }
        return _first + _sizes.Count;
    }

    bool AnchorFor(in Geometry g, float start, float viewStart, out int line, out float u)
    {
        var offset = MainOffset();
        if (offset > 0f && _first >= 0 && _sizes.Count > 0)
        {
            line = LinePast(viewStart, g.Gap, out u);
            if (line <= _first + _sizes.Count - 1 && u < viewStart + g.ViewportU)
                return true;
        }

        if (_heldLine >= 0)
        {
            line = _heldLine;
            u = _heldU;
            _heldLine = -1;
            if (line < g.Lines && u <= viewStart && viewStart - u < g.ViewportU)
                return false;
        }

        if (start <= 0f)
        {
            line = 0;
            u = 0f;
            return false;
        }

        var stride = Estimate() + g.Gap;
        line = stride > 0f ? Math.Min((int)(start / stride), g.Lines - 1) : 0;
        u = line * stride;
        return false;
    }

    void Realize(ref Geometry g, int anchor = -1, float anchorU = 0f)
    {
        var settles = anchor < 0;
        var viewStart = MainOffset() - g.PadU;
        var start = g.Bounded ? viewStart - g.Cache : float.NegativeInfinity;
        var end = g.Bounded ? viewStart + g.ViewportU + g.Cache : float.PositiveInfinity;
        var holds = settles && AnchorFor(g, start, viewStart, out anchor, out anchorU);
        anchor = Math.Clamp(anchor, 0, Math.Max(0, g.Lines - 1));

        _byKey.Clear();
        for (var i = 0; i < _cells.Count; i++)
        {
            if (_cells[i] is { } row)
                _byKey.TryAdd(row.Key, row);
        }
        for (var i = 0; i < _loose.Count; i++)
            _byKey.TryAdd(_loose[i].Key, _loose[i]);
        _loose.Clear();
        if (_kept is { } kept)
            _byKey.TryAdd(kept.Key, kept);
        _kept = null;

        _nextSizes.Clear();
        _nextCells.Clear();
        var line = anchor;
        var u = anchorU;
        do
        {
            u += RealizeLine(ref g, line, append: true) + g.Gap;
            line++;
        } while (u < end && line < g.Lines);
        var nextLast = line - 1;

        line = anchor - 1;
        u = anchorU;
        while (u > start && line >= 0)
        {
            u -= RealizeLine(ref g, line, append: false) + g.Gap;
            line--;
        }
        var nextFirst = line + 1;

        var startMoved = 0f;
        var grown = 0f;
        var oldLast = _first + _sizes.Count - 1;
        var firstKept = Math.Max(nextFirst, _first);
        if (holds && _first >= 0 && firstKept <= Math.Min(nextLast, oldLast))
        {
            var nextKeptU = u;
            for (var l = nextFirst; l < firstKept; l++)
                nextKeptU += _nextSizes[l - nextFirst] + g.Gap;
            startMoved = LineStart(firstKept, g.Gap) - nextKeptU;
            for (var l = firstKept; l < anchor; l++)
                grown += _nextSizes[l - nextFirst] - _sizes[l - _first];
        }

        (_sizes, _nextSizes) = (_nextSizes, _sizes);
        (_cells, _nextCells) = (_nextCells, _cells);
        _first = nextFirst;
        _startU = u + startMoved;
        foreach (var left in _byKey)
            _loose.Add(left.Value);
        _byKey.Clear();

        if (!settles)
            return;
        var slide = 0f;
        if (_first == 0)
            slide = -_startU;
        else if (holds && _startU <= 0f)
            slide = _first * (Estimate() + g.Gap) - _startU;
        _startU += slide;
        ShiftOffset(grown + slide);
    }

    float RealizeLine(ref Geometry g, int line, bool append)
    {
        var size = 0f;
        var columns = g.Columns;
        for (var c = 0; c < columns; c++)
        {
            var index = line * columns + c;
            Node? row = null;
            if (index < g.Count)
            {
                row = Claim(index);
                size = MathF.Max(size, MeasureRow(ref g, row));
            }
            if (append)
                _nextCells.Add(row);
            else
                _nextCells.Insert(c, row);
        }
        if (_props.FixedRowHeight)
            size = _props.RowHeight;
        if (append)
            _nextSizes.Add(size);
        else
            _nextSizes.Insert(0, size);
        return size;
    }

    Node Claim(int index)
    {
        var item = _props.Items.ItemAt(index);
        var key = _props.ItemKey(item);
        _byKey.Remove(key, out var row);
        return Show(index, item, key, row);
    }

    Node Show(int index, TItem item, UiKey key, Node? row)
    {
        RowHost<TItem> host;
        if (row is not null)
            host = HostOf(row);
        else if (_spares.Count > 0)
        {
            row = _spares.Pop();
            host = HostOf(row);
            View.ReuseRow(row, key, _owner);
            host.Stale = true;
        }
        else
        {
            host = Pool<RowHost<TItem>>.TryRent() ?? new RowHost<TItem>();
            row = View.MountRow(ListNode, host, _owner, _file, _line, key);
            host.Stale = true;
        }
        host.At = index;
        if (host.Stale || host.Dirty || host.Index != index || host.Row != _props.Row)
        {
            host.Item = item;
            host.Index = index;
            host.Row = _props.Row;
            host.Stale = false;
            View.RenderRow(ListNode, row);
        }
        if (FlagRoot(row, host))
            View.RenderRow(ListNode, row);
        return row;
    }

    // A state flip re-renders the row, so it never shows a frame without its states.
    bool FlagRoot(Node row, RowHost<TItem> host)
    {
        var root = host.Root;
        if (host.FlaggedRoot != root)
        {
            if (host.FlaggedRoot is { } old && old.Generation == host.FlaggedGeneration)
            {
                old.RowFocusable = false;
                old.SetRowSelected(false);
            }
            host.FlaggedRoot = root;
            host.FlaggedGeneration = root?.Generation ?? 0;
        }
        if (root is null)
            return false;
        root.RowFocusable = true;
        var selected = IsSelected(row.Key);
        if (root.RowSelected == selected)
            return false;
        root.SetRowSelected(selected);
        return true;
    }

    bool IsSelected(UiKey key) =>
        _props.SelectionMode != SelectionMode.None
        && _props.Selection is { } selection
        && selection.Contains(key);

    Size MeasureNode(ref Geometry g, Node node, out float marginU, out float marginV)
    {
        var horizontal = g.Horizontal;
        var m = node.Place.Margin;
        marginU = horizontal ? m.Left + m.Right : m.Top + m.Bottom;
        marginV = horizontal ? m.Top + m.Bottom : m.Left + m.Right;
        var fixedMain = _props.FixedRowHeight ? _props.RowHeight : float.NaN;
        var cellV = g.CrossExact ? MathF.Max(0f, g.CellV - marginV) : float.NaN;
        node.PctBasis = horizontal ? new Vector2(fixedMain, cellV) : new Vector2(cellV, fixedMain);
        var mainAvail = _props.FixedRowHeight
            ? Avail.Exact(MathF.Max(0f, fixedMain - marginU))
            : Avail.MaxContent;
        var crossAvail = g.CrossExact
            ? UiView.Stretched(node, horizontal, cellV)
            : Avail.MaxContent;
        var size = horizontal
            ? View.Measure(node, mainAvail, crossAvail)
            : View.Measure(node, crossAvail, mainAvail);
        if (g.Widest)
        {
            var natural = horizontal
                ? View.Measure(node, mainAvail, Avail.MaxContent).Height
                : View.Measure(node, Avail.MaxContent, mainAvail).Width;
            _nextWidest = MathF.Max(_nextWidest, natural + marginV);
        }
        return size;
    }

    float MeasureRow(ref Geometry g, Node row)
    {
        _rowNodes.Clear();
        UiView.CollectChildren(row, ref _rowNodes);
        var main = 0f;
        for (var i = 0; i < _rowNodes.Count; i++)
        {
            var size = MeasureNode(ref g, _rowNodes[i], out var marginU, out _);
            main += (g.Horizontal ? size.Width : size.Height) + marginU;
        }
        return main;
    }

    void ArrangeRows(ref Geometry g, Size viewport)
    {
        var toView = UiView.ContentToView(ListNode);
        var snapped = UiView.ContentSnappedToView(ListNode);
        if (_empty is { } empty)
        {
            _rowNodes.Clear();
            UiView.CollectChildren(empty, ref _rowNodes);
            var border = ListNode.Layout.Border;
            for (var i = 0; i < _rowNodes.Count; i++)
            {
                var node = _rowNodes[i];
                node.PctBasis = new Vector2(viewport.Width, viewport.Height);
                View.Measure(node, Avail.Exact(viewport.Width), Avail.Exact(viewport.Height));
                View.Arrange(
                    node,
                    new Rect(border.Left, border.Top, viewport.Width, viewport.Height),
                    toView,
                    snapped
                );
            }
        }

        var columns = g.Columns;
        var u = _startU;
        for (var line = 0; line < _sizes.Count; line++)
        {
            var size = _sizes[line];
            for (var c = 0; c < columns; c++)
            {
                if (_cells[line * columns + c] is { } row)
                    ArrangeRow(ref g, row, u, c, size, toView, snapped);
            }
            u += size + g.Gap;
        }

        if (_kept is { } kept)
        {
            var at = HostOf(kept).At;
            var keptLine = at / columns;
            var size = MeasureRow(ref g, kept);
            var estimated = keptLine * (Estimate() + g.Gap);
            var keptU =
                keptLine < _first
                    ? MathF.Min(estimated, _startU - size - g.Gap)
                    : MathF.Max(estimated, RunEnd(g.Gap) + g.Gap);
            ArrangeRow(ref g, kept, keptU, at % columns, size, toView, snapped);
        }
    }

    void ArrangeRow(
        ref Geometry g,
        Node row,
        float u,
        int column,
        float lineSize,
        in Matrix3x2 toView,
        in Matrix3x2 snapped
    )
    {
        _rowNodes.Clear();
        UiView.CollectChildren(row, ref _rowNodes);
        var horizontal = g.Horizontal;
        var v = g.PadV + column * (g.CellV + g.Gap);
        var stack = g.PadU + u;
        var single = _rowNodes.Count == 1;
        for (var i = 0; i < _rowNodes.Count; i++)
        {
            var node = _rowNodes[i];
            var size = MeasureNode(ref g, node, out var marginU, out _);
            var m = node.Place.Margin;
            var main = horizontal ? size.Width : size.Height;
            if (single && (horizontal ? node.Layout.Width : node.Layout.Height).IsAuto)
                main = MathF.Max(0f, lineSize - marginU);
            var cross = horizontal ? size.Height : size.Width;
            var rect = horizontal
                ? new Rect(g.BorderU + stack + m.Left, g.BorderV + v + m.Top, main, cross)
                : new Rect(g.BorderV + v + m.Left, g.BorderU + stack + m.Top, cross, main);
            View.Arrange(node, rect, toView, snapped);
            stack += main + marginU;
        }
    }

    bool ScrollToIndex(ref Geometry g)
    {
        var handle = Handle;
        var index = handle.ToIndex;
        if (index < 0 || g.ViewportU <= 0f)
            return false;
        var align = handle.ToIndexAlign;
        var animate = handle.ToIndexAnimate && View.Context.SmoothScrolling;
        handle.ToIndex = -1;
        if (!handle.ToIndexShown && _props.Items is IShownIndexes shown)
            index = shown.ShownIndex(index);
        handle.ToIndexShown = false;
        if ((uint)index >= (uint)g.Count)
            return false;

        var from = MainOffset();
        var line = index / g.Columns;
        float u;
        float size;
        if (
            _first >= 0
            && line >= _first
            && line < _first + _sizes.Count
            && _cells[(line - _first) * g.Columns] is not null
        )
        {
            u = LineStart(line, g.Gap);
            size = _sizes[line - _first];
        }
        else
        {
            var estimate = Estimate();
            u = line * (estimate + g.Gap);
            size = estimate;
            SetMainOffset(MathF.Max(0f, from + Reach(g, u, size, from, align)));
            Realize(ref g, line, u);
            u = LineStart(line, g.Gap);
            size = _sizes[line - _first];
        }
        var offset = MainOffset();
        var target = MathF.Max(0f, offset + Reach(g, u, size, offset, align));
        SetMainOffset(target);
        Realize(ref g);
        if (!animate)
            return true;

        target = MainOffset();
        SetMainOffset(from);
        Realize(ref g);
        var current = Handle.Current;
        View.GlideFor(
            handle,
            Horizontal ? new Vector2(target, current.Y) : new Vector2(current.X, target)
        );
        return true;
    }

    float Reach(in Geometry g, float u, float size, float offset, ScrollAlign align)
    {
        var start = g.PadU + u;
        return UiView.Reach(start, start + size, offset, offset + g.ViewportU, align);
    }

    void ShowEmpty(Size viewport)
    {
        if (_props.Empty.IsEmpty)
        {
            HideEmpty();
            return;
        }
        if (_empty is null)
        {
            var host = Pool<SlotHost>.TryRent() ?? new SlotHost();
            _empty = View.MountRow(ListNode, host, _owner, _file, _line, default);
        }
        var slotHost = _empty.HostOf<SlotHost>();
        if (slotHost.Dirty || slotHost.Content != _props.Empty)
        {
            slotHost.Content = _props.Empty;
            View.RenderRow(ListNode, _empty);
        }
    }

    void HideEmpty()
    {
        if (_empty is not { } empty)
            return;
        _empty = null;
        View.Unmount(empty);
    }

    void FocusPending()
    {
        if (!_pendingFocus.IsKeyed)
            return;
        var key = _pendingFocus;
        _pendingFocus = default;
        Node? target = null;
        for (var i = 0; i < _cells.Count; i++)
        {
            if (_cells[i] is { } row && row.Key == key)
            {
                target = row;
                break;
            }
        }
        if (_pendingHandoff)
            target ??= Keep(key);
        if (target is null || HostOf(target).Root is not { } root)
            return;
        if (_pendingHandoff)
            View.HandFocus(root, _pendingFocusVisible);
        else
            View.Focus(root, FocusCause.Arrow, _pendingFocusVisible);
    }

    Node? Keep(UiKey key)
    {
        var index = IndexOfKey(key);
        if (index < 0)
            return null;
        Node? row = null;
        for (var i = 0; i < _loose.Count; i++)
        {
            if (_loose[i].Key == key)
            {
                row = _loose[i];
                _loose.RemoveAt(i);
                break;
            }
        }
        return _kept = Show(index, _props.Items.ItemAt(index), key, row);
    }

    void SettleLoose()
    {
        for (var i = 0; i < _loose.Count; i++)
        {
            var row = _loose[i];
            if (_kept is null && View.HoldsFocus(row))
            {
                var host = HostOf(row);
                if (host.At < 0)
                    host.At = IndexOfKey(row.Key);
                if (host.At >= 0)
                {
                    _kept = row;
                    if (host.Stale || host.Index != host.At)
                    {
                        host.Item = _props.Items.ItemAt(host.At);
                        host.Index = host.At;
                        host.Stale = false;
                        View.RenderRow(ListNode, row);
                    }
                    continue;
                }
            }
            Retire(row);
        }
        _loose.Clear();
    }

    void Retire(Node row)
    {
        var host = HostOf(row);
        host.FlaggedRoot = null;
        host.At = -1;
        View.RetireRow(row);
        _spares.Add(row);
    }

    int IndexOfKey(UiKey key)
    {
        if (!key.IsKeyed)
            return -1;
        var columns = Columns;
        for (var i = 0; i < _cells.Count; i++)
        {
            if (_cells[i] is { } row && row.Key == key)
                return _first * columns + i;
        }
        var items = _props.Items;
        var count = items.Count;
        for (var i = 0; i < count; i++)
        {
            if (_props.ItemKey(items.ItemAt(i)) == key)
                return i;
        }
        return -1;
    }

    void Order()
    {
        Node? previous = null;
        var keptBefore = _kept is { } kept && HostOf(kept).At / Columns < _first ? kept : null;
        if (keptBefore is not null)
            Place(keptBefore, ref previous);
        for (var i = 0; i < _cells.Count; i++)
        {
            if (_cells[i] is { } row)
                Place(row, ref previous);
        }
        if (_kept is { } keptAfter && keptBefore is null)
            Place(keptAfter, ref previous);
        if (_empty is { } empty)
            Place(empty, ref previous);
        for (var i = 0; i < _spares.Count; i++)
            Place(_spares[i], ref previous);
    }

    void Place(Node node, ref Node? previous)
    {
        if (node._prev != previous)
        {
            ListNode.Unlink(node);
            ListNode.InsertAfter(previous, node);
            View.MarkComposite();
        }
        previous = node;
    }

    void Trim(ref Geometry g)
    {
        var realized = _kept is null ? 0 : 1;
        for (var i = 0; i < _cells.Count; i++)
        {
            if (_cells[i] is not null)
                realized++;
        }
        var window = g.Bounded ? g.ViewportU + 2f * g.Cache : float.PositiveInfinity;
        _poolKeeps =
            float.IsFinite(window) && window >= _poolWindow
                ? Math.Max(_poolKeeps, realized)
                : realized;
        _poolWindow = window;
        while (_spares.Count > _poolKeeps)
            View.Unmount(_spares.Pop());
    }

    void TrackSelection()
    {
        _selectionStale = false;
        var scope = Observation.Track(this);
        try
        {
            if (_props.SelectionMode != SelectionMode.None && _props.Selection is { } selection)
                _ = selection.Version;
        }
        finally
        {
            scope.Complete(keepDropped: false);
        }
        Reflag();
    }

    public void OnChanged(object source, int property) => TrackSelection();

    void Reflag()
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            if (_cells[i] is { } row)
                HostOf(row).Root?.SetRowSelected(IsSelected(row.Key));
        }
        if (_kept is { } kept)
            HostOf(kept).Root?.SetRowSelected(IsSelected(kept.Key));
    }

    public void OnItemsChanged(IItemsSource source, in ItemsChange change)
    {
        if (!ReferenceEquals(source, _props.Items) || !ListNode.IsMounted)
            return;
        Follow(change);
        if (
            change.Kind
            is ItemsChangeKind.Remove
                or ItemsChangeKind.Replace
                or ItemsChangeKind.Reset
        )
            _widest = 0f;
        if (change.Kind == ItemsChangeKind.Reset)
            Hold();
        else if (change.Kind == ItemsChangeKind.Replace)
            Replaced(change.OldIndex, change.Count);
        else if (Columns > 1)
        {
            DropRemoved();
            Hold();
        }
        else
        {
            switch (change.Kind)
            {
                case ItemsChangeKind.Add:
                    Inserted(change.NewIndex, change.Count);
                    break;
                case ItemsChangeKind.Remove:
                    Removed(change.OldIndex, change.Count, moving: false);
                    break;
                case ItemsChangeKind.Move:
                    Removed(change.OldIndex, change.Count, moving: true);
                    Inserted(change.NewIndex, change.Count);
                    break;
            }
        }
        View.InvalidateMeasure(ListNode);
    }

    void Follow(in ItemsChange change)
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            if (_cells[i] is { } row)
                FollowRow(row, change);
        }
        for (var i = 0; i < _loose.Count; i++)
            FollowRow(_loose[i], change);
        if (_kept is { } kept)
        {
            FollowRow(kept, change);
            if (HostOf(kept).At < 0 && change.Kind == ItemsChangeKind.Remove)
            {
                _kept = null;
                Retire(kept);
            }
        }
        if (ListNode.Scroller is { } handle && handle.ToIndex >= 0)
        {
            if (change.Kind == ItemsChangeKind.Reset)
                handle.ToIndex = -1;
            else if (handle.ToIndexShown || _props.Items is not IShownIndexes)
                handle.ToIndex = Math.Max(-1, change.Follow(handle.ToIndex));
        }
    }

    static void FollowRow(Node row, in ItemsChange change)
    {
        var host = HostOf(row);
        host.At = change.Kind == ItemsChangeKind.Replace ? host.At : change.Follow(host.At);
        if (change.Kind == ItemsChangeKind.Reset)
            host.Stale = true;
    }

    void Hold()
    {
        if (_first >= 0 && _sizes.Count > 0)
        {
            var viewStart =
                MainOffset()
                - (Horizontal ? ListNode.Layout.Padding.Left : ListNode.Layout.Padding.Top);
            _heldLine = LinePast(viewStart, Gap, out _heldU);
        }
        Loosen(stale: false);
    }

    void DropRemoved()
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            if (_cells[i] is { } row && HostOf(row).At < 0)
            {
                _cells[i] = null;
                Retire(row);
            }
        }
    }

    void Replaced(int index, int count)
    {
        for (var i = 0; i < _loose.Count; i++)
        {
            var at = HostOf(_loose[i]).At;
            if (at >= index && at < index + count)
                ReplaceRow(_loose[i], at);
        }
        var columns = Columns;
        for (var i = 0; i < _cells.Count; i++)
        {
            var at = _first * columns + i;
            if (at < index || at >= index + count || _cells[i] is not { } row)
                continue;
            ReplaceRow(row, at);
            _loose.Add(row);
            _cells[i] = null;
        }
        if (_kept is { } kept && HostOf(kept).At >= index && HostOf(kept).At < index + count)
        {
            _kept = null;
            ReplaceRow(kept, HostOf(kept).At);
            _loose.Add(kept);
        }
    }

    void ReplaceRow(Node row, int at)
    {
        HostOf(row).Stale = true;
        if (View.HoldsFocus(row))
        {
            _pendingFocus = KeyAt(at);
            _pendingHandoff = true;
            _pendingFocusVisible =
                View.Focused is { } focused && (focused._states & InputStates.FocusVisible) != 0;
        }
    }

    void Inserted(int index, int count)
    {
        if (_first < 0 || _sizes.Count == 0)
            return;
        var at = index - _first;
        if (at >= _sizes.Count)
            return;
        if (at < 0)
        {
            _first += count;
            var moved = count * (Estimate() + Gap);
            _startU += moved;
            ShiftOffset(moved);
            return;
        }
        for (var i = 0; i < count; i++)
        {
            _sizes.Insert(at, 0f);
            _cells.Insert(at, null);
        }
    }

    void Removed(int index, int count, bool moving)
    {
        if (_first < 0 || _sizes.Count == 0)
            return;
        var gap = Gap;
        var startIndex = index - _first;
        var endIndex = index + count - _first;
        var moved = 0f;
        if (endIndex > 0 && startIndex < _sizes.Count)
        {
            var start = Math.Max(startIndex, 0);
            var end = Math.Min(endIndex, _sizes.Count);
            var viewStart =
                MainOffset()
                - (Horizontal ? ListNode.Layout.Padding.Left : ListNode.Layout.Padding.Top);
            var u = _startU;
            for (var i = 0; i < end; i++)
            {
                u += _sizes[i];
                if (i >= start && u <= viewStart)
                    moved -= _sizes[i] + gap;
                u += gap;
            }
            for (var i = end - 1; i >= start; i--)
            {
                if (_cells[i] is { } row)
                {
                    if (moving)
                        _loose.Add(row);
                    else
                        Retire(row);
                }
                _cells.RemoveAt(i);
                _sizes.RemoveAt(i);
            }
        }
        if (startIndex < 0)
        {
            var before = Math.Min(-startIndex, count);
            _first -= before;
            var startU = _first == 0 ? 0f : MathF.Max(0f, _startU - before * (Estimate() + gap));
            moved += startU - _startU;
            _startU = startU;
        }
        if (_sizes.Count == 0)
            _first = -1;
        ShiftOffset(moved);
    }

    void PointerDown(ref PointerEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;
        if (RowAt(e.ViewPosition, out var pressable, out var inner) is not { } row || inner)
            return;
        if (e.Pointer.Kind != PointerKind.Mouse)
        {
            _tapping = true;
            _tapPointer = e.Pointer;
            _tapKey = row.Key;
            _tapStart = e.ViewPosition;
            return;
        }
        Pick(row.Key, e.Modifiers);
        if (e.ClickCount == 2 && !pressable && !_props.OnAction.IsEmpty)
            View.Report(_props.OnAction, row.Key);
    }

    void PointerUp(ref PointerEvent e)
    {
        if (!_tapping || e.Pointer != _tapPointer)
            return;
        _tapping = false;
        if (
            RowAt(e.ViewPosition, out _, out var inner) is { } row
            && !inner
            && row.Key == _tapKey
            && Vector2.Distance(e.ViewPosition, _tapStart) <= GestureSlop.Tap
        )
            Pick(row.Key, e.Modifiers);
    }

    void PointerCanceled(ref PointerEvent e)
    {
        if (e.Pointer == _tapPointer)
            _tapping = false;
    }

    // A pressable inside the row, not its root, owns the press: it neither selects nor activates the row.
    Node? RowAt(Vector2 viewPosition, out bool pressable, out bool inner)
    {
        pressable = inner = false;
        Node? last = null;
        for (var n = View.HitAt(viewPosition); n is not null && n != ListNode; n = n._parent)
        {
            if (n._parent == ListNode)
            {
                if (n.Host is not RowHost<TItem> host || n.SpareRow)
                    return null;
                inner |= pressable && last != host.Root;
                return n;
            }
            if (n.Interaction?.Props.IsPressable != true)
                continue;
            inner |= last is not null;
            pressable = true;
            last = n;
        }
        return null;
    }

    void Pick(UiKey key, ModifierKeys modifiers)
    {
        _cursor = key;
        switch (_props.SelectionMode)
        {
            case SelectionMode.Single:
                Report(SelectionAction.Replace, key, key);
                _anchor = key;
                break;
            case SelectionMode.Multiple when (modifiers & ModifierKeys.Control) != 0:
                Report(SelectionAction.Toggle, key, key);
                _anchor = key;
                break;
            case SelectionMode.Multiple when (modifiers & ModifierKeys.Shift) != 0:
                Report(SelectionAction.Range, key, _anchor.IsKeyed ? _anchor : key);
                break;
            case SelectionMode.Multiple:
                Report(SelectionAction.Replace, key, key);
                _anchor = key;
                break;
        }
    }

    void Report(SelectionAction action, UiKey key, UiKey anchor)
    {
        var selection = _props.Selection;
        if (action == SelectionAction.Replace && selection is not null)
        {
            var same = selection is SelectionSet set
                ? set.Count == 1 && set.Contains(key)
                : _props.SelectionMode == SelectionMode.Single && selection.Contains(key);
            if (same)
                return;
        }
        View.Report(_props.OnSelectionChange, new SelectionChange(action, key, anchor));
    }

    void KeyDown(ref KeyEvent e)
    {
        var items = _props.Items;
        var count = items.Count;
        if (count == 0)
            return;
        var modifiers = e.Modifiers;
        var control = (modifiers & ModifierKeys.Control) != 0;
        var other = (modifiers & (ModifierKeys.Alt | ModifierKeys.Gui)) != 0;
        var current = Current(out var rowFocused);
        var columns = Columns;
        var horizontal = Horizontal;
        int target;
        switch (e.Key)
        {
            case Key.Up or Key.Down or Key.Left or Key.Right:
            {
                if (control || other)
                    return;
                var key = e.Key;
                var forward = key == (horizontal ? Key.Right : Key.Down);
                var back = key == (horizontal ? Key.Left : Key.Up);
                if (forward || back)
                {
                    if (current < 0)
                        target = Navigable(0, 1, count);
                    else
                    {
                        var to = current + (forward ? columns : -columns);
                        if (forward && to >= count && current / columns < (count - 1) / columns)
                            to = count - 1;
                        target = Navigable(to, forward ? columns : -columns, count);
                    }
                }
                else if (columns > 1)
                {
                    var step = key == (horizontal ? Key.Down : Key.Right) ? 1 : -1;
                    target =
                        current < 0
                            ? Navigable(0, 1, count)
                            : Navigable(current + step, step, count);
                }
                else
                    return;
                if (target < 0 || target == current)
                    return;
                break;
            }
            case Key.Home when !other:
                target = Navigable(0, 1, count);
                break;
            case Key.End when !other:
                target = Navigable(count - 1, -1, count);
                break;
            case Key.PageDown or Key.PageUp when !control && !other:
                target = Page(current, e.Key == Key.PageDown, count);
                break;
            case Key.Space when !control && !other:
                if (
                    _props.SelectionMode != SelectionMode.Multiple
                    || current < 0
                    || FocusedPressablePresses(byKeyboardEnter: false)
                )
                    return;
                var toggled = KeyAt(current);
                Report(SelectionAction.Toggle, toggled, toggled);
                _anchor = toggled;
                Take(ref e);
                return;
            case Key.Enter when !control && !other:
                if (
                    current < 0
                    || _props.OnAction.IsEmpty
                    || FocusedPressablePresses(byKeyboardEnter: !e.FromGamepad)
                )
                    return;
                View.Report(_props.OnAction, KeyAt(current));
                Take(ref e);
                return;
            case Key.A when control && !other && _props.SelectionMode == SelectionMode.Multiple:
                Report(SelectionAction.All, default, default);
                Take(ref e);
                return;
            default:
                if (control || other || (target = Typeahead(e.Key, current, count)) < 0)
                    return;
                break;
        }
        if (target < 0)
            return;
        MoveTo(target, rowFocused);
        Take(ref e);
    }

    static void Take(ref KeyEvent e)
    {
        e.StopPropagation();
        e.PreventDefault();
    }

    bool FocusedPressablePresses(bool byKeyboardEnter) =>
        View.Focused is { Interaction: { } interaction } focused
        && !focused.RowFocusable
        && (byKeyboardEnter ? interaction.Props.EnterPresses : interaction.Props.SpacePresses);

    int Current(out bool rowFocused)
    {
        rowFocused = false;
        for (var n = View.Focused; n is not null && n != ListNode; n = n._parent)
        {
            if (n._parent == ListNode && n.Host is RowHost<TItem> host && !n.SpareRow)
            {
                rowFocused = true;
                return host.At;
            }
        }
        return IndexOfKey(_cursor);
    }

    int Navigable(int index, int step, int count)
    {
        for (; index >= 0 && index < count; index += step)
        {
            if (RealizedRoot(index) is not { } root)
                return index;
            if ((root._states & InputStates.Disabled) == 0 && !root.Place.Collapsed)
                return index;
        }
        return -1;
    }

    Node? RealizedRoot(int index)
    {
        var cell = index - _first * Columns;
        return _first >= 0 && cell >= 0 && cell < _cells.Count && _cells[cell] is { } row
            ? HostOf(row).Root
            : null;
    }

    int Page(int current, bool down, int count)
    {
        if (_first < 0 || _sizes.Count == 0)
            return down ? count - 1 : 0;
        var columns = Columns;
        var gap = Gap;
        var pad = Horizontal ? ListNode.Layout.Padding.Left : ListNode.Layout.Padding.Top;
        var viewport = Horizontal ? Handle.ViewportSize.Width : Handle.ViewportSize.Height;
        var viewStart = MainOffset() - pad;
        var viewEnd = viewStart + viewport;
        var whole = -1;
        var u = _startU;
        for (var line = 0; line < _sizes.Count; line++)
        {
            var end = u + _sizes[line];
            var inView = u >= viewStart - 0.5f && end <= viewEnd + 0.5f;
            if (inView && (down || whole < 0))
                whole = _first + line;
            u = end + gap;
        }
        if (whole < 0)
            whole = LinePast(viewStart, gap, out _);
        var currentLine = current < 0 ? -1 : current / columns;
        var column = current < 0 ? 0 : current % columns;
        var targetLine = whole;
        if (
            currentLine == whole
            || (down ? currentLine > whole : currentLine < whole && currentLine >= 0)
        )
        {
            var stride = Estimate() + gap;
            var lines = stride > 0f ? Math.Max(1, (int)(viewport / stride)) : 1;
            targetLine = currentLine + (down ? lines : -lines);
        }
        var lastLine = (count - 1) / columns;
        targetLine = Math.Clamp(targetLine, 0, lastLine);
        return Math.Min(count - 1, targetLine * columns + column);
    }

    int Typeahead(Key key, int current, int count)
    {
        if (_props.Typeahead is not { } text)
            return -1;
        char typed;
        if (key is >= Key.A and <= Key.Z)
            typed = (char)('a' + (key - Key.A));
        else if (key is >= Key.D0 and <= Key.D9)
            typed = (char)('0' + (key - Key.D0));
        else
            return -1;
        var now = View.Context.Now;
        if (now - _typedAt > TypeaheadPause)
            _typedLength = 0;
        _typedAt = now;
        if (_typedLength < _typed.Length)
            _typed[_typedLength++] = typed;
        var prefix = _typed.AsSpan(0, _typedLength);
        var start =
            current < 0 ? 0
            : _typedLength == 1 ? current + 1
            : current;
        var items = _props.Items;
        for (var k = 0; k < count; k++)
        {
            var i = (start + k) % count;
            if (text(items.ItemAt(i)).Span.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    void MoveTo(int target, bool rowFocused)
    {
        var key = KeyAt(target);
        _cursor = key;
        if (_props.SelectionMode == SelectionMode.Single)
        {
            Report(SelectionAction.Replace, key, key);
            _anchor = key;
        }
        var handle = Handle;
        handle.ClearRequests();
        handle.ToEnd = false;
        handle.ToIndex = target;
        handle.ToIndexAlign = ScrollAlign.Nearest;
        handle.ToIndexAnimate = false;
        handle.ToIndexShown = true;
        handle.Invalidate();
        if (!rowFocused)
            return;
        if (RealizedRoot(target) is { } root && root.IsMounted)
        {
            _pendingFocus = default;
            View.Focus(root, FocusCause.Arrow, visible: true);
        }
        else
        {
            _pendingFocus = key;
            _pendingHandoff = false;
            _pendingFocusVisible = true;
        }
    }

    internal override void UnmountRows()
    {
        while (ListNode._first is { } row)
            View.Unmount(row);
    }

    public override void Release(UiView view)
    {
        _listening?.RemoveListener(this);
        _listening = null;
        _dependencies.Clear();
        _props = default;
        _selectionStale = true;
        _first = -1;
        _startU = 0f;
        _sizes.Clear();
        _cells.Clear();
        _nextSizes.Clear();
        _nextCells.Clear();
        _loose.Clear();
        _spares.Clear();
        _rowNodes.Clear();
        _byKey.Clear();
        _kept = _empty = null;
        _estimate = FirstEstimate;
        _widest = _nextWidest = 0f;
        _crossAuto = _reportedUnbounded = false;
        _poolKeeps = 0;
        _poolWindow = 0f;
        _heldLine = -1;
        _pendingFocus = _cursor = _anchor = _tapKey = default;
        _tapping = false;
        _typedLength = 0;
        _owner = null;
        _view = null;
        _node = null;
        Pool<ListPayload<TItem>>.Return(this);
    }

    struct Geometry
    {
        public bool Horizontal;
        public bool Bounded;
        public bool CrossExact;
        public bool Widest;
        public int Columns;
        public int Count;
        public int Lines;
        public float Gap;
        public float BorderU;
        public float BorderV;
        public float PadU;
        public float PadEndU;
        public float PadV;
        public float ViewportU;
        public float ViewportV;
        public float CellV;
        public float Cache;
    }
}

[FramePath]
internal sealed class RowHost<TItem> : ComponentHost
{
    internal Slot<TItem> Row;
    internal TItem? Item;
    internal int Index = -1;
    internal int At = -1;
    internal bool Stale;
    internal Node? FlaggedRoot;
    internal int FlaggedGeneration;

    internal override Type ComponentType => typeof(VirtualListRow);

    internal override int TypeId => ComponentType<VirtualListRow>.Id;

    [MemberNotNullWhen(true, nameof(Item))]
    bool Bound => Index >= 0;

    internal override void Invoke(Ui ui)
    {
        if (Bound)
            Row.Render(ui, Item, Index);
    }

    internal override bool DescribeProps(IPropsVisitor visitor) => false;

    internal override void Release()
    {
        base.Release();
        Row = default;
        Item = default;
        Index = At = -1;
        Stale = false;
        FlaggedRoot = null;
        Pool<RowHost<TItem>>.Return(this);
    }
}

[FramePath]
internal sealed class SlotHost : ComponentHost
{
    internal Slot Content;

    internal override Type ComponentType => typeof(SlotHost);

    internal override int TypeId => ComponentType<SlotHost>.Id;

    internal override void Invoke(Ui ui) => Content.Render(ui);

    internal override bool DescribeProps(IPropsVisitor visitor) => false;

    internal override void Release()
    {
        base.Release();
        Content = default;
        Pool<SlotHost>.Return(this);
    }
}
