using System.Text;

namespace Tralys.Ui.CodeGen;

sealed class CodeWriter
{
    const string Step = "    ";

    readonly StringBuilder _text = new();
    int _depth;

    public void Line() => _text.Append('\n');

    public void Line(string text)
    {
        if (text.IndexOf('\n') < 0)
        {
            AppendIndented(text);
            return;
        }

        foreach (var slice in text.Split('\n'))
            AppendIndented(slice.TrimEnd('\r'));
    }

    public Scope Block(string header)
    {
        Line(header);
        Line("{");
        _depth++;
        return new Scope(this, closes: true);
    }

    public Scope Indented()
    {
        _depth++;
        return new Scope(this, closes: false);
    }

    public override string ToString()
    {
        if (_depth != 0)
            throw new InvalidOperationException($"{_depth} scope(s) are still open.");
        return _text.ToString();
    }

    void AppendIndented(string slice)
    {
        if (slice.Length > 0)
        {
            for (var i = 0; i < _depth; i++)
                _text.Append(Step);
            _text.Append(slice);
        }
        _text.Append('\n');
    }

    void Close(bool closes)
    {
        _depth--;
        if (closes)
            Line("}");
    }

    public readonly struct Scope : IDisposable
    {
        readonly CodeWriter _writer;
        readonly bool _closes;

        internal Scope(CodeWriter writer, bool closes)
        {
            _writer = writer;
            _closes = closes;
        }

        public void Dispose() => _writer?.Close(_closes);
    }
}
