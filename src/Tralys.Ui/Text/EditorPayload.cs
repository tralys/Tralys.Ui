using System.Numerics;
using Tralys.Ui.Fonts;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui;

/// <summary>A selection in an editable text: where it started and where the caret is, in UTF-16 units.</summary>
[FramePath]
public readonly record struct TextSelection(int Anchor, int Caret);

[FramePath]
internal record struct EditorProps
{
    public Callback OnEdit;
    public Callback OnCommit;
    public Callback OnSubmit;
    public Callback OnEscape;
    public Callback<TextSelection> OnSelectionChange;
    public TextInputKind Kind;
    public bool Multiline;
    public bool Wrap;
    public bool ReadOnly;
    public bool Masked;
    public bool KeepSelectionOnFocus;
    public int MaxLength;
    public int MaxBytes;
    public int TabIndex;
    public NodeRef? Next;
    public Paint SelectionColor;
    public Paint CaretColor;
    public Paint PlaceholderColor;

    public readonly bool Masking => Masked || Kind == TextInputKind.Password;

    public readonly bool LaysOutLike(in EditorProps other) =>
        Wrap == other.Wrap && Masked == other.Masked;

    public readonly bool DrawsLike(in EditorProps other) =>
        LaysOutLike(other)
        && ReadOnly == other.ReadOnly
        && SelectionColor == other.SelectionColor
        && CaretColor == other.CaretColor
        && PlaceholderColor == other.PlaceholderColor;
}

[FramePath]
internal sealed class EditorPayload : NodePayload, ITabIndexed, IEditCommandTarget
{
    const char MaskChar = '\u2022';
    const int Unsynced = -1;
    static readonly long BlinkTicks = TimeSpan.FromMilliseconds(530).Ticks;

    internal readonly TextEditor Editor = new() { AcceptsLineBreaks = false };
    internal readonly Handler<PointerEvent> OnPointerDown;
    internal readonly Handler<PointerEvent> OnPointerMove;
    internal readonly Handler<PointerEvent> OnPointerUp;
    internal readonly Handler<PointerEvent> OnPointerCancel;
    internal readonly Handler<KeyEvent> OnKeyDown;
    internal readonly Callback<FocusChange> OnFocusChange;
    readonly TextBuffer _display;
    readonly TextBuffer _placeholder;
    readonly TextLayout _layout = new();
    readonly TextLayout _measured = new();
    readonly TextLayout _placeholderLayout = new();
    readonly TextLayout _placeholderMeasured = new();

    internal EditorProps Props;
    internal Node? Node;
    internal UiView? View;
    internal RenderCommandBuffer? CaretCommands;
    internal int CaretSpan = -1;
    internal long CaretSpanList;
    internal bool CaretDirty;
    internal Size CaretRecordedSize;
    internal bool Reconciling;
    internal bool Leaving;
    internal bool ByContact;

    bool _written;
    ObservableText? _text;
    int _synced = Unsynced;
    bool _commitPending;

    Paint _textFill;
    Paint _placeholderFill;
    Paint _selectionFill;
    Paint _caretFill;
    TextAlign _align;
    float _lineHeight = float.NaN;

    int _displayVersion = -1;
    bool _displayMasked;
    char[] _mask = [];
    int[] _stops = [];
    int _stopCount;
    Rect[] _rects = new Rect[4];
    int _paintedStart = -1;
    int _paintedEnd = -1;

    float _width = float.NaN;
    float _height;
    float _scrollX;
    float _scrollY;
    float _alignX;
    float _placeholderAlignX;
    bool _reveal = true;
    bool _upstream;
    float _column = float.NaN;

    TimeSpan _blinkStart;
    bool _caretOn = true;

    bool _selecting;
    Pointer _selectingPointer;
    DragUnit _dragUnit;
    int _wordStart;
    int _wordEnd;
    bool _pressByContact;
    int _shownCaret;
    int _shownAnchor;

