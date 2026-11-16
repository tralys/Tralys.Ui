using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Tralys.Ui.DevTools;

internal sealed class PropRow
{
    const int MostSlots = 8;

    public string Name = "";
    public char[] Value = new char[32];
    public int Length;
    public bool IsDefault;
    public bool Shown;
    public PropSlot? Last;
    readonly List<PropSlot> _slots = [];

    public TextValue Text => new(Value, 0, Length);

    public ReadOnlySpan<char> Chars => Value.AsSpan(0, Length);

    public void Show(ReadOnlySpan<char> text)
    {
        if (Value.Length < text.Length)
            Value = new char[Math.Max(text.Length, Value.Length * 2)];
        text.CopyTo(Value);
        Length = text.Length;
    }

    // A row keeps the text of the last few values it showed, so a selection going back and forth formats nothing.
    public PropSlot<T>? Find<T>(in T value, int stamp)
    {
        foreach (var slot in _slots)
        {
            if (slot is PropSlot<T> typed && EqualityComparer<T>.Default.Equals(typed.Value, value))
            {
                typed.Used = stamp;
                return typed;
            }
        }
        return null;
    }

    public PropSlot<T> Make<T>(int stamp)
    {
        PropSlot<T>? reused = null;
        var oldest = -1;
        for (var i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] is PropSlot<T> typed && (reused is null || typed.Used < reused.Used))
                reused = typed;
            if (oldest < 0 || _slots[i].Used < _slots[oldest].Used)
                oldest = i;
        }
        if (reused is null || _slots.Count < MostSlots)
        {
            reused = new PropSlot<T>();
            if (_slots.Count < MostSlots)
                _slots.Add(reused);
            else
                _slots[oldest] = reused;
        }
        reused.Used = stamp;
        return reused;
    }
}

internal abstract class PropSlot
{
    public char[] Text = new char[16];
    public int Length;
    public int Used;

    public void Keep(ReadOnlySpan<char> text)
    {
        if (Text.Length < text.Length)
            Text = new char[Math.Max(text.Length, Text.Length * 2)];
        text.CopyTo(Text);
        Length = text.Length;
    }
}

internal sealed class PropSlot<T> : PropSlot
{
    public T Value = default!;
}

internal sealed class PropertyTable : IPropsVisitor
{
    readonly List<PropRow> _rows = [];
    readonly FrameArena _scratch = new();
    readonly InspectorModel _model;
    ComponentHost? _host;
    int _generation;
    int _renders = -1;
    int _count;
    int _at;
    int _stamp;
    bool _changed;

    public PropertyTable(InspectorModel model) => _model = model;

    public int Count => _count;

    public PropRow this[int index] => _rows[index];

    public bool Describe(Node? component)
    {
        var host = component?.Host;
        if (
            host == _host
            && (host is null || (host.Generation == _generation && host.RenderCount == _renders))
        )
            return false;
        _changed = host != _host || host is null || host.Generation != _generation;
        if (_changed)
        {
            for (var i = 0; i < _rows.Count; i++)
                _rows[i].Shown = false;
        }
        _host = host;
        _generation = host?.Generation ?? 0;
        _renders = host?.RenderCount ?? -1;
        _at = 0;
        component?.DescribeProps(this);
        if (_at != _count)
            _changed = true;
        _count = _at;
        return _changed;
    }

    public void Forget()
    {
        _host = null;
        _renders = -1;
    }

    public void Prop<T>(string name, in T value)
    {
        PropRow row;
        if (_at < _rows.Count)
            row = _rows[_at];
        else
            _rows.Add(row = new PropRow());
        _at++;
        var again = row.Shown && ReferenceEquals(row.Name, name);
        if (typeof(T) == typeof(TextValue))
        {
            // A borrowed text's chars belong to its host, which writes over them, so it is compared as text.
            var text = Format(in value);
            if (again && text.SequenceEqual(row.Chars))
                return;
            row.Last = null;
            row.Show(text);
        }
        else
        {
            if (
                again
                && row.Last is PropSlot<T> last
                && EqualityComparer<T>.Default.Equals(last.Value, value)
            )
                return;
            var stamp = ++_stamp;
            var slot = row.Find(in value, stamp);
            if (slot is null)
            {
                slot = row.Make<T>(stamp);
                slot.Value = value;
                slot.Keep(Format(in value));
            }
            row.Last = slot;
            row.Show(slot.Text.AsSpan(0, slot.Length));
        }
        row.Name = name;
        row.Shown = true;
        row.IsDefault = EqualityComparer<T>.Default.Equals(value, default!);
        _changed = true;
        if (again && _model.Watching && ReferenceEquals(_model.WatchedProp, name))
        {
            _model.WatchCount++;
            if (Debugger.IsAttached)
                Debugger.Break();
        }
    }

    ReadOnlySpan<char> Format<T>(in T value)
    {
        _scratch.Reset();
        var start = _scratch.Begin(32);
        ValueText.Append(_scratch, in value);
        return _scratch.End(start).Span;
    }
}

