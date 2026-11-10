using System.Runtime.ExceptionServices;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Components;

/// <summary>Answers a field's text by filling its rows.</summary>
public interface ISuggestionSource
{
    /// <summary>Fills <paramref name="rows"/> for <paramref name="query"/>, now or once an answer lands.</summary>
    [FramePath]
    void Suggest(ReadOnlySpan<char> query, SuggestionList rows);

    /// <summary>Drops an answer still on its way.</summary>
    [FramePath]
    void Cancel();
}

/// <summary>One row under a field. An answer rewrites it in place rather than replacing it, so showing one allocates
/// nothing.</summary>
public sealed class SuggestionSlot
{
    internal SuggestionSlot() { }

    public string Text { get; private set; } = "";

    /// <summary>The caption above the first row of a group; empty for none.</summary>
    public string Header { get; private set; } = "";

    /// <summary>Quieter text beside the row's own; empty for none.</summary>
    public string Detail { get; private set; } = "";

    /// <summary>What the source attached to the row, for an owner that needs more than its text.</summary>
    public object? Value { get; private set; }

    [FramePath]
    internal void Show(string text, string header, object? value, string detail)
    {
        Text = text;
        Header = header;
        Value = value;
        Detail = detail;
    }
}

/// <summary>Whether an item a group offers matches the field's text.</summary>
public delegate bool SuggestionMatch<in T>(T item, ReadOnlySpan<char> query);

/// <summary>The text, attached value and detail of the row an item shows as.</summary>
public delegate (string Text, object? Value, string Detail) SuggestionRowOf<in T>(T item);

/// <summary>The rows under a field: a fixed set of <see cref="Slots"/> each answer rewrites, so showing one allocates
/// nothing. An answer runs from <see cref="BeginRows"/> through <see cref="AddRow(string, string, object?, string)"/>
/// to <see cref="EndRows"/>, which opens the rows if there are any, lighting the first; <see cref="Move"/> moves the lit
/// row through them. What renders it reads it tracked, so it shows each answer as it lands.</summary>
public sealed class SuggestionList : IObservable
{
    const int MaxPerGroup = 32;

    SuggestionSlot[] _slots;
    ObserverSet _observers;
    int _count;
    int _matches;
    int _selected = -1;
    bool _open;
    int _more;
    UiContext? _shownIn;
    int _showings;

    public SuggestionList(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _slots = Make(capacity);
    }

    ref ObserverSet IObservable.Observers => ref _observers;

    public IReadOnlyList<SuggestionSlot> Slots => _slots;

    /// <summary>The rows the last answer filled, which stay put while the list is closed.</summary>
    public int Count
    {
        get
        {
            Observation.Read(this, 0);
            return _count;
        }
    }

    /// <summary>Whether a final answer with no rows opens on the empty row instead of closing.</summary>
    public bool OpenWhenEmpty { get; set; }

    public bool IsOpen
    {
        get
        {
            Observation.Read(this, 0);
            return _open;
        }
    }

    /// <summary>Open with no rows, which only a final answer under <see cref="OpenWhenEmpty"/> reaches.</summary>
    public bool IsEmpty => IsOpen && _count == 0;

    /// <summary>Matches past the rows shown.</summary>
    public int MoreCount
    {
        get
        {
            Observation.Read(this, 0);
            return _more;
        }
    }

    /// <summary>The lit row while the list is open.</summary>
    public SuggestionSlot? Selected => IsOpen && _selected >= 0 ? _slots[_selected] : null;

    /// <summary>The context whose frame clock and thread an answer runs on, while rows show the list in a
    /// view.</summary>
    internal UiContext? ShownIn => _shownIn;

    internal void Show(UiContext context)
    {
        _shownIn = context;
        _showings++;
    }

    internal void Hide()
    {
        if (--_showings == 0)
            _shownIn = null;
    }

    /// <summary>Starts an answer; <see cref="AddRow(string, string, object?, string)"/> fills it and
    /// <see cref="EndRows"/> shows it.</summary>
    [FramePath]
    public void BeginRows()
    {
        _count = 0;
        _matches = 0;
    }

    /// <summary>Counts a match, and shows it while a slot is free.</summary>
    [FramePath]
    public void AddRow(string text, string header = "", object? value = null, string detail = "")
    {
        _matches++;
        if (_count < _slots.Length)
            _slots[_count++].Show(text, header, value, detail);
    }

    /// <summary>Counts matches a writer leaves out of the rows, so <see cref="MoreCount"/> still counts them.</summary>
    [FramePath]
    public void CountMore(int count) => _matches += count;

