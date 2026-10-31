namespace Tralys.Ui.Mvvm;

/// <summary>Text a view model owns and a text field edits in place: each edit writes into it and raises
/// <see cref="Changed"/>, making no string, and a <see cref="Set(ReadOnlySpan{char})"/> from the owner replaces what
/// the field shows. Its reads report to <see cref="Observation"/>. An observed one changes only on its UI thread.</summary>
[FramePath]
public sealed class ObservableText : IObservable
{
    const int Content = 0;

    char[] _chars;
    int _length;
    int _version;
    Index? _caret;
    string? _string;
    int _stringVersion = -1;
    ObserverSet _observers;

    public ObservableText() => _chars = [];

    public ObservableText(ReadOnlySpan<char> text)
    {
        _chars = text.ToArray();
        _length = text.Length;
    }

    ref ObserverSet IObservable.Observers => ref _observers;

    /// <summary>Raised after every change, an edit or a <see cref="Set(ReadOnlySpan{char})"/>; a handler may
    /// <see cref="Set(ReadOnlySpan{char})"/> it again, as one that clamps or reformats what was typed does.</summary>
    public event Action? Changed;

    public ReadOnlySpan<char> Span
    {
        get
        {
            Observation.Read(this, Content);
            return _chars.AsSpan(0, _length);
        }
    }

    public int Length
    {
        get
        {
            Observation.Read(this, Content);
            return _length;
        }
    }

    public bool IsEmpty => Length == 0;

    /// <summary>Replaces the text, which a field showing it takes as an undo step. Nothing happens when it is
    /// unchanged.</summary>
    public void Set(ReadOnlySpan<char> text)
    {
        if (!text.SequenceEqual(_chars.AsSpan(0, _length)))
            Write(text, null);
    }

    /// <summary>Replaces the text as <see cref="Set(ReadOnlySpan{char})"/> does, even when unchanged, then has a field
    /// showing it put the caret at <paramref name="caret"/>: from the start, or as <c>^n</c>, from the end.</summary>
    public void Set(ReadOnlySpan<char> text, Index caret) => Write(text, caret);

    public void Clear() => Set(default);

    /// <summary>The text as a string, made once per change.</summary>
    public override string ToString()
    {
        Observation.Read(this, Content);
        if (_stringVersion != _version)
        {
            _string = _length == 0 ? "" : new string(_chars, 0, _length);
            _stringVersion = _version;
        }
        return _string!;
    }

    /// <summary>Counts its changes, so a field showing it can tell whether it changed since it last looked.</summary>
    public int Version
    {
        get
        {
            Observation.Read(this, Content);
            return _version;
        }
    }

    /// <summary>Where the latest change asked a field showing it to put the caret; null unless that change was a
    /// <see cref="Set(ReadOnlySpan{char}, Index)"/>.</summary>
    public Index? Caret => _caret;

    void Write(ReadOnlySpan<char> text, Index? caret)
    {
        if (_observers.IsOffThread())
            throw new InvalidOperationException(
                "An observed ObservableText changes only on its UI thread; post the change there with Dispatcher.Post."
            );
        if (_chars.Length < text.Length)
            _chars = new char[Math.Max(text.Length, Math.Max(16, _chars.Length * 2))];
        text.CopyTo(_chars);
        _length = text.Length;
        _version++;
        _caret = caret;
        _observers.Notify(this, Content);
        Changed?.Invoke();
    }
}