internal static class ValueText
{
    public static void Append<T>(FrameArena a, in T value)
    {
        if (!typeof(T).IsValueType)
        {
            var o = Unsafe.As<T, object?>(ref Unsafe.AsRef(in value));
            switch (o)
            {
                case null:
                    a.Append("null");
                    return;
                case string s:
                    Quoted(a, s);
                    return;
            }
            var text = o.ToString();
            var type = o.GetType();
            a.Append(text is null || text == type.FullName ? ShortName(type) : text);
            return;
        }
        ref var v = ref Unsafe.AsRef(in value);
        if (typeof(T) == typeof(bool))
            a.Append(Unsafe.As<T, bool>(ref v) ? "true" : "false");
        else if (typeof(T) == typeof(int))
            a.AppendFormatted(Unsafe.As<T, int>(ref v), default);
        else if (typeof(T) == typeof(long))
            a.AppendFormatted(Unsafe.As<T, long>(ref v), default);
        else if (typeof(T) == typeof(float))
            Number(a, Unsafe.As<T, float>(ref v));
        else if (typeof(T) == typeof(double))
            a.AppendFormatted(Unsafe.As<T, double>(ref v), "0.###");
        else if (typeof(T) == typeof(float?))
            Nullable(a, Unsafe.As<T, float?>(ref v));
        else if (typeof(T) == typeof(int?))
        {
            if (Unsafe.As<T, int?>(ref v) is { } i)
                a.AppendFormatted(i, default);
            else
                a.Append("null");
        }
        else if (typeof(T) == typeof(bool?))
            a.Append(
                Unsafe.As<T, bool?>(ref v) switch
                {
                    true => "true",
                    false => "false",
                    null => "null",
                }
            );
        else if (typeof(T) == typeof(TextValue))
        {
            var text = Unsafe.As<T, TextValue>(ref v);
            if (text.IsNull)
                a.Append("null");
            else
                Quoted(a, text.Span);
        }
        else if (typeof(T) == typeof(Thickness))
            Sides(a, Unsafe.As<T, Thickness>(ref v));
        else if (typeof(T) == typeof(CornerRadius))
        {
            var r = Unsafe.As<T, CornerRadius>(ref v);
            Four(a, r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft);
        }
        else if (typeof(T) == typeof(Color))
            Hex(a, Unsafe.As<T, Color>(ref v));
        else if (typeof(T) == typeof(Size))
        {
            var size = Unsafe.As<T, Size>(ref v);
            Number(a, size.Width);
            a.Append("×");
            Number(a, size.Height);
        }
        else if (typeof(T) == typeof(Vector2))
        {
            var vector = Unsafe.As<T, Vector2>(ref v);
            Number(a, vector.X);
            a.Append(",");
            Number(a, vector.Y);
        }
        else if (typeof(T) == typeof(Rect))
            Box(a, Unsafe.As<T, Rect>(ref v));
        else if (!typeof(T).IsEnum || !EnumText<T>.TryAppend(a, value, default))
            a.Append(value!.ToString() ?? "");
    }

    public static void Number(FrameArena a, float value)
    {
        if (value == 0f)
            value = 0f;
        a.AppendFormatted(value, "0.##");
    }

    public static void Sides(FrameArena a, Thickness t)
    {
        if (t.Left == t.Top && t.Top == t.Right && t.Right == t.Bottom)
            Number(a, t.Left);
        else
            Four(a, t.Left, t.Top, t.Right, t.Bottom);
    }

    public static void Box(FrameArena a, Rect r)
    {
        Number(a, r.X);
        a.Append(",");
        Number(a, r.Y);
        a.Append(" ");
        Number(a, r.Width);
        a.Append("×");
        Number(a, r.Height);
    }

    static void Nullable(FrameArena a, float? value)
    {
        if (value is { } f)
            Number(a, f);
        else
            a.Append("null");
    }

    static void Four(FrameArena a, float first, float second, float third, float fourth)
    {
        if (first == second && second == third && third == fourth)
        {
            Number(a, first);
            return;
        }
        Number(a, first);
        a.Append(",");
        Number(a, second);
        a.Append(",");
        Number(a, third);
        a.Append(",");
        Number(a, fourth);
    }

    static void Hex(FrameArena a, Color c)
    {
        a.Append("#");
        if (c.A != 255)
            a.AppendFormatted(c.A, "X2");
        a.AppendFormatted(c.R, "X2");
        a.AppendFormatted(c.G, "X2");
        a.AppendFormatted(c.B, "X2");
    }

    static void Quoted(FrameArena a, ReadOnlySpan<char> text)
    {
        a.Append("\"");
        a.Append(text);
        a.Append("\"");
    }

    // Type.Name allocates again once a GC drops the runtime's weakly held type cache.
    static readonly ConcurrentDictionary<Type, string> s_shortNames = new();

    public static ReadOnlySpan<char> ShortName(Type type) =>
        s_shortNames.GetOrAdd(
            type,
            static t =>
            {
                var name = t.Name;
                var tick = name.IndexOf('`');
                return tick < 0 ? name : name[..tick];
            }
        );
}