    EditorPayload(TextStyle style)
    {
        _display = new TextBuffer(style);
        _placeholder = new TextBuffer(style);
        OnPointerDown = Handler<PointerEvent>.Of(
            this,
            static (EditorPayload p, ref PointerEvent e) => p.PointerDown(ref e)
        );
        OnPointerMove = Handler<PointerEvent>.Of(
            this,
            static (EditorPayload p, ref PointerEvent e) => p.PointerMove(ref e)
        );
        OnPointerUp = Handler<PointerEvent>.Of(
            this,
            static (EditorPayload p, ref PointerEvent e) => p.PointerUp(ref e)
        );
        OnPointerCancel = Handler<PointerEvent>.Of(
            this,
            static (EditorPayload p, ref PointerEvent e) => p.PointerCancel(ref e)
        );
        OnKeyDown = Handler<KeyEvent>.Of(
            this,
            static (EditorPayload p, ref KeyEvent e) => p.KeyDown(ref e)
        );
        OnFocusChange = Callback<FocusChange>.Of(this, static (p, c) => p.FocusChanged(c));
    }

    internal static EditorPayload Rent(UiView view, Node node)
    {
        var payload =
            Pool<EditorPayload>.TryRent()
            ?? new EditorPayload(
                new TextStyle(
                    view.Context.Text.Resolve(null, FontWeight.Normal, FontStyle.Normal),
                    14f,
                    default
                )
            );
        payload.View = view;
        payload.Node = node;
        return payload;
    }

    public override void Release(UiView view)
    {
        Editor.SetMasked(false);
        Editor.Release();
        Editor.MaxLength = 0;
        Editor.MaxBytes = 0;
        Editor.AcceptsLineBreaks = false;
        _display.Release();
        _placeholder.Release();
        _layout.Release();
        _measured.Release();
        _placeholderLayout.Release();
        _placeholderMeasured.Release();
        Props = default;
        Node = null;
        View = null;
        CaretCommands?.Release();
        CaretSpan = -1;
        CaretSpanList = 0;
        CaretDirty = false;
        CaretRecordedSize = default;
        Reconciling = Leaving = ByContact = false;
        _written = false;
        _text = null;
        _synced = Unsynced;
        _commitPending = false;
        _lineHeight = float.NaN;
        _align = default;
        _displayVersion = -1;
        _displayMasked = false;
        _paintedStart = _paintedEnd = -1;
        _width = float.NaN;
        _height = 0f;
        _scrollX = _scrollY = _alignX = _placeholderAlignX = 0f;
        _reveal = true;
        _upstream = false;
        _column = float.NaN;
        _caretOn = true;
        _selecting = false;
        _pressByContact = false;
        _shownCaret = _shownAnchor = 0;
        Pool<EditorPayload>.Return(this);
    }

    internal ReadOnlySpan<char> Text => Editor.Text;

    internal TextSelection Selection => new(Editor.Anchor, Editor.Caret);

    bool Focused => Node is { } node && (node._states & InputStates.Focused) != 0;

    internal bool Editable => !Props.ReadOnly;

    internal bool CaretOn => _caretOn;

    internal bool CaretShown =>
        Focused && !Props.ReadOnly && !Editor.HasSelection && _caretFill.IsVisible;

    bool SelectionShown => Focused && Editor.HasSelection && _selectionFill.IsVisible;

    internal TextInputKind InputKind => Props.Masking ? TextInputKind.Password : Props.Kind;

    bool LinesMove => Props.Multiline || Props.Wrap;

    bool CanCopy => Editor.HasSelection && !Props.Masking;

    bool CanCut => CanCopy && !Props.ReadOnly;

    bool CanUndo => !Props.ReadOnly && Editor.CanUndo;

    bool CanRedo => !Props.ReadOnly && Editor.CanRedo;

    internal bool Write(
        UiView view,
        Node node,
        ObservableText text,
        in EditorProps props,
        ReadOnlySpan<char> placeholder
    )
    {
        var changed = false;
        if (Props != props)
        {
            var old = Props;
            Props = props;
            changed = true;
            Editor.MaxLength = props.MaxLength;
            Editor.MaxBytes = props.MaxBytes;
            Editor.AcceptsLineBreaks = props.Multiline;
            if (props.Masking != old.Masking || !_written)
                Editor.SetMasked(props.Masking);
            if (props.ReadOnly != old.ReadOnly || props.Masking != old.Masking)
                view.EditStateChanged();
            if (!old.LaysOutLike(props))
            {
                _reveal = true;
                view.InvalidateMeasure(node);
            }
            if (!old.DrawsLike(props))
            {
                Recolor(node);
                view.MarkPaint(node);
                view.MarkCaret(node);
            }
        }

        if (!placeholder.SequenceEqual(_placeholder.Text))
        {
            _placeholder.SetText(placeholder);
            view.InvalidateMeasure(node);
            view.MarkPaint(node);
            changed = true;
        }

        var version = text.Version;
        if (!_written)
        {
            _written = true;
            _text = text;
            _synced = version;
            Editor.SetText(text.Span);
            _shownCaret = Editor.Caret;
            _shownAnchor = Editor.Anchor;
            return true;
        }
        if (!ReferenceEquals(text, _text))
        {
            _text = text;
            _synced = Unsynced;
        }
        if (version != _synced)
        {
            view.QueueReconcile(this);
            changed = true;
        }
        return changed;
    }

