using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

public sealed partial class UiView
{
    ValueList<EditorPayload> _reconciling;
    ValueList<EditorPayload> _leftEditors;
    EditorPayload? _typing;
    TextInputKind _typingKind;
    Rect _typingArea;
    bool _textBlocked;

    /// <summary>Text the host committed, typed or from its IME; it goes where keys go. Empty text, and text following a
    /// key-down a handler prevented, are ignored.</summary>
    /// <returns>Whether the UI took it.</returns>
    public bool TextInput(ReadOnlySpan<char> text)
    {
        BeforeInput();
        if (text.IsEmpty || _textBlocked)
            return false;
        var target = KeyReceiver();
        return target is { Payload: EditorPayload editor }
            && (target._states & InputStates.Disabled) == 0
            && editor.TypeText(this, text);
    }

    internal bool CaretBlinks =>
        !_windowInactive
        && _focused is { Payload: EditorPayload { CaretShown: true } } focused
        && Drawn(focused);

    internal void Raise<T>(Callback<T> callback, T value) => Invoke(callback, value);

    internal void Raise(Callback callback) => Invoke(callback);

    internal void Write(ObservableText text, ReadOnlySpan<char> value)
    {
        try
        {
            text.Set(value);
        }
        catch (Exception fault) when (Context.OnFault is not null)
        {
            Context.OnFault(fault);
        }
    }

    internal string? ClipboardText() => _host.GetClipboardText();

    internal void SetClipboard(ReadOnlySpan<char> text) => _host.SetClipboardText(text);

    internal void MarkCaret(Node node)
    {
        if (node.Payload is not EditorPayload editor)
            return;
        editor.CaretDirty = true;
        if (node.PaintQueued || !node.IsMounted)
            return;
        node.PaintQueued = true;
        _paintDirty.Add(node);
    }

    internal void QueueReconcile(EditorPayload editor)
    {
        if (editor.Reconciling)
            return;
        editor.Reconciling = true;
        _reconciling.Add(editor);
    }

    void ReconcileEditors()
    {
        for (var i = 0; i < _reconciling.Count; i++)
            _reconciling[i].Reconcile(this);
        _reconciling.Clear();
    }

    internal void QueueLeft(EditorPayload editor)
    {
        if (editor.Leaving)
            return;
        editor.Leaving = true;
        _leftEditors.Add(editor);
    }

    void EditorDetached(EditorPayload editor)
    {
        editor.Detached(this);
        Unlist(ref _reconciling, editor);
        editor.Reconciling = false;
    }

    static void Unlist(ref ValueList<EditorPayload> list, EditorPayload editor)
    {
        var index = list.IndexOf(editor);
        if (index >= 0)
            list.RemoveAt(index);
    }

    void DeliverEditorNotices()
    {
        for (var i = 0; i < _leftEditors.Count; i++)
        {
            var editor = _leftEditors[i];
            editor.Leaving = false;
            editor.Commit(this);
        }
        _leftEditors.Clear();
    }

    void UpdateEditors()
    {
        var focused =
            !_windowInactive && _focused is { Payload: EditorPayload editor } node && Drawn(node)
                ? editor
                : null;
        focused?.Blink(this, Context.Now);
        if (
            focused is not { Editable: true, Node: { } at } typing
            || typing.InputKind == TextInputKind.None
        )
        {
            if (_typing is { } stopped)
                StopTyping(stopped);
            return;
        }

        var area = typing.CaretArea(this, at);
        var kind = typing.InputKind;
        if (ReferenceEquals(_typing, typing) && kind == _typingKind)
        {
            if (area == _typingArea)
                return;
            _typingArea = area;
            _host.MoveTextInput(area);
            return;
        }
        _typing = typing;
        _typingKind = kind;
        _typingArea = area;
        _host.StartTextInput(kind, area, typing.ByContact);
    }

    internal void StopTyping(EditorPayload editor)
    {
        if (!ReferenceEquals(_typing, editor))
            return;
        _typing = null;
        _host.StopTextInput();
    }

    internal void TouchedWhileTyping(EditorPayload editor)
    {
        editor.ByContact = true;
        if (ReferenceEquals(_typing, editor))
            _host.StartTextInput(_typingKind, _typingArea, byContact: true);
    }

    internal void ReleaseCapture(Node node, Pointer pointer)
    {
        if (FindState(pointer) is { } state && state.Captor == node)
            SetCaptor(state, null);
    }

    void DrawCaret(EditorPayload editor, DrawList list, Size size)
    {
        var commands = RecordCaret(editor, size);
        var span = list.BeginSpan();
        list.Draw(commands);
        list.EndSpan(span);
        if (!list.RecordsSpans)
            return;
        editor.CaretSpan = span;
        editor.CaretSpanList = list.Generation;
    }

    bool PatchCaret(Node node, EditorPayload editor, DrawList list)
    {
        if (!editor.CaretDirty)
            return true;
        if (editor.CaretSpan < 0 || editor.CaretSpanList != list.Generation)
            return false;
        var commands = RecordCaret(editor, FrameSize(node));
        if (!list.BeginPatch(editor.CaretSpan))
            return false;
        list.Draw(commands);
        return list.EndPatch();
    }

    RenderCommandBuffer RecordCaret(EditorPayload editor, Size size)
    {
        var buffer = editor.CaretCommands ??= new RenderCommandBuffer();
        var dc = buffer.Open();
        var recorded = false;
        try
        {
            editor.RecordCaret(dc, this, size);
            recorded = true;
        }
        finally
        {
            if (!recorded)
            {
                buffer.Clear();
                _compositeDirty = true;
            }
        }
        return buffer;
    }
}