    /// <summary>One group's matches, matched once each: the first <paramref name="perGroup"/> as rows, the first of
    /// them under the header <paramref name="header"/> gives for the count of them all, the rest only towards
    /// <see cref="MoreCount"/>.</summary>
    public void AddGroup<T>(
        IReadOnlyList<T> items,
        ReadOnlySpan<char> query,
        SuggestionMatch<T> matches,
        SuggestionRowOf<T> rowOf,
        Func<int, string> header,
        int perGroup
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(perGroup);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(perGroup, MaxPerGroup);
        Span<int> shown = stackalloc int[perGroup];
        var count = 0;
        for (var i = 0; i < items.Count; i++)
        {
            if (!matches(items[i], query))
                continue;
            if (count < perGroup)
                shown[count] = i;
            count++;
        }
        var rows = Math.Min(count, perGroup);
        for (var i = 0; i < rows; i++)
        {
            var (text, value, detail) = rowOf(items[shown[i]]);
            AddRow(text, i == 0 ? header(count) : "", value, detail);
        }
        CountMore(count - rows);
    }

    /// <param name="final">Whether more rows may yet arrive for this text; only a final answer opens empty.</param>
    [FramePath]
    public void EndRows(bool final)
    {
        _open = _count > 0 || (final && OpenWhenEmpty);
        _more = _matches - _count;
        _selected = _count > 0 ? 0 : -1;
        Changed();
    }

    /// <summary>Lights the row <paramref name="direction"/> rows on, wrapping round the ends.</summary>
    [FramePath]
    public void Move(int direction)
    {
        if (!_open || _count == 0)
            return;
        _selected = ((_selected + direction) % _count + _count) % _count;
        Changed();
    }

    [FramePath]
    public void Select(SuggestionSlot slot)
    {
        var index = Array.IndexOf(_slots, slot);
        if (index < 0 || index >= _count || index == _selected)
            return;
        _selected = index;
        Changed();
    }

    /// <summary>Whether <paramref name="slot"/> is the lit row, which Enter takes.</summary>
    public bool IsLit(SuggestionSlot slot)
    {
        Observation.Read(this, 0);
        return _open && _selected >= 0 && ReferenceEquals(_slots[_selected], slot);
    }

    [FramePath]
    public void Close()
    {
        if (!_open && _selected < 0)
            return;
        _open = false;
        _selected = -1;
        Changed();
    }

    internal void Resize(int capacity)
    {
        if (capacity == _slots.Length)
            return;
        _slots = Make(capacity);
        _count = 0;
        _matches = 0;
        Close();
    }

    static SuggestionSlot[] Make(int capacity)
    {
        var slots = new SuggestionSlot[capacity];
        for (var i = 0; i < capacity; i++)
            slots[i] = new SuggestionSlot();
        return slots;
    }

    void Changed() => _observers.Notify(this, 0);
}

/// <summary>Names the client holds, matched on every keystroke ignoring case: those starting with the text first, then
/// those holding it, each name once.</summary>
public sealed class LocalSuggestions(Action<List<string>> fillNames) : ISuggestionSource
{
    readonly List<string> _names = [];
    readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    [FramePath]
    public void Suggest(ReadOnlySpan<char> query, SuggestionList rows)
    {
        _names.Clear();
        _seen.Clear();
        fillNames(_names);
        rows.BeginRows();
        for (var i = 0; i < _names.Count; i++)
        {
            if (
                _names[i].AsSpan().StartsWith(query, StringComparison.OrdinalIgnoreCase)
                && _seen.Add(_names[i])
            )
                rows.AddRow(_names[i]);
        }
        for (var i = 0; i < _names.Count; i++)
        {
            var name = _names[i].AsSpan();
            if (
                !name.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                && name.Contains(query, StringComparison.OrdinalIgnoreCase)
                && _seen.Add(_names[i])
            )
                rows.AddRow(_names[i]);
        }
        rows.EndRows(final: true);
    }

    public void Cancel() { }
}

/// <summary>Adds rows for <paramref name="query"/> between the list's own begin and end.</summary>
public delegate void SuggestionWriter(ReadOnlySpan<char> query, SuggestionList rows);

/// <summary>Names the server holds, asked once typing pauses, after any the client holds, which show on every
/// keystroke. Until the answer lands, the server's rows shown narrow to those still holding the text, and an answer to
/// text since changed is dropped. The pause runs on the frame clock of the context the rows show in; rows shown in no
/// view ask at once.</summary>
/// <remarks>The fetch may answer on any thread: an answer from another lands at the context's next update, on its
/// thread, as everything that touches the rows does. An answer that fails throws from the context's next update,
/// unless the text changed or the rows were dismissed since it was asked, as such an answer is dropped. Rows shown in
/// no view have no context to land on: they take the answer, or its failure, where the fetch finishes.</remarks>
public sealed class RemoteSuggestions<TReply> : ISuggestionSource, IDisposable
{
    static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(250);

    readonly Func<string, ValueTask<TReply>> _fetch;
    readonly Func<TReply, SuggestionList, bool> _write;
    readonly SuggestionWriter? _local;
    Held[] _held = [];
    int _serverStart;
    char[] _query = new char[32];
    int _queryLength;
    string _asked = "";
    int _generation;
    SuggestionList? _rows;
    UiContext? _clock;
    TimeSpan _due;
    bool _waiting;
    bool _ticking;
    bool _disposed;

