using System.Numerics;
using Tralys.Ui.Gallery;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Demo.Tests;

public sealed class StorySweepAllocationTests
{
    // Capacities can take dozens of cycles to settle; a cost every cycle pays still shows in every window.
    const int Windows = 64;

    public enum Input
    {
        Idle,
        Rerender,
        Hover,
        Click,
        RightClick,
        DoubleClick,
        Keys,
        Text,
        Wheel,
        Touch,
        Pen,
        TouchDensity,
        Hold,
        Gamepad,
        Resize,
        Themes,
        Densities,
        States,
    }

    public static IEnumerable<(string Story, Input Input)> Cases()
    {
        foreach (var story in StoryRegistry.All)
        {
            foreach (var input in Enum.GetValues<Input>())
                yield return (story.Key, input);
        }
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task The_story_under_the_input_every_frame_allocates_nothing_once_warm(
        string key,
        Input input
    )
    {
        var story = await Assert.That(DemoOptions.Find(key)).IsNotNull();
        using var stage = new StoryStage();
        if (input == Input.TouchDensity)
            stage.Context.Density = Density.Touch;
        stage.Settle(story);
        var sweep = new Sweep(stage, story, input);

        var worst = AllocationGate.WorstFrameBytes(
            sweep.Frame,
            frames: Math.Max(120, sweep.Cycle),
            windows: Windows,
            warmup: Math.Max(4, 2 * sweep.Cycle)
        );

        await Assert.That(worst).IsEqualTo(0L);
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task The_story_under_the_input_every_frame_draws_what_a_rebuild_draws(
        string key,
        Input input
    )
    {
        var story = await Assert.That(DemoOptions.Find(key)).IsNotNull();
        using var stage = new StoryStage();
        if (input == Input.TouchDensity)
            stage.Context.Density = Density.Touch;
        stage.Settle(story);
        stage.View.VerifiesFrames = true;
        var sweep = new Sweep(stage, story, input);

        for (var frame = 0; frame < Math.Max(60, 2 * sweep.Cycle); frame++)
            sweep.Frame();

        await Assert.That(stage.View.FrameDifference).IsNull();
    }

    [Test]
    public async Task Walking_every_story_on_one_stage_a_second_time_takes_all_it_rents_from_the_pools()
    {
        var stories = StoryRegistry.All;
        var sweeps = new Sweep[stories.Count][];
        var misses = new long[stories.Count];
        using var stage = new StoryStage();
        Walk(stage, sweeps, misses);

        Walk(stage, sweeps, misses);

        var missing = new List<(string Story, long Misses)>();
        for (var i = 0; i < stories.Count; i++)
        {
            if (misses[i] != 0)
                missing.Add((stories[i].Key, misses[i]));
        }
        await Assert.That(missing).IsEmpty();
    }

    static readonly Input[] Walked = [Input.Hover, Input.Click, Input.Text];

    const int WalkFrames = 12;

    // The first walk makes each story's sweeps from its settled layout; the second replays them.
    static void Walk(StoryStage stage, Sweep[][] sweeps, long[] misses)
    {
        var stories = StoryRegistry.All;
        for (var i = 0; i < stories.Count; i++)
        {
            var before = Misses;
            var story = stories[i];
            stage.Settle(story);
            if (sweeps[i] is null)
            {
                sweeps[i] = new Sweep[Walked.Length];
                for (var w = 0; w < Walked.Length; w++)
                    sweeps[i][w] = new Sweep(stage, story, Walked[w]);
            }
            foreach (var sweep in sweeps[i])
            {
                sweep.Restart();
                for (var frame = 0; frame < WalkFrames; frame++)
                    sweep.Frame();
            }
            misses[i] = Misses - before;
        }
    }

    static long Misses => PoolMisses.Count + Mvvm.PoolMisses.Count;

    sealed class Sweep
    {
        const int Columns = 6;
        const int Rows = 4;
        const int MaxPoints = 64;
        const int MaxStops = 64;
        const int StrokeSteps = 6;
        const int TypingSteps = 6;
        const int HoldSteps = 3;
        const double FrameSeconds = 1.0 / 60.0;

        static readonly Pointer Finger = new(PointerKind.Touch, 1);
        static readonly Pointer Stylus = new(PointerKind.Pen, 2);
        static readonly Key[] Keys =
        [
            Key.Tab,
            Key.Space,
            Key.Down,
            Key.Right,
            Key.Enter,
            Key.BackSpace,
            Key.Up,
            Key.Left,
            Key.Escape,
        ];
        static readonly GamepadButton[] Buttons =
        [
            GamepadButton.DPadDown,
            GamepadButton.DPadRight,
            GamepadButton.South,
            GamepadButton.DPadUp,
            GamepadButton.DPadLeft,
            GamepadButton.East,
        ];
        static readonly (float Width, float Height, float Scale)[] Viewports =
        [
            (StoryStage.Width, StoryStage.Height, 1f),
            (960, StoryStage.Height, 1f),
            (480, StoryStage.Height, 1f),
            (StoryStage.Width, StoryStage.Height, 1.5f),
        ];

        readonly StoryStage _stage;
        readonly Input _input;
        readonly Vector2[] _points;
        readonly int _stops;
        int _tick;

        public Sweep(StoryStage stage, Story story, Input input)
        {
            _stage = stage;
            _input = input;
            var view = stage.View;
            var shown = default(Rect);
            Shown(FrameBox(view.Root), ref shown);
            shown = shown.Intersect(new Rect(view.Size));
            var points = new List<Vector2>(MaxPoints);
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                    points.Add(
                        new Vector2(
                            shown.X + shown.Width * (column + 0.5f) / Columns,
                            shown.Y + shown.Height * (row + 0.5f) / Rows
                        )
                    );
            }
            Centres(view, view.Root, story.Component, points);
            Centres(view, view.Root, null, points);
            _points = [.. points];

            var stops = _stops = Math.Clamp(Stops(view.Root), 1, MaxStops);
            Cycle = input switch
            {
                Input.Idle or Input.Rerender => 1,
                Input.Hover or Input.Click or Input.RightClick or Input.DoubleClick =>
                    _points.Length,
                Input.Keys => (Keys.Length + 2 * (Keys.Length - 1)) * stops,
                Input.Wheel => 2 * _points.Length,
                Input.Touch or Input.Pen or Input.TouchDensity => StrokeSteps * _points.Length,
                Input.Text => TypingSteps * Math.Max(stops, _points.Length),
                Input.Resize => Viewports.Length,
                Input.Hold => HoldSteps * _points.Length,
                Input.Gamepad => Buttons.Length * stops,
                Input.Themes => Tralys.Ui.Themes.All.Count,
                Input.Densities => 2,
                Input.States => StoryStates.All.Length,
                _ => throw new ArgumentOutOfRangeException(nameof(input)),
            };
        }

        public int Cycle { get; }

        public void Restart() => _tick = 0;

        // The frame's box fills the view, so the points spread over only what it holds.
        static Node FrameBox(Node root)
        {
            foreach (var child in root.Children)
                return child.Host?.Root ?? child;
            throw new InvalidOperationException("The stage shows no story.");
        }

        static void Shown(Node node, ref Rect shown)
        {
            foreach (var child in node.Children)
            {
                if (child.Place.Collapsed)
                    continue;
                if (!child.IsTransparent)
                    shown = shown.IsEmpty ? child.ViewBounds : shown.Union(child.ViewBounds);
                Shown(child, ref shown);
            }
        }

        static void Centres(UiView view, Node node, Type? subject, List<Vector2> points)
        {
            foreach (var child in node.Children)
            {
                if (child.Place.Collapsed || child.Place.Hidden)
                    continue;
                var target = subject is not null
                    ? child.Component == subject
                        ? child.Host?.Root
                        : null
                    : !child.IsTransparent
                    && (child.IsFocusable || child.Interaction?.Props.IsPressable == true)
                        ? child
                        : null;
                if (target is not null && points.Count < MaxPoints)
                {
                    var bounds = target.ViewBounds;
                    var centre = new Vector2(
                        bounds.X + bounds.Width / 2f,
                        bounds.Y + bounds.Height / 2f
                    );
                    if (
                        view.HitTest(centre * view.Scale) is { } hit
                        && target.IsAncestorOrSelfOf(hit)
                        && !points.Contains(centre)
                    )
                        points.Add(centre);
                }
                Centres(view, child, subject, points);
            }
        }

        static int Stops(Node node)
        {
            var stops = 0;
            foreach (var child in node.Children)
            {
                if (child.Place.Collapsed || child.Place.Hidden)
                    continue;
                if (
                    !child.IsTransparent
                    && child.IsFocusable
                    && child.TabIndex >= 0
                    && (child.States & InputStates.Disabled) == 0
                )
                    stops++;
                stops += Stops(child);
            }
            return stops;
        }

        public void Frame()
        {
            var tick = _tick++;
            var view = _stage.View;
            var seconds = FrameSeconds;
            switch (_input)
            {
                case Input.Rerender:
                    view.RerenderRoot();
                    break;
                case Input.Hover:
                    Move(view, Pointer.Mouse, Point(tick), MouseButtons.None);
                    break;
                case Input.Click:
                    Press(view, Point(tick), MouseButton.Left);
                    break;
                case Input.RightClick:
                    Press(view, Point(tick), MouseButton.Right);
                    break;
                case Input.DoubleClick:
                    Press(view, Point(tick), MouseButton.Left);
                    view.PointerDown(
                        Pointer.Mouse,
                        Point(tick),
                        MouseButton.Left,
                        ModifierKeys.None,
                        Now,
                        clicks: 2
                    );
                    view.PointerUp(
                        Pointer.Mouse,
                        Point(tick),
                        MouseButton.Left,
                        ModifierKeys.None,
                        Now
                    );
                    break;
                case Input.Keys:
                    var key = KeyAt(tick % Cycle);
                    view.KeyDown(key, ModifierKeys.None, false);
                    view.KeyUp(key, ModifierKeys.None);
                    break;
                case Input.Wheel:
                    view.Wheel(
                        Point(tick / 2),
                        new Vector2(0f, tick % 2 == 0 ? -1f : 1f),
                        ModifierKeys.None
                    );
                    break;
                case Input.Touch or Input.TouchDensity:
                    Stroke(view, Finger, Point(tick / StrokeSteps), tick % StrokeSteps);
                    break;
                case Input.Pen:
                    Stroke(view, Stylus, Point(tick / StrokeSteps), tick % StrokeSteps);
                    break;
                case Input.Text:
                    Type(view, Point(tick / TypingSteps), tick % TypingSteps);
                    break;
                case Input.Hold:
                    seconds = LongPress(view, Point(tick / HoldSteps), tick % HoldSteps);
                    break;
                case Input.Resize:
                    var viewport = Viewports[tick % Viewports.Length];
                    view.Scale = viewport.Scale;
                    view.Size = new Size(
                        viewport.Width / viewport.Scale,
                        viewport.Height / viewport.Scale
                    );
                    break;
                case Input.Gamepad:
                    var button = Buttons[tick % Buttons.Length];
                    view.Gamepad(button, true);
                    view.Gamepad(button, false);
                    view.Stick(
                        GamepadStick.Left,
                        tick % 2 == 0 ? new Vector2(0f, 0.8f) : Vector2.Zero
                    );
                    break;
                case Input.Themes:
                    _stage.Context.Theme = Tralys.Ui.Themes.All[tick % Tralys.Ui.Themes.All.Count];
                    break;
                case Input.Densities:
                    _stage.Context.Density = tick % 2 == 0 ? Density.Touch : Density.Pointer;
                    break;
                case Input.States:
                    _stage.State = StoryStates.All[tick % StoryStates.All.Length];
                    break;
            }
            _stage.Frame(seconds);
        }

        TimeSpan Now => _stage.Context.Now;

        Vector2 Point(int index) => _points[index % _points.Length] * _stage.View.Scale;

        // After the chain, each key alone at each stop: in a chain, one key can move the focus off what the next acts on.
        Key KeyAt(int step)
        {
            var chain = Keys.Length * _stops;
            if (step < chain)
                return Keys[step % Keys.Length];

            var alone = step - chain;
            return alone % 2 == 0 ? Key.Tab : Keys[1 + alone / (2 * _stops)];
        }

        void Move(UiView view, Pointer pointer, Vector2 at, MouseButtons held) =>
            view.PointerMove(pointer, at, held, held != MouseButtons.None, ModifierKeys.None, Now);

        void Press(UiView view, Vector2 at, MouseButton button)
        {
            Move(view, Pointer.Mouse, at, MouseButtons.None);
            view.PointerDown(Pointer.Mouse, at, button, ModifierKeys.None, Now);
            view.PointerUp(Pointer.Mouse, at, button, ModifierKeys.None, Now);
        }

        void Stroke(UiView view, Pointer pointer, Vector2 from, int step)
        {
            var across = from + new Vector2(12f, 0f);
            var down = from + new Vector2(12f, 12f);
            switch (step)
            {
                case 0 or 2:
                    view.PointerDown(pointer, from, MouseButton.Left, ModifierKeys.None, Now);
                    break;
                case 1:
                    view.PointerUp(pointer, from, MouseButton.Left, ModifierKeys.None, Now);
                    break;
                case 3:
                    Move(view, pointer, across, MouseButtons.Left);
                    break;
                case 4:
                    Move(view, pointer, down, MouseButtons.Left);
                    break;
                default:
                    view.PointerUp(pointer, down, MouseButton.Left, ModifierKeys.None, Now);
                    break;
            }
        }

        double LongPress(UiView view, Vector2 at, int step)
        {
            switch (step)
            {
                case 0:
                    view.PointerDown(Finger, at, MouseButton.Left, ModifierKeys.None, Now);
                    return UiView.HoldTime.TotalSeconds;
                case 1:
                    return FrameSeconds;
                default:
                    view.PointerUp(Finger, at, MouseButton.Left, ModifierKeys.None, Now);
                    return FrameSeconds;
            }
        }

        void Type(UiView view, Vector2 at, int step)
        {
            switch (step)
            {
                case 0:
                    view.KeyDown(Key.Tab, ModifierKeys.None, false);
                    view.KeyUp(Key.Tab, ModifierKeys.None);
                    break;
                case 3:
                    Press(view, at, MouseButton.Left);
                    break;
                case 1 or 4:
                    view.TextInput("7");
                    break;
                default:
                    view.KeyDown(Key.BackSpace, ModifierKeys.None, false);
                    view.KeyUp(Key.BackSpace, ModifierKeys.None);
                    break;
            }
        }
    }
}
