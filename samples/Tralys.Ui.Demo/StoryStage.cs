using Tralys.Ui.Gallery;
using Tralys.Ui.Gallery.Textures;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Demo;

/// <summary>A context and a view showing one story at a time, 1272×808 at scale 1, in the gallery's fonts, icons and
/// images, in the dark theme at pointer density until its context says otherwise, recorded each frame into a draw list
/// no renderer submits, or drawn by one: what the story capture, the goldens and the sweep run. Its frames step a
/// sixtieth of a second, so it scrolls without gliding.</summary>
sealed class StoryStage : IDisposable
{
    public const int Width = 1272;
    public const int Height = 808;

    const int MostFrames = 600;

    readonly GalleryFonts _fonts;
    readonly Action<UiView>? _draw;
    readonly GradientRampAtlas? _ramps;
    readonly DrawList? _list;
    TimeSpan _now;
    Type? _subject;
    InputStates _held;

    /// <summary>A stage that lays out and records without a GPU, its images known only by their size.</summary>
    public StoryStage()
        : this(new MemoryTextures()) { }

    StoryStage(MemoryTextures textures)
        : this(textures, textures, null) { }

    /// <summary>A stage whose glyph pages <paramref name="glyphs"/> makes, whose image keys
    /// <paramref name="images"/> resolves, and whose every frame, its view updated, ends in <paramref name="draw"/>,
    /// which draws it; null records it into a list no renderer submits.</summary>
    public StoryStage(ITextureFactory glyphs, ITextureProvider images, Action<UiView>? draw)
    {
        _fonts = new GalleryFonts(glyphs);
        Context = new UiContext(_fonts.Services, images)
        {
            SmoothScrolling = false,
            Icons = Icons.Set,
        };
        View = new UiView(Context, new StageHost()) { Size = new Size(Width, Height) };
        _draw = draw;
        if (draw is null)
        {
            _ramps = new GradientRampAtlas();
            _list = new DrawList(_ramps);
        }
        Context.Advance(_now);
    }

    public UiContext Context { get; }

    public UiView View { get; }

    /// <summary>The state every frame holds the shown story's components in, from the next frame on.</summary>
    public StoryState State { get; set; }

    /// <summary>Mounts <paramref name="story"/> as the view's root, in place of the one shown.</summary>
    public void Show(Story story)
    {
        _subject = story.Component;
        View.Mount(StoryFrame.Of(story));
    }

    /// <summary>Moves the clock on by <paramref name="seconds"/>, holds the story's components in
    /// <see cref="State"/>, updates the view and records or draws it.</summary>
    [FramePath]
    public void Frame(double seconds = 1.0 / 60.0)
    {
        _now += TimeSpan.FromSeconds(seconds);
        Context.Advance(_now);
        var held = State.Forced();
        if (_subject is { } subject && (held | _held) != InputStates.None)
            Hold(View.Root, subject, held);
        _held = held;
        View.Update();
        if (_draw is { } draw)
        {
            draw(View);
            return;
        }
        _ramps!.BeginFrame();
        _list!.ClearPatchedRanges();
        View.Render(_list);
    }

    /// <summary>Shows <paramref name="story"/> and runs frames until two in a row leave the view idle.</summary>
    /// <exception cref="InvalidOperationException">The story was still busy after 600 frames.</exception>
    public void Settle(Story story)
    {
        Show(story);
        var idle = 0;
        for (var i = 0; i < MostFrames; i++)
        {
            Frame();
            idle = View.IsIdle ? idle + 1 : 0;
            if (idle == 2)
                return;
        }
        throw new InvalidOperationException(
            $"{story.Key} was still busy after {MostFrames} frames."
        );
    }

    /// <summary>The shown story's layout.</summary>
    public LayoutDump Layout() => LayoutDump.Of(View);

    /// <summary>What the view draws as it stands.</summary>
    public DrawCommandStream Draw() => DrawCommandStream.Of(View);

    // Parts mount after the story shows, so each frame holds every component of the story's type again.
    [FramePath]
    void Hold(Node node, Type subject, InputStates states)
    {
        foreach (var child in node.Children)
        {
            if (child.Component == subject)
                View.ForceStates(child, states);
            Hold(child, subject, states);
        }
    }

    public void Dispose()
    {
        View.Dispose();
        Context.Dispose();
        _fonts.Dispose();
    }

    sealed class StageHost : IViewHost
    {
        string? _clipboard;

        public void SetCursor(Cursor? cursor) { }

        public string? GetClipboardText() => _clipboard;

        public void SetClipboardText(ReadOnlySpan<char> text) => _clipboard = text.ToString();

        public void StartTextInput(TextInputKind kind, Rect area, bool byContact) { }

        public void StopTextInput() { }
    }
}