    internal void Reconcile(UiView view)
    {
        Reconciling = false;
        if (Node is not { IsMounted: true } || _text is not { } text || text.Version == _synced)
            return;
        _synced = text.Version;
        var caret = text.Caret;
        if (!Editor.Replace(text.Span))
        {
            if (caret is { } at)
                PlaceCaret(view, at);
            return;
        }
        _upstream = false;
        _column = float.NaN;
        TextChanged(view);
        ReportSelection(view);
        _commitPending = true;
        Commit(view);
        if (caret is { } placed)
            PlaceCaret(view, placed);
    }

    void Edited(UiView view)
    {
        _upstream = false;
        _column = float.NaN;
        TextChanged(view);
        _commitPending = true;
        if (_text is { } text)
        {
            view.Write(text, Editor.Text);
            if (text.Caret is null && text.Span.SequenceEqual(Editor.Text))
                _synced = text.Version;
            else
                view.QueueReconcile(this);
        }
        ReportSelection(view);
        if (!Props.OnEdit.IsEmpty)
            view.Raise(Props.OnEdit);
    }

    void TextChanged(UiView view)
    {
        view.EditStateChanged();
        _reveal = true;
        RestartBlink(view);
        if (Node is { IsMounted: true } node)
        {
            view.InvalidateMeasure(node);
            view.MarkPaint(node);
            view.MarkCaret(node);
        }
    }

    void Moved(UiView view, bool keepColumn = false)
    {
        view.EditStateChanged();
        if (!keepColumn)
            _column = float.NaN;
        _reveal = true;
        RestartBlink(view);
        if (Node is { IsMounted: true } node)
        {
            view.MarkCaret(node);
            var selectionStale = SelectionShown
                ? _paintedStart != Editor.SelectionStart || _paintedEnd != Editor.SelectionEnd
                : _paintedStart >= 0;
            if (selectionStale)
                view.MarkPaint(node);
            if (!float.IsNaN(_width) && LaidOut(view))
                Scroll(view, node);
        }
        ReportSelection(view);
    }

    void ReportSelection(UiView view)
    {
        if (Editor.Caret == _shownCaret && Editor.Anchor == _shownAnchor)
            return;
        _shownCaret = Editor.Caret;
        _shownAnchor = Editor.Anchor;
        if (!Props.OnSelectionChange.IsEmpty)
            view.Raise(Props.OnSelectionChange, new TextSelection(_shownAnchor, _shownCaret));
    }

    internal void Commit(UiView view)
    {
        if (!_commitPending)
            return;
        _commitPending = false;
        if (!Props.OnCommit.IsEmpty)
            view.Raise(Props.OnCommit);
    }

    void RestartBlink(UiView view)
    {
        _blinkStart = view.Context.Now;
        if (_caretOn)
            return;
        _caretOn = true;
        if (Node is { IsMounted: true } node)
            view.MarkCaret(node);
    }

    internal void Blink(UiView view, TimeSpan now)
    {
        if (!CaretShown || Node is not { } node)
            return;
        var on = ((now - _blinkStart).Ticks / BlinkTicks & 1) == 0;
        if (on == _caretOn)
            return;
        _caretOn = on;
        view.MarkCaret(node);
    }

    void FocusChanged(FocusChange change)
    {
        if (View is not { } view || Node is not { } node)
            return;
        if (change.Focused)
        {
            if (
                change.Cause != FocusCause.Pointer
                && !Props.KeepSelectionOnFocus
                && !view.FocusReturning
            )
            {
                Editor.SelectAll();
                _upstream = false;
            }
            ByContact = change.Cause == FocusCause.Pointer && _pressByContact;
            view.MarkPaint(node);
            Moved(view);
            return;
        }

        EndSelecting(view);
        view.StopTyping(this);
        view.MarkPaint(node);
        view.MarkCaret(node);
        Commit(view);
    }

