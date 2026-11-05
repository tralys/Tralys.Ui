using System.Text;

namespace Tralys.Ui.Testing;

/// <summary>What a view draws: each push and primitive as a line in local space, after the path of the node drawing
/// it. Two streams are equal when every line is.</summary>
public sealed class DrawCommandStream : IEquatable<DrawCommandStream>
{
    readonly string[] _lines;

    internal DrawCommandStream(string[] lines) => _lines = lines;

    /// <summary>What <paramref name="view"/> draws as it stands.</summary>
    public static DrawCommandStream Of(UiView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var lines = new List<string>();
        view.Trace((drawer, line) => lines.Add($"{NodePath.Of(drawer as Node)}: {line}"));
        return new DrawCommandStream([.. lines]);
    }

    public IReadOnlyList<string> Lines => _lines;

    public bool Equals(DrawCommandStream? other) =>
        other is not null && _lines.AsSpan().SequenceEqual(other._lines);

    public override bool Equals(object? obj) => obj is DrawCommandStream other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var line in _lines)
            hash.Add(line, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    public static bool operator ==(DrawCommandStream? a, DrawCommandStream? b) =>
        a is null ? b is null : a.Equals(b);

    public static bool operator !=(DrawCommandStream? a, DrawCommandStream? b) => !(a == b);

    /// <summary>One line each, ending in a line break.</summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        foreach (var line in _lines)
            text.Append(line).Append('\n');
        return text.ToString();
    }
}
