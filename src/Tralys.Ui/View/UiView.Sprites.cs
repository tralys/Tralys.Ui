namespace Tralys.Ui;

public sealed partial class UiView
{
    ValueList<ImagePayload> _sprites;
    ValueList<ImagePayload> _restingSprites;
    bool _spritesHeld;

    internal bool SpritesRunning => _sprites.Count > 0 && !(_spritesHeld && Context.ReducedMotion);

    [FramePath]
    internal void PlaySprite(ImagePayload sprite)
    {
        if (sprite.SpriteSlot >= 0 || sprite.Resting)
            return;
        sprite.SpriteSlot = _sprites.Count;
        _sprites.Add(sprite);
        _spritesHeld = false;
    }

    [FramePath]
    internal void StopSprite(ImagePayload sprite)
    {
        if (sprite.Resting)
        {
            var index = _restingSprites.IndexOf(sprite);
            if (index >= 0)
                _restingSprites.RemoveAtSwap(index);
            sprite.Resting = false;
        }
        var slot = sprite.SpriteSlot;
        if (slot < 0)
            return;
        var last = _sprites.Count - 1;
        var moved = _sprites[last];
        _sprites[slot] = moved;
        moved.SpriteSlot = slot;
        _sprites.Truncate(last);
        sprite.SpriteSlot = -1;
    }

    // Backwards, so a sprite leaving swaps in one already turned.
    [FramePath]
    internal void AdvanceSprites()
    {
        var time = Context.AnimationTime;
        var reduced = Context.ReducedMotion;
        for (var i = _sprites.Count - 1; i >= 0; i--)
        {
            var sprite = _sprites[i];
            if (!Shown(sprite.Node))
            {
                StopSprite(sprite);
                sprite.Resting = true;
                _restingSprites.Add(sprite);
            }
            else if (sprite.Turn(time, reduced) && sprite.Node is { } node)
                MarkPaint(node);
        }
        _spritesHeld = reduced;
    }

    [FramePath]
    void WakeSprites()
    {
        for (var i = _restingSprites.Count - 1; i >= 0; i--)
        {
            var sprite = _restingSprites[i];
            if (!Shown(sprite.Node))
                continue;
            _restingSprites.RemoveAtSwap(i);
            sprite.Resting = false;
            PlaySprite(sprite);
        }
    }
}