    internal void Detached(UiView view)
    {
        _selecting = false;
        view.StopTyping(this);
        if (_commitPending)
            view.QueueLeft(this);
    }

    internal void CaretToEnd(UiView view) => CaretTo(view, Index.End);

    void CaretTo(UiView view, Index at)
    {
        Editor.MoveCaret(at.GetOffset(Editor.Length), extend: false);
        _upstream = false;
        Moved(view);
    }

    void PlaceCaret(UiView view, Index at)
    {
        Editor.PlaceCaret(at.GetOffset(Editor.Length));
        _upstream = false;
        Moved(view);
    }

    internal bool TypeText(UiView view, ReadOnlySpan<char> text)
    {
        if (Props.ReadOnly)
            return false;
        if (Editor.Type(text))
            Edited(view);
        return true;
    }

    void KeyDown(ref KeyEvent e)
    {
        if (View is not { } view)
            return;
        if (Handle(view, e.Key, e.Modifiers))
        {
            e.StopPropagation();
            e.PreventDefault();
        }
        else if (Editable && Types(e.Key, e.Modifiers))
        {
            e.StopPropagation();
            e.Typed = true;
        }
    }

    static bool Types(Key key, ModifierKeys modifiers) =>
        (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Gui)) == 0
        && key
            is Key.Space
                or (>= Key.D0 and <= Key.D9)
                or (>= Key.A and <= Key.Z)
                or (>= Key.NumPad0 and <= Key.Divide)
                or (>= Key.OemSemicolon and <= Key.OemQuotes);

    bool Handle(UiView view, Key key, ModifierKeys modifiers)
    {
        if (key == Key.Escape)
        {
            if (Props.OnEscape.IsEmpty)
                return false;
            Commit(view);
            view.Raise(Props.OnEscape);
            return true;
        }
        if (key == Key.Enter)
            return Enter(view);
        if ((modifiers & (ModifierKeys.Alt | ModifierKeys.Gui)) != 0)
            return false;

        var control = (modifiers & ModifierKeys.Control) != 0;
        var shift = (modifiers & ModifierKeys.Shift) != 0;
        switch (key)
        {
            case Key.Left or Key.Right:
                Step(view, key == Key.Right, control, shift);
                return true;
            case Key.Up or Key.Down when LinesMove:
            {
                var index = LineStep(view, key == Key.Down, out var upstream);
                MoveTo(view, index, shift, upstream, keepColumn: true);
                return true;
            }
            case Key.Home or Key.End when control:
                MoveTo(view, key == Key.End ? Editor.Length : 0, shift, upstream: false);
                return true;
            case Key.Home or Key.End:
            {
                var index = LineEdge(view, key == Key.End, out var upstream);
                MoveTo(view, index, shift, upstream);
                return true;
            }
            case Key.BackSpace:
                return Erase(view, forward: false, word: control);
            case Key.Delete when shift && !control:
                return Perform(view, EditCommand.Cut);
            case Key.Delete:
                return Erase(view, forward: true, word: control);
            case Key.Insert when control && !shift:
                return Perform(view, EditCommand.Copy);
            case Key.Insert when shift && !control:
                return Perform(view, EditCommand.Paste);
            case Key.X when control && !shift:
                return Perform(view, EditCommand.Cut);
            case Key.C when control && !shift:
                return Perform(view, EditCommand.Copy);
            case Key.V when control && !shift:
                return Perform(view, EditCommand.Paste);
            case Key.A when control && !shift:
                return Perform(view, EditCommand.SelectAll);
            case Key.Z when control:
                return Perform(view, shift ? EditCommand.Redo : EditCommand.Undo);
            case Key.Y when control && !shift:
                return Perform(view, EditCommand.Redo);
            default:
                return false;
        }
    }

    bool Enter(UiView view)
    {
        if (Props.ReadOnly)
            return false;
        if (Props.Next?.Focus() == true)
            return true;
        Commit(view);
        if (!Props.OnSubmit.IsEmpty)
        {
            view.Raise(Props.OnSubmit);
            return true;
        }
        if (!Props.Multiline)
            return false;
        if (Editor.Insert("\n"))
            Edited(view);
        return true;
    }

    int ITabIndexed.TabIndex => Props.TabIndex;

    bool IEditCommandTarget.CanRun(EditCommand command) => CanPerform(command);

    void IEditCommandTarget.Run(UiView view, EditCommand command) => Perform(view, command);

    internal bool CanPerform(EditCommand command) =>
        command switch
        {
            EditCommand.Cut => CanCut,
            EditCommand.Copy => CanCopy,
            EditCommand.Paste or EditCommand.Delete => !Props.ReadOnly,
            EditCommand.Undo => CanUndo,
            EditCommand.Redo => CanRedo,
            EditCommand.SelectAll => true,
            _ => false,
        };

    internal bool Perform(UiView view, EditCommand command)
    {
        if (!CanPerform(command))
            return false;
        switch (command)
        {
            case EditCommand.Copy:
                view.SetClipboard(Editor.SelectedText);
                break;
            case EditCommand.Cut:
                view.SetClipboard(Editor.SelectedText);
                if (Editor.Backspace())
                    Edited(view);
                break;
            case EditCommand.Paste:
                if (view.ClipboardText() is { Length: > 0 } pasted && Editor.Insert(pasted))
                    Edited(view);
                break;
            case EditCommand.Delete:
                return Erase(view, forward: true, word: false);
            case EditCommand.Undo:
                if (Editor.Undo())
                    Edited(view);
                break;
            case EditCommand.Redo:
                if (Editor.Redo())
                    Edited(view);
                break;
            case EditCommand.SelectAll:
                Editor.SelectAll();
                _upstream = false;
                Moved(view);
                break;
        }
        return true;
    }

    bool Erase(UiView view, bool forward, bool word)
    {
        if (Props.ReadOnly)
            return false;
        if (forward ? Editor.Delete(word) : Editor.Backspace(word))
            Edited(view);
        return true;
    }

    void Step(UiView view, bool forward, bool word, bool extend)
    {
        var caret = Editor.Caret;
        int index;
        if (!extend && !word && Editor.HasSelection)
            index = forward ? Editor.SelectionEnd : Editor.SelectionStart;
        else if (word)
            index = forward ? Editor.NextWord(caret) : Editor.PreviousWord(caret);
        else
            index = forward ? Editor.NextCluster(caret) : Editor.PreviousCluster(caret);
        MoveTo(view, index, extend, upstream: false);
    }

    void MoveTo(UiView view, int index, bool extend, bool upstream, bool keepColumn = false)
    {
        Editor.MoveCaret(index, extend);
        _upstream = upstream;
        Moved(view, keepColumn);
    }

    void PointerDown(ref PointerEvent e)
    {
        if (View is not { } view)
            return;
        var touch = e.Pointer.Kind != PointerKind.Mouse;
        if (!touch && e.Button != MouseButton.Left)
            return;
        _pressByContact = touch;
        if (touch)
            view.TouchedWhileTyping(this);

        _dragUnit = e.ClickCount switch
        {
            >= 3 => DragUnit.All,
            2 => DragUnit.Word,
            _ => DragUnit.Cluster,
        };
        DragTo(
            view,
            e.Position,
            extend: _dragUnit == DragUnit.Cluster && (e.Modifiers & ModifierKeys.Shift) != 0
        );
        _selecting = true;
        _selectingPointer = e.Pointer;
        e.Capture();
        Moved(view);
        e.StopPropagation();
    }

    void PointerMove(ref PointerEvent e)
    {
        if (!_selecting || e.Pointer != _selectingPointer || View is not { } view)
            return;
        if ((e.Buttons & MouseButtons.Left) == 0)
        {
            _selecting = false;
            e.ReleaseCapture();
            return;
        }
        DragTo(view, e.Position, extend: true);
        Moved(view);
        e.StopPropagation();
    }

    void PointerUp(ref PointerEvent e)
    {
        if (!_selecting || e.Pointer != _selectingPointer)
            return;
        if (e.Pointer.Kind == PointerKind.Mouse && e.Button != MouseButton.Left)
            return;
        _selecting = false;
        e.ReleaseCapture();
        e.StopPropagation();
    }

    void PointerCancel(ref PointerEvent e)
    {
        if (e.Pointer == _selectingPointer)
            _selecting = false;
    }

    void EndSelecting(UiView view)
    {
        if (!_selecting)
            return;
        _selecting = false;
        if (Node is { } node)
            view.ReleaseCapture(node, _selectingPointer);
    }

    void DragTo(UiView view, Vector2 point, bool extend)
    {
        switch (_dragUnit)
        {
            case DragUnit.All:
                Editor.SelectAll();
                _upstream = false;
                break;
            case DragUnit.Word:
            {
                var (start, end) = Editor.WordAt(ClusterAt(view, point));
                if (!extend)
                    (_wordStart, _wordEnd) = (start, end);
                if (start < _wordStart)
                    Editor.Select(_wordEnd, start);
                else
                    Editor.Select(_wordStart, Math.Max(end, _wordEnd));
                _upstream = false;
                break;
            }
            default:
            {
                var index = HitTest(view, point, out var upstream);
                Editor.MoveCaret(index, extend);
                _upstream = upstream;
                break;
            }
        }
    }

    Vector2 Origin => new(_alignX - _scrollX, -_scrollY);

    int HitTest(UiView view, Vector2 point, out bool upstream)
    {
        upstream = false;
        if (!LaidOut(view))
            return 0;
        return FromDisplay(_layout.HitTest(point - Origin, out upstream));
    }

    // A letter's right half belongs to that letter's word, though the caret lands after it.
    int ClusterAt(UiView view, Vector2 point)
    {
        if (!LaidOut(view))
            return 0;
        var local = point - Origin;
        var display = _layout.HitTest(local, out var upstream);
        var index = FromDisplay(display);
        var line = _layout.GetLineAt(display, upstream);
        return
            display > _layout.Lines[line].Start
            && local.X < _layout.GetCaretRect(display, upstream).X
            ? Editor.PreviousCluster(index)
            : index;
    }

    int LineEdge(UiView view, bool end, out bool upstream)
    {
        upstream = false;
        if (!LaidOut(view))
            return end ? Editor.Length : 0;
        var lines = _layout.Lines;
        var line = _layout.GetLineAt(ToDisplay(Editor.Caret), _upstream);
        if (!end)
            return FromDisplay(lines[line].Start);
        var edge = _layout.GetLineEnd(line);
        upstream = line < lines.Length - 1 && edge == lines[line + 1].Start;
        return FromDisplay(edge);
    }

    int LineStep(UiView view, bool down, out bool upstream)
    {
        upstream = false;
        if (!LaidOut(view))
            return down ? Editor.Length : 0;
        var display = ToDisplay(Editor.Caret);
        var line = _layout.GetLineAt(display, _upstream);
        if (float.IsNaN(_column))
            _column = _layout.GetCaretRect(display, _upstream).X;
        var target = line + (down ? 1 : -1);
        var lines = _layout.Lines;
        if (target < 0)
            return 0;
        if (target >= lines.Length)
            return Editor.Length;
        ref readonly var to = ref lines[target];
        return FromDisplay(
            _layout.HitTest(new Vector2(_column, to.Top + to.Height * 0.5f), out upstream)
        );
    }

    internal void Restyle(UiView view, Node node)
    {
        ref readonly var env = ref node.Env;
        var font = view.Context.Text.Resolve(
            env.FontFamily,
            env.FontWeight == 0 ? FontWeight.Normal : env.FontWeight,
            env.FontStyle
        );
        var style = new TextStyle(
            font,
            env.FontSize > 0f ? env.FontSize : 14f,
            env.Color,
            0f,
            default,
            env.TabularDigits,
            (int)MathF.Round(env.LetterSpacing * 1000f)
        );
        var lineHeight = env.LineHeight > 0f ? env.LineHeight : float.NaN;
        var current = _display.Style;
        var lays =
            current.LaysOutLike(style) && _lineHeight.Equals(lineHeight) && _align == env.TextAlign;
        _textFill = env.Color;
        _lineHeight = lineHeight;
        _align = env.TextAlign;
        _display.Style = style;
        Recolor(node);
        if (!lays)
        {
            _reveal = true;
            view.InvalidateMeasure(node);
        }
        view.MarkPaint(node);
        view.MarkCaret(node);
    }

    void Recolor(Node node)
    {
        var theme = node.Env.Theme;
        if (theme is null)
            return;
        _selectionFill =
            Props.SelectionColor.Kind != PaintKind.None
                ? Props.SelectionColor
                : theme.Color(ColorToken.BgTextSelection);
        _caretFill = Props.CaretColor.Kind != PaintKind.None ? Props.CaretColor : _textFill;
        _placeholderFill =
            Props.PlaceholderColor.Kind != PaintKind.None
                ? Props.PlaceholderColor
                : theme.Color(ColorToken.FgPlaceholder);
        var style = _display.Style;
        _placeholder.Style = new TextStyle(
            style.Font,
            style.Size,
            _placeholderFill,
            0f,
            default,
            style.TabularDigits,
            style.CharacterSpacing
        );
    }

    void Prepare()
    {
        var masked = Props.Masked;
        if (_displayVersion != Editor.Version || masked != _displayMasked)
            Show(masked);
    }

    void Show(bool masked)
    {
        var text = Editor.Text;
        _displayVersion = Editor.Version;
        _displayMasked = masked;
        if (!masked)
        {
            _display.SetText(text);
            return;
        }
        ValueList.EnsureCapacity(ref _stops, text.Length + 1);
        ValueList.EnsureCapacity(ref _mask, text.Length);
        var count = 0;
        for (
            var index = 0;
            index < text.Length;
            index = TextClusters.Next(text, index, text.Length)
        )
        {
            _stops[count] = index;
            _mask[count++] = MaskChar;
        }
        _stops[count] = text.Length;
        _stopCount = count + 1;
        _display.SetText(_mask.AsSpan(0, count));
    }

    int ToDisplay(int index)
    {
        if (!_displayMasked)
            return index;
        var low = 0;
        var high = _stopCount - 1;
        while (low < high)
        {
            var middle = (low + high + 1) >> 1;
            if (_stops[middle] <= index)
                low = middle;
            else
                high = middle - 1;
        }
        return low;
    }

    int FromDisplay(int index) =>
        _displayMasked ? _stops[Math.Clamp(index, 0, _stopCount - 1)] : index;

    void Lay(TextLayout layout, TextBuffer buffer, float maxWidth, float scale)
    {
        layout.Scale = scale;
        layout.Wrapping = Props.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        layout.LineHeight = _lineHeight;
        layout.MaxWidth = Props.Wrap ? MathF.Max(0f, maxWidth) : float.PositiveInfinity;
        layout.Update(buffer);
    }

    // An edit since the last layout still has to be laid out before a caret or a hit is placed in it.
    bool LaidOut(UiView view)
    {
        if (float.IsNaN(_width))
            return false;
        Prepare();
        var caret = CaretWidth(view.Scale);
        Lay(_layout, _display, _width - caret, view.Scale);
        return true;
    }

    static float CaretWidth(float scale) => MathF.Max(1f, MathF.Round(scale)) / scale;

    static float Ceil(float value, float scale) => MathF.Ceiling(value * scale - 1e-3f) / scale;

    internal Size Measure(UiView view, Avail width)
    {
        var scale = view.Scale;
        Prepare();
        var caret = CaretWidth(scale);
        var room = width.IsBounded ? width.Value - caret : float.PositiveInfinity;
        Lay(_measured, _display, room, scale);
        Lay(_placeholderMeasured, _placeholder, room, scale);
        var text = _measured.Size;
        var placeholder = _placeholder.Text.IsEmpty ? default : _placeholderMeasured.Size;
        var height = MathF.Max(text.Height, placeholder.Height);
        var wants = Ceil(MathF.Max(text.Width, placeholder.Width) + caret, scale);
        var w = width.Kind switch
        {
            AvailKind.Exact => width.Value,
            AvailKind.AtMost => MathF.Min(wants, width.Value),
            AvailKind.MinContent => caret,
            _ => wants,
        };
        return new Size(w, height);
    }

    internal float Baseline(UiView view, float width)
    {
        Prepare();
        Lay(_measured, _display, width - CaretWidth(view.Scale), view.Scale);
        var lines = _measured.Lines;
        return lines.Length > 0 ? lines[0].Baseline : _measured.Size.Height;
    }

    internal void Arrange(UiView view, Node node, Size size)
    {
        if (size.Width != _width || size.Height != _height)
            _reveal = true;
        _width = size.Width;
        _height = size.Height;
        LaidOut(view);
        Lay(_placeholderLayout, _placeholder, _width - CaretWidth(view.Scale), view.Scale);
        Scroll(view, node);
    }

    void Scroll(UiView view, Node node)
    {
        var caret = CaretWidth(view.Scale);
        var size = _layout.Size;
        var wraps = Props.Wrap;
        var alignX = Aligned(size.Width, caret, wraps);
        var scrollX = _scrollX;
        var scrollY = _scrollY;
        var at = _layout.GetCaretRect(ToDisplay(Editor.Caret), _upstream);
        var left = alignX + at.X;
        if (_reveal)
        {
            _reveal = false;
            if (left < scrollX)
                scrollX = left;
            else if (left + caret > scrollX + _width)
                scrollX = left + caret - _width;
            if (at.Y < scrollY)
                scrollY = at.Y;
            else if (at.Bottom > scrollY + _height)
                scrollY = at.Bottom - _height;
        }

        // A line's width leaves out its trailing spaces, which the caret can sit past.
        var widest = MathF.Max(alignX + size.Width, left);
        scrollX = wraps ? 0f : Math.Clamp(scrollX, 0f, MathF.Max(0f, widest + caret - _width));
        scrollY = Math.Clamp(scrollY, 0f, MathF.Max(0f, size.Height - _height));
        _placeholderAlignX = Aligned(_placeholderLayout.Size.Width, caret, wraps);
        if (scrollX == _scrollX && scrollY == _scrollY && alignX == _alignX)
            return;
        _scrollX = scrollX;
        _scrollY = scrollY;
        _alignX = alignX;
        view.MarkPaint(node);
        view.MarkCaret(node);
    }

    float Aligned(float textWidth, float caret, bool wraps)
    {
        var room = _width - caret - textWidth;
        return wraps || room <= 0f ? 0f
            : _align == TextAlign.Center ? room * 0.5f
            : _align == TextAlign.Right ? room
            : 0f;
    }

    internal void Record(DrawingContext dc, UiView view, Size size)
    {
        _paintedStart = _paintedEnd = -1;
        if (!LaidOut(view))
            return;
        dc.PushClip(new Rect(size));
        var origin = Origin;
        if (SelectionShown)
        {
            var start = Editor.SelectionStart;
            var end = Editor.SelectionEnd;
            ValueList.EnsureCapacity(ref _rects, _layout.Lines.Length);
            var count = _layout.GetSelectionRects(ToDisplay(start), ToDisplay(end), _rects);
            for (var i = 0; i < count; i++)
            {
                var rect = _rects[i];
                dc.DrawRectangle(
                    _selectionFill,
                    new Rect(origin.X + rect.X, origin.Y + rect.Y, rect.Width, rect.Height)
                );
            }
            _paintedStart = start;
            _paintedEnd = end;
        }
        var atlas = view.Context.Text.Atlas;
        if (Editor.Length > 0)
            _layout.Draw(dc, atlas, origin);
        else if (!_placeholder.Text.IsEmpty)
            _placeholderLayout.Draw(dc, atlas, new Vector2(_placeholderAlignX, 0f));
        dc.Pop();
    }

    internal void RecordCaret(DrawingContext dc, UiView view, Size size)
    {
        CaretDirty = false;
        CaretRecordedSize = size;
        if (!CaretShown || !LaidOut(view))
            return;
        var rect = CaretRect(view.Scale);
        dc.PushClip(new Rect(size));
        if (_caretOn)
            dc.DrawRectangle(_caretFill, rect);
        else
            dc.HoldRectangle(rect);
        dc.Pop();
    }

    Rect CaretRect(float scale)
    {
        var at = _layout.GetCaretRect(ToDisplay(Editor.Caret), _upstream);
        var origin = Origin;
        var x = MathF.Round((origin.X + at.X) * scale) / scale;
        return new Rect(x, origin.Y + at.Y, CaretWidth(scale), at.Height);
    }

    internal Rect CaretArea(UiView view, Node node)
    {
        var scale = view.Scale;
        var local = LaidOut(view) ? CaretRect(scale) : new Rect(0f, 0f, CaretWidth(scale), _height);
        var area = local.TransformedBy(node.SnappedToView);
        return new Rect(area.X * scale, area.Y * scale, area.Width * scale, area.Height * scale);
    }

    enum DragUnit : byte
    {
        Cluster,
        Word,
        All,
    }
}