    /// <param name="fetch">Asks the server for the text; called once per pause, not per keystroke.</param>
    /// <param name="write">Adds the reply's rows between the list's own begin and end; false refuses the reply, so
    /// the list never opens empty on it.</param>
    /// <param name="local">Adds the rows the client holds, ahead of the server's.</param>
    public RemoteSuggestions(
        Func<string, ValueTask<TReply>> fetch,
        Func<TReply, SuggestionList, bool> write,
        SuggestionWriter? local = null
    )
    {
        _fetch = fetch;
        _write = write;
        _local = local;
    }

    [FramePath]
    public void Suggest(ReadOnlySpan<char> query, SuggestionList rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _rows = rows;
        _generation++;
        Narrow(query, rows);
        if (query.Length > _query.Length)
            _query = new char[query.Length * 2];
        query.CopyTo(_query);
        _queryLength = query.Length;
        if (rows.ShownIn is not { } clock)
        {
            _waiting = false;
            Ask();
            return;
        }
        _clock = clock;
        _due = clock.Now + Pause;
        _waiting = true;
        Wait(clock);
    }

    [FramePath]
    public void Cancel()
    {
        _generation++;
        _waiting = false;
    }

    public void Dispose()
    {
        _disposed = true;
        Cancel();
    }

    [FramePath]
    void Wait(UiContext clock)
    {
        if (_ticking)
            return;
        _ticking = true;
        clock.Dispatcher.Post(this, static source => source.Tick());
    }

    [FramePath]
    void Tick()
    {
        _ticking = false;
        if (!_waiting || _clock is not { } clock)
            return;
        if (clock.Now < _due)
        {
            Wait(clock);
            return;
        }
        _waiting = false;
        Ask();
    }

    [FramePath]
    void Narrow(ReadOnlySpan<char> query, SuggestionList rows)
    {
        if (_held.Length < rows.Slots.Count)
            _held = new Held[rows.Slots.Count];
        var held = 0;
        var header = "";
        var shown = rows.IsOpen ? rows.Count : 0;
        for (var i = _serverStart; i < shown; i++)
        {
            var slot = rows.Slots[i];
            if (slot.Header.Length > 0)
                header = slot.Header;
            if (!slot.Text.AsSpan().Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;
            _held[held++] = new Held(slot.Text, header, slot.Value, slot.Detail);
            header = "";
        }
        rows.BeginRows();
        WriteLocal(query, rows);
        for (var i = 0; i < held; i++)
            rows.AddRow(_held[i].Text, _held[i].Header, _held[i].Value, _held[i].Detail);
        Array.Clear(_held, 0, held);
        rows.EndRows(final: false);
    }

    [FramePath]
    void WriteLocal(ReadOnlySpan<char> query, SuggestionList rows)
    {
        _local?.Invoke(query, rows);
        _serverStart = rows.Count;
    }

    void Ask()
    {
        var query = _query.AsSpan(0, _queryLength);
        if (!query.SequenceEqual(_asked))
            _asked = new string(query);
        var generation = _generation;
        var reply = _fetch(_asked);
        if (reply.IsCompleted)
            Answer(reply.Result, generation);
        else
            _ = Await(reply, generation);
    }

    async Task Await(ValueTask<TReply> reply, int generation)
    {
        TReply answer;
        try
        {
            answer = await reply;
        }
        catch (Exception e) when (_clock is { } clock)
        {
            clock.Dispatcher.Post(
                (Source: this, Error: ExceptionDispatchInfo.Capture(e), Generation: generation),
                static failed => failed.Source.Fail(failed.Error, failed.Generation)
            );
            return;
        }
        if (_clock is { } context && !context.Dispatcher.CheckAccess())
            context.Dispatcher.Post(
                (Source: this, Reply: answer, Generation: generation),
                static answered => answered.Source.Answer(answered.Reply, answered.Generation)
            );
        else
            Answer(answer, generation);
    }

    void Fail(ExceptionDispatchInfo error, int generation)
    {
        if (generation == _generation)
            error.Throw();
    }

    void Answer(TReply reply, int generation)
    {
        if (generation != _generation || _rows is not { } rows)
            return;
        // A lit row other than the first is one the user picked; a default pick follows the answer's first row.
        string? litText = null;
        object? litValue = null;
        if (rows.Selected is { } lit && lit != rows.Slots[0])
        {
            litText = lit.Text;
            litValue = lit.Value;
        }
        rows.BeginRows();
        WriteLocal(_asked, rows);
        var answered = _write(reply, rows);
        rows.EndRows(final: answered);
        if (litText is not null)
            Relight(rows, litText, litValue);
    }

    static void Relight(SuggestionList rows, string text, object? value)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var slot = rows.Slots[i];
            if (slot.Text == text && Equals(slot.Value, value))
            {
                rows.Select(slot);
                return;
            }
        }
    }

    readonly record struct Held(string Text, string Header, object? Value, string Detail);
}
