namespace Tralys.Ui;

internal sealed partial class TextEditor
{
    const int MaxSteps = 100;
    const int MaxHistoryChars = 1 << 16;

    Step[] _steps = [];
    char[] _history = [];
    int _stepCount;
    int _done;
    int _historyLength;
    bool _undoEnabled = true;
    bool _masked;

    // Whether the open run of changes from outside has its step on top of the history.
    bool _outsideStep;

    // A masked text has no words, and keeps no history that would give it back.
    public void SetMasked(bool masked)
    {
        _masked = masked;
        SetUndoEnabled(!masked);
    }

    void SetUndoEnabled(bool enabled)
    {
        _undoEnabled = enabled;
        if (!enabled)
            ClearUndo();
    }

    public bool CanUndo => _done > 0;

    public bool CanRedo => _done < _stepCount;

    [FramePath]
    void ClearUndo()
    {
        _stepCount = 0;
        _done = 0;
        _historyLength = 0;
        _run = Run.None;
        SharedArrays<Step>.Release(ref _steps);
        SharedArrays<char>.Release(ref _history);
    }

    [FramePath]
    public bool Undo()
    {
        _run = Run.None;
        if (_done == 0)
            return false;

        ref readonly var step = ref _steps[--_done];
        Splice(
            step.Position,
            step.Position + step.InsertedLength,
            _history.AsSpan(step.Start, step.RemovedLength)
        );
        _caret = step.CaretBefore;
        _anchor = step.AnchorBefore;
        return true;
    }

    [FramePath]
    public bool Redo()
    {
        _run = Run.None;
        if (_done == _stepCount)
            return false;

        ref readonly var step = ref _steps[_done++];
        Splice(
            step.Position,
            step.Position + step.RemovedLength,
            _history.AsSpan(step.Start + step.RemovedLength, step.InsertedLength)
        );
        Collapse(step.Position + step.InsertedLength);
        return true;
    }

    [FramePath]
    void Record(
        int position,
        ReadOnlySpan<char> removed,
        ReadOnlySpan<char> inserted,
        int caretBefore,
        int anchorBefore
    )
    {
        if (!_undoEnabled)
            return;

        _historyLength = _done == 0 ? 0 : _steps[_done - 1].End;
        var chars = removed.Length + inserted.Length;
        while (_done > 0 && (_done >= MaxSteps || _historyLength + chars > MaxHistoryChars))
            Forget(Math.Max(1, _done / 4));

        if (_steps.Length == 0)
            _steps = SharedArrays<Step>.Rent(MaxSteps);
        ReserveHistory(_historyLength + chars);
        removed.CopyTo(_history.AsSpan(_historyLength));
        inserted.CopyTo(_history.AsSpan(_historyLength + removed.Length));
        _steps[_done++] = new Step(
            position,
            _historyLength,
            removed.Length,
            inserted.Length,
            caretBefore,
            anchorBefore
        );
        _stepCount = _done;
        _historyLength += chars;
    }

    [FramePath]
    bool Continues(Run run, int start, int end)
    {
        if (!_undoEnabled || _done == 0 || _done != _stepCount)
            return false;

        ref readonly var step = ref _steps[_done - 1];
        return run switch
        {
            Run.Typing => start == end && start == step.Position + step.InsertedLength,
            Run.Backspace => step.InsertedLength == 0 && end == step.Position,
            Run.Delete => step.InsertedLength == 0 && start == step.Position,
            _ => false,
        };
    }

    [FramePath]
    void Extend(Run run, int start, int end, ReadOnlySpan<char> text)
    {
        ref var step = ref _steps[_done - 1];
        var added = run == Run.Typing ? text : Text[start..end];
        ReserveHistory(_historyLength + added.Length);
        if (run == Run.Backspace)
        {
            Array.Copy(
                _history,
                step.Start,
                _history,
                step.Start + added.Length,
                _historyLength - step.Start
            );
            added.CopyTo(_history.AsSpan(step.Start));
            step.Position = start;
            step.RemovedLength += added.Length;
        }
        else
        {
            added.CopyTo(_history.AsSpan(_historyLength));
            if (run == Run.Typing)
                step.InsertedLength += added.Length;
            else
                step.RemovedLength += added.Length;
        }
        _historyLength += added.Length;
    }

    [FramePath]
    void Forget(int count)
    {
        var shift = count < _done ? _steps[count].Start : _historyLength;
        Array.Copy(_history, shift, _history, 0, _historyLength - shift);
        Array.Copy(_steps, count, _steps, 0, _done - count);
        _done -= count;
        _historyLength -= shift;
        for (var i = 0; i < _done; i++)
            _steps[i].Start -= shift;
    }

    [FramePath]
    void Restate(ReadOnlySpan<char> text)
    {
        if (Removed(_done - 1).SequenceEqual(text))
        {
            _historyLength = _steps[--_done].Start;
            _stepCount = _done;
            _outsideStep = false;
            return;
        }

        while (
            _done > 1
            && _steps[_done - 1].Start + _steps[_done - 1].RemovedLength + text.Length
                > MaxHistoryChars
        )
            Forget(Math.Min(Math.Max(1, _done / 4), _done - 1));

        ref var step = ref _steps[_done - 1];
        _historyLength = step.Start + step.RemovedLength;
        ReserveHistory(_historyLength + text.Length);
        text.CopyTo(_history.AsSpan(_historyLength));
        step.InsertedLength = text.Length;
        _historyLength += text.Length;
    }

    [FramePath]
    ReadOnlySpan<char> Removed(int step) =>
        _history.AsSpan(_steps[step].Start, _steps[step].RemovedLength);

    [FramePath]
    void ReserveHistory(int length) =>
        SharedArrays<char>.Grow(ref _history, length, _historyLength);

    // The removed characters, then the inserted ones, sit in the history from Start; a step that grows is the last.
    struct Step(
        int position,
        int start,
        int removedLength,
        int insertedLength,
        int caretBefore,
        int anchorBefore
    )
    {
        public int Position = position;
        public int Start = start;
        public int RemovedLength = removedLength;
        public int InsertedLength = insertedLength;
        public readonly int CaretBefore = caretBefore;
        public readonly int AnchorBefore = anchorBefore;

        public readonly int End => Start + RemovedLength + InsertedLength;
    }
}
