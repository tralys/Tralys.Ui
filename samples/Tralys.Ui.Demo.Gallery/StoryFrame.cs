using Tralys.Ui.Docking;

namespace Tralys.Ui.Gallery;

/// <summary>How a gallery hosts a story: at the view's top left at its own size, the root of a column that starts
/// there, so a story whose root grows or stretches itself fills the view.</summary>
public static class StoryFrame
{
    static readonly Action<Ui, Action<Ui>> Frame = static (ui, render) =>
    {
        using (ui.Box(direction: Direction.Column, align: Align.Start))
            render(ui);
    };

    static readonly Action<Ui, Windowed> FrameWithWindows = static (ui, story) =>
    {
        ui.Provide(StoryWindows.Key, new StoryWindows(story.Windows));
        Frame(ui, story.Render);
    };

    static readonly Action<Ui, Held> FrameHeld = static (ui, held) =>
    {
        using (
            ui.Box(
                direction: Direction.Column,
                align: Align.Start,
                forceStates: held.States,
                forceStatesOn: held.Component
            )
        )
            held.Render(ui);
    };

    /// <summary>The content a view mounts to show <paramref name="story"/> with its components held in
    /// <paramref name="state"/>.</summary>
    public static Slot Of(Story story, StoryState state) =>
        Slot.Of(new Held(story.Render, story.Component, state.Forced()), FrameHeld);

    /// <summary>The content a view mounts to show <paramref name="story"/>, whose docks tear panels out into
    /// <paramref name="windows"/>' windows, or into floats of their own without them.</summary>
    public static Slot Of(Story story, IDockWindowHost? windows = null) =>
        windows is null
            ? Slot.Of(story.Render, Frame)
            : Slot.Of(new Windowed(story.Render, windows), FrameWithWindows);

    sealed record Windowed(Action<Ui> Render, IDockWindowHost Windows);

    sealed record Held(Action<Ui> Render, Type Component, InputStates States);
}

/// <summary>The windows a story's docks tear panels out into, which the app showing the stories provides.</summary>
public readonly record struct StoryWindows(IDockWindowHost? Host)
{
    public static readonly ContextKey<StoryWindows> Key = new("StoryWindows", default);
}
