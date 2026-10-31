using System.Diagnostics.CodeAnalysis;

namespace Tralys.Ui.Rendering;

public sealed partial class DrawList
{
    ValueList<SpanRecord> _spans = new(4);
    ValueList<BatchRequest> _requests = new(16);
    ValueList<VertexRange> _patched = new(4);
    int _openSpan = -1;
    int _patchSpan = -1;
    int _patchRequest;
    bool _patchFailed;
    Cursors _frame;

    /// <summary>The vertices rewritten in place since the last <see cref="Begin"/> or <see cref="ClearPatchedRanges"/>,
    /// for a renderer holding this generation's geometry to upload alone; the indices and batches are unchanged.</summary>
    [Experimental("TRL5001")]
    public ReadOnlySpan<VertexRange> PatchedRanges => _patched.AsSpan();

    /// <summary>Forgets <see cref="PatchedRanges"/>, once the renderer has uploaded them.</summary>
    [Experimental("TRL5001")]
    public void ClearPatchedRanges() => _patched.Clear();

    /// <summary>Whether what is drawn redraws a span over itself: then nothing may change the frame beyond the span's
    /// own vertices, and whatever would fails the patch.</summary>
    bool Replaying => _patchSpan >= 0;

    void ForgetSpans()
    {
        _spans.Clear();
        _requests.Clear();
        _patched.Clear();
        _openSpan = -1;
        _patchSpan = -1;
    }

    void NoteRequest(in BatchRequest request, int first)
    {
        if (_openSpan >= 0)
            _requests.Add(request with { First = first });
    }

    /// <summary>Notes the state and where what is drawn next lands, until <see cref="EndSpan"/>, so a patch between
    /// <see cref="BeginPatch"/> and <see cref="EndPatch"/> can draw it again from that state into the same place.</summary>
    internal int BeginSpan()
    {
        _openSpan = _spans.Count;
        _spans.Add(
            new SpanRecord
            {
                State = _state,
                FirstVertex = _vertexCount,
                FirstIndex = _indexCount,
                FirstRequest = _requests.Count,
                Layers = _layerCount,
                Ramps = _rampCount,
            }
        );
        return _openSpan;
    }

    internal void EndSpan(int span)
    {
        ref var open = ref _spans[span];
        open.EndVertex = _vertexCount;
        open.EndIndex = _indexCount;
        open.EndRequest = _requests.Count;
        open.Patchable = _layerCount == open.Layers && _rampCount == open.Ramps;
        _openSpan = -1;
    }

    /// <summary>Starts drawing over <paramref name="span"/> of this generation from the state it began in; false when
    /// the span changed the frame beyond its own vertices, or its layer folded it, so only a rebuild redraws it.</summary>
    internal bool BeginPatch(int span)
    {
        ref readonly var target = ref _spans[span];
        if (
            !target.Patchable
            || (target.State.Layer >= 0 && _layerRecords[target.State.Layer].Folded)
        )
            return false;

        _frame = new Cursors(
            _vertexCount,
            _indexCount,
            _quadCount,
            _layerCount,
            _rampCount,
            _stack.Count,
            _state
        );
        _vertexCount = target.FirstVertex;
        _indexCount = target.FirstIndex;
        _state = target.State;
        _patchSpan = span;
        _patchRequest = target.FirstRequest;
        _patchFailed = false;
        return true;
    }

    internal bool BeginPatchAtOpacity(int span, float opacity)
    {
        if (!BeginPatch(span))
            return false;
        _state.Opacity = opacity;
        _spans[span].State.Opacity = opacity;
        return true;
    }

    /// <summary>Ends <see cref="BeginPatch"/>, and adds the span's vertices to <see cref="PatchedRanges"/> when what was
    /// drawn landed as the same requests to the same batches, inside the bounds its layer took, with no layer or
    /// gradient row of its own. When it returns false the span may be partly overwritten, and the list must be
    /// rebuilt.</summary>
    internal bool EndPatch()
    {
        ref readonly var target = ref _spans[_patchSpan];
        var frame = _frame;
        var landed =
            !_patchFailed
            && _vertexCount == target.EndVertex
            && _indexCount == target.EndIndex
            && _patchRequest == target.EndRequest
            && _layerCount == frame.Layers
            && _rampCount == frame.Ramps
            && _stack.Count == frame.Depth;
        if (landed && target.EndVertex > target.FirstVertex)
            _patched.Add(
                new VertexRange(target.FirstVertex, target.EndVertex - target.FirstVertex)
            );

        _patchSpan = -1;
        _vertexCount = frame.Vertices;
        _indexCount = frame.Indices;
        _quadCount = frame.Quads;
        _layerCount = frame.Layers;
        _rampCount = frame.Ramps;
        _stack.Truncate(frame.Depth);
        _state = frame.State;
        return landed;
    }

    // The span is left partly overwritten, so the list must be rebuilt.
    internal void AbortPatch()
    {
        if (_patchSpan < 0)
            return;

        _patchFailed = true;
        EndPatch();
    }

    int ReplayBatch(in BatchRequest request)
    {
        if (_patchRequest >= _spans[_patchSpan].EndRequest)
        {
            _patchFailed = true;
            return 0;
        }

        ref readonly var built = ref _requests[_patchRequest++];
        if (!request.Matches(built))
            _patchFailed = true;
        return built.First;
    }

    // The layer's composite already landed on the bounds it took, so what it draws must stay inside them.
    void ReplayTrack(in LayerRecord layer, Rect device)
    {
        if (layer.Primitives == 0 || !Holds(layer.Content, device))
            _patchFailed = true;
    }

    // A patch uploads vertices only, so one that rewires them must fail.
    void ReplayIndex(ushort value)
    {
        if (_indices[_indexCount] != value)
            _patchFailed = true;
    }

    void ReplayRamp(int row)
    {
        if (Array.IndexOf(_rampRows, row, 0, _rampCount) < 0)
            _patchFailed = true;
    }

    struct SpanRecord
    {
        public State State;
        public int FirstVertex;
        public int EndVertex;
        public int FirstIndex;
        public int EndIndex;
        public int FirstRequest;
        public int EndRequest;
        public int Layers;
        public int Ramps;
        public bool Patchable;
    }

    readonly record struct Cursors(
        int Vertices,
        int Indices,
        int Quads,
        int Layers,
        int Ramps,
        int Depth,
        State State
    );
}
