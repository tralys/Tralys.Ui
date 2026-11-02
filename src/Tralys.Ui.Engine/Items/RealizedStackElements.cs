using System.Runtime.InteropServices;

namespace Tralys.Ui;

interface IElementRecycler<in TElement>
{
    [FramePath]
    void Recycle(TElement element);

    [FramePath]
    void RecycleElement(TElement element, int index);
}

sealed class RealizedStackElements<TElement>
    where TElement : class
{
    readonly List<TElement?> _elements = [];
    readonly List<float> _sizes = [];
    int _firstIndex;
    float _startU;

    public int Count => _elements.Count;

    public int FirstIndex => _elements.Count > 0 ? _firstIndex : -1;

    public int LastIndex => _elements.Count > 0 ? _firstIndex + _elements.Count - 1 : -1;

    public List<TElement?> Elements => _elements;

    public List<float> SizeU => _sizes;

    public float StartU => _startU;

    [FramePath]
    public void Add(int index, TElement element, float u, float sizeU)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (Count == 0)
        {
            _elements.Add(element);
            _sizes.Add(sizeU);
            _startU = u;
            _firstIndex = index;
        }
        else if (index == LastIndex + 1)
        {
            _elements.Add(element);
            _sizes.Add(sizeU);
        }
        else if (index == FirstIndex - 1)
        {
            --_firstIndex;
            _elements.Insert(0, element);
            _sizes.Insert(0, sizeU);
            _startU = u;
        }
        else
            throw new NotSupportedException(
                "Elements are realized only at the start or the end of the run."
            );
    }

    [FramePath]
    public TElement? GetElement(int index)
    {
        var i = index - FirstIndex;
        return i >= 0 && i < _elements.Count ? _elements[i] : null;
    }

    [FramePath]
    public float GetElementU(int index)
    {
        if (index < FirstIndex)
            return float.NaN;

        var endIndex = index - FirstIndex;
        if (endIndex >= _sizes.Count || _elements[endIndex] is null)
            return float.NaN;

        var u = _startU;
        for (var i = 0; i < endIndex; ++i)
        {
            if (_elements[i] is null)
                return float.NaN;
            u += _sizes[i];
        }
        return u;
    }

    [FramePath]
    public int IndexPast(float u, out float elementU)
    {
        elementU = _startU;
        for (var i = 0; i < _sizes.Count; ++i)
        {
            var endU = elementU + _sizes[i];
            if (endU > u)
                return _firstIndex + i;
            elementU = endU;
        }
        return _firstIndex + _sizes.Count;
    }

    [FramePath]
    public void MoveStart(float by) => _startU += by;

    [FramePath]
    public int GetIndex(TElement element)
    {
        var index = _elements.IndexOf(element);
        return index >= 0 ? index + FirstIndex : -1;
    }

    [FramePath]
    public float ItemsInserted(int index, int count, float estimatedSizeU)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (_elements.Count == 0)
            return 0f;

        var realizedIndex = index - FirstIndex;
        if (realizedIndex >= Count)
            return 0f;

        if (realizedIndex < 0)
        {
            _firstIndex += count;
            var moved = count * estimatedSizeU;
            _startU += moved;
            return moved;
        }

        Open(_elements, realizedIndex, count);
        Open(_sizes, realizedIndex, count);
        return 0f;
    }

    [FramePath]
    static void Open<T>(List<T> list, int at, int count)
    {
        var size = list.Count;
        CollectionsMarshal.SetCount(list, size + count);
        var items = CollectionsMarshal.AsSpan(list);
        items[at..size].CopyTo(items[(at + count)..]);
        items.Slice(at, count).Clear();
    }

    [FramePath]
    public float ItemsRemoved(
        int index,
        int count,
        IElementRecycler<TElement> panel,
        bool moving,
        float estimatedSizeU,
        float viewU
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (_elements.Count == 0)
            return 0f;

        var first = FirstIndex;
        var startIndex = index - first;
        var endIndex = index + count - first;
        var moved = 0f;

        if (endIndex > 0 && startIndex < _elements.Count)
        {
            var start = Math.Max(startIndex, 0);
            var end = Math.Min(endIndex, _elements.Count);
            var u = _startU;
            for (var i = 0; i < end; ++i)
            {
                var size = _sizes[i];
                u += size;
                if (i >= start && u <= viewU)
                    moved -= size;
            }

            if (moving)
                RecycleRange(start, end, panel);
            else
            {
                for (var i = start; i < end; ++i)
                {
                    if (_elements[i] is { } element)
                    {
                        _elements[i] = null;
                        panel.Recycle(element);
                    }
                }
            }

            _elements.RemoveRange(start, end - start);
            _sizes.RemoveRange(start, end - start);
        }

        if (startIndex < 0)
        {
            var before = Math.Min(-startIndex, count);
            _firstIndex -= before;
            var startU = _firstIndex == 0 ? 0f : MathF.Max(0f, _startU - before * estimatedSizeU);
            moved += startU - _startU;
            _startU = startU;
        }
        return moved;
    }

    [FramePath]
    public void ItemsReplaced(int index, int count, IElementRecycler<TElement> panel)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (_elements.Count == 0)
            return;

        var startIndex = index - FirstIndex;
        var endIndex = Math.Min(startIndex + count, Count);
        startIndex = Math.Max(startIndex, 0);
        if (endIndex <= startIndex)
            return;

        for (var i = startIndex; i < endIndex; ++i)
        {
            if (_elements[i] is { } element)
            {
                _elements[i] = null;
                panel.RecycleElement(element, i + _firstIndex);
            }
        }
    }

    [FramePath]
    public void RecycleElementsBefore(int index, IElementRecycler<TElement> panel)
    {
        if (index <= FirstIndex || _elements.Count == 0)
            return;

        if (index > LastIndex)
        {
            RecycleAllElements(panel);
            return;
        }

        var endIndex = index - FirstIndex;
        RecycleRange(0, endIndex, panel);
        for (var i = 0; i < endIndex; ++i)
            _startU += _sizes[i];
        _elements.RemoveRange(0, endIndex);
        _sizes.RemoveRange(0, endIndex);
        _firstIndex = index;
    }

    [FramePath]
    public void RecycleElementsAfter(int index, IElementRecycler<TElement> panel)
    {
        if (index >= LastIndex || _elements.Count == 0)
            return;

        if (index < FirstIndex)
        {
            RecycleAllElements(panel);
            return;
        }

        var startIndex = index + 1 - FirstIndex;
        RecycleRange(startIndex, _elements.Count, panel);
        _elements.RemoveRange(startIndex, _elements.Count - startIndex);
        _sizes.RemoveRange(startIndex, _sizes.Count - startIndex);
    }

    [FramePath]
    public void RecycleAllElements(IElementRecycler<TElement> panel)
    {
        RecycleRange(0, _elements.Count, panel);
        _startU = _firstIndex = 0;
        _elements.Clear();
        _sizes.Clear();
    }

    [FramePath]
    void RecycleRange(int start, int end, IElementRecycler<TElement> panel)
    {
        for (var i = start; i < end; ++i)
        {
            if (_elements[i] is { } element)
            {
                _elements[i] = null;
                panel.RecycleElement(element, i + _firstIndex);
            }
        }
    }

    // Without recycling: its elements now belong to another run.
    [FramePath]
    public void ResetForReuse()
    {
        _startU = _firstIndex = 0;
        _elements.Clear();
        _sizes.Clear();
    }
}
