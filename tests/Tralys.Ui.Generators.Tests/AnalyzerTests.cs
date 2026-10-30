using Tralys.Ui.Analyzers;
using Tralys.Ui.Analyzers.CodeFixes;

namespace Tralys.Ui.Generators.Tests;

public sealed partial class AnalyzerTests
{
    const string Preamble = """
        using System;
        using System.Runtime.CompilerServices;
        using Tralys.Ui;

        namespace Sample;

        public sealed class Model
        {
            public int Count;
        }

        public readonly record struct CardProps
        {
            public TextValue Title { get; init; }

            public string[]? Items { get; init; }
        }

        public sealed class Card : Component<CardProps>
        {
            protected override void Render(Ui ui, in CardProps p) => ui.Text(p.Title);
        }

        """;

    [Test]
    public async Task Props_and_components_keep_the_shapes_the_generator_relies_on()
    {
        const string source =
            Preamble
            + """
                public readonly record struct GoodProps
                {
                    public TextValue Text { get; init; }

                    public int? Count { get; init; }

                    public (string Label, int Rank) Pair { get; init; }
                }

                public sealed class Good : Component<GoodProps>
                {
                    static int s_renders;

                    protected override void Render(Ui ui, in GoodProps p) => s_renders++;
                }

                public record struct LooseProps
                {
                    public int Size { get; set; }
                }

                public sealed class Loose : Component<LooseProps>
                {
                    protected override void Render(Ui ui, in LooseProps p) { }
                }

                public readonly record struct SeededProps
                {
                    public SeededProps() { }

                    public int Size { get; init; } = 4;
                }

                public sealed class Seeded : Component<SeededProps>
                {
                    protected override void Render(Ui ui, in SeededProps p) { }
                }

                public readonly record struct PointProps(int X);

                public sealed class Point : Component<PointProps>
                {
                    protected override void Render(Ui ui, in PointProps p) { }
                }

                public struct Spot
                {
                    public int X;
                }

                public readonly record struct PlacedProps
                {
                    public float Grow { get; init; }

                    public Spot Spot { get; init; }

                    public (int Index, Spot Spot) Pinned { get; init; }
                }

                public sealed class Placed : Component<PlacedProps>
                {
                    protected override void Render(Ui ui, in PlacedProps p) { }
                }

                public class Open : Component<GoodProps>
                {
                    protected override void Render(Ui ui, in GoodProps p) { }
                }

                public sealed class Stateful : Component<GoodProps>
                {
                    int _renders;

                    public string Name { get; set; } = "";

                    protected override void Render(Ui ui, in GoodProps p) => _renders++;
                }

                public sealed class Made : Component<GoodProps>
                {
                    public Made(int seed) { }

                    protected override void Render(Ui ui, in GoodProps p) { }
                }

                public readonly record struct PickProps<TItem>
                {
                    public TItem? Value { get; init; }
                }

                public sealed class Pick<TItem> : Component<PickProps<TItem>>
                {
                    protected override void Render(Ui ui, in PickProps<TItem> p) { }
                }

                public sealed class Plain<TItem> : Component<GoodProps>
                {
                    protected override void Render(Ui ui, in GoodProps p) { }
                }
                """;

        var reported = await Harness.Analyze(new ComponentShapeAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRL3001", Harness.LineOf(source, "record struct LooseProps")),
                ("TRL3001", Harness.LineOf(source, "int Size { get; set; }")),
                ("TRL3001", Harness.LineOf(source, "int Size { get; init; } = 4")),
                ("TRL3001", Harness.LineOf(source, "record struct PointProps")),
                ("TRL3001", Harness.LineOf(source, "float Grow")),
                ("TRL3001", Harness.LineOf(source, "Spot Spot { get")),
                ("TRL3001", Harness.LineOf(source, "Spot Spot) Pinned")),
                ("TRL3006", Harness.LineOf(source, "class Open")),
                ("TRL3006", Harness.LineOf(source, "int _renders")),
                ("TRL3006", Harness.LineOf(source, "string Name")),
                ("TRL3006", Harness.LineOf(source, "class Made")),
                ("TRL3006", Harness.LineOf(source, "class Pick<TItem>")),
            ]);
    }

    [Test]
    public async Task Callback_and_draw_lambdas_must_be_static()
    {
        const string source =
            Preamble
            + """
                static class Wiring
                {
                    static readonly CanvasDraw<Model> Drawn = static (dc, model, info) => { };
                    static readonly CanvasDraw<Model> Draw = (dc, model, info) => { };

                    [FramePath]
                    static void Wire(Model model)
                    {
                        var still = Callback.Of(model, static m => m.Count++);
                        var loose = Callback.Of(model, m => m.Count++);
                        Action<Model> plain = m => m.Count++;
                    }
                }
                """;

        var reported = await Harness.Analyze(new StaticLambdaAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRL3002", Harness.LineOf(source, "Draw = (dc")),
                ("TRL3002", Harness.LineOf(source, "var loose")),
            ]);
    }

    [Test]
    public async Task A_lambda_carrying_the_frame_path_is_checked_wherever_it_is_written()
    {
        const string source =
            Preamble
            + """
                static class Rows
                {
                    static readonly Slot<string> Row = Slot<string>.Of(static (ui, item, index) => ui.Text(item + "!"));
                    static readonly Func<string, string> Shout = static text => text + "!";
                }
                """;

        var reported = await Harness.Analyze(new FramePathAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([("TRL2004", Harness.LineOf(source, "item + \"!\""))]);
    }

    [Test]
    public async Task Each_frame_path_ban_is_reported_on_the_line_it_is_written_under_its_own_rule()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            using Tralys.Ui;

            namespace Sample;

            class Hud
            {
                int _hp;
                float? _shrink;
                List<int> _values = new();
                IEnumerable<int> _sequence = new List<int>();

                [FramePath]
                void Frame(string name)
                {
                    var any = _values.Any();
                    foreach (var value in _sequence) { }
                    Log("hp", 1, 2);
                    var label = name + name;
                    var made = string.Create(null, $"{_hp}");
                    System.Action raise = () => _hp++;
                    object boxed = _hp;
                    var same = _shrink.Equals(_shrink);
                    var hits = 0;
                    void Hit() => hits++;
                    System.Action strike = () => Hit();
                }

                [FramePath]
                IEnumerable<int> Values()
                {
                    yield return _hp;
                }

                static void Log(string text, params int[] values) { }
            }
            """;

        var reported = await Harness.Analyze(new FramePathAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRL2001", Harness.LineOf(source, "_values.Any()")),
                ("TRL2002", Harness.LineOf(source, "foreach (var value")),
                ("TRL2003", Harness.LineOf(source, "Log(\"hp\"")),
                ("TRL2004", Harness.LineOf(source, "name + name")),
                ("TRL2004", Harness.LineOf(source, "string.Create")),
                ("TRL2005", Harness.LineOf(source, "() => _hp++")),
                ("TRL2006", Harness.LineOf(source, "object boxed")),
                ("TRL2006", Harness.LineOf(source, "_shrink.Equals(_shrink)")),
                ("TRL2005", Harness.LineOf(source, "() => Hit()")),
                ("TRL2002", Harness.LineOf(source, "yield return")),
            ]);
    }

    [Test]
    public async Task Overrides_and_implementations_of_a_frame_path_member_are_on_the_frame_path()
    {
        const string source = """
            using System.Linq;
            using Tralys.Ui;

            namespace Sample;

            interface IScene
            {
                [FramePath]
                int Frame();

                [FramePath]
                int Count { get; }
            }

            abstract class Element
            {
                [FramePath]
                protected virtual int Measure() => 0;

                protected virtual int Load() => 0;
            }

            class Panel : Element
            {
                int[] _items = new int[4];

                protected override int Measure() => _items.Sum();

                protected override int Load() => _items.Sum();
            }

            sealed class Grid : Panel
            {
                int[] _cells = new int[4];

                protected override int Measure() => _cells.Max();
            }

            sealed class Scene : IScene
            {
                int[] _items = new int[4];

                public int Frame() => _items.Min();

                int IScene.Count => _items.Count();
            }
            """;

        var reported = await Harness.Analyze(new FramePathAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRL2001", Harness.LineOf(source, "Measure() => _items.Sum()")),
                ("TRL2001", Harness.LineOf(source, "_cells.Max()")),
                ("TRL2001", Harness.LineOf(source, "_items.Min()")),
                ("TRL2001", Harness.LineOf(source, "_items.Count()")),
            ]);
    }

    [Test]
    public async Task A_formatted_text_is_allowed_where_an_interpolated_string_is_not()
    {
        const string source =
            Preamble
            + """
                public sealed class Counter : Component<CardProps>
                {
                    protected override void Render(Ui ui, in CardProps p)
                    {
                        ui.Text(ui.Format($"{p.Items!.Length} items"));
                        string label = $"{p.Items!.Length} items";
                        ui.Text(label);
                    }
                }
                """;

        var reported = await Harness.Analyze(new FramePathAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([("TRL2004", Harness.LineOf(source, "string label"))]);
    }

    [Test]
    public async Task A_helper_forwards_its_callers_identity_to_one_top_level_call_or_a_fragment()
    {
        const string source =
            Preamble
            + """
                static class Helpers
                {
                    [FramePath]
                    static void Forwarded(Ui ui, string text, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
                        ui.Text(text, file: file, line: line);

                    [FramePath]
                    static void Wrapped(Ui ui, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
                    {
                        using (ui.Fragment(file: file, line: line))
                        {
                            var open = ui.State(false);
                            ui.Text("a");
                            ui.Text("b");
                        }
                    }

                    [FramePath]
                    static void Quiet(Ui ui)
                    {
                        var theme = ui.Theme;
                    }

                    [FramePath]
                    static void Bare(Ui ui) => ui.Text("x");

                    [FramePath]
                    static void Dropped(Ui ui, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) => ui.Text("x");

                    [FramePath]
                    static void Pair(Ui ui, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
                    {
                        ui.Text("a", file: file, line: line);
                        ui.Text("b", file: file, line: line);
                    }

                    [FramePath]
                    static void Hooked(Ui ui, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
                    {
                        using (ui.Box(file: file, line: line))
                        {
                            var open = ui.State(false);
                        }
                    }
                }

                [Stories<Card>("Cards")]
                static class CardStories
                {
                    [FramePath]
                    public static void Two(Ui ui)
                    {
                        ui.Text("a");
                        ui.Text("b");
                    }
                }
                """;

        var reported = (await Harness.Analyze(new IdentityAnalyzer(), source)).Where(d =>
            d.Id == "TRL3003"
        );

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRL3003", Harness.LineOf(source, "void Bare")),
                ("TRL3003", Harness.LineOf(source, "void Dropped")),
                ("TRL3003", Harness.LineOf(source, "void Pair")),
                ("TRL3003", Harness.LineOf(source, "void Hooked")),
            ]);
    }

    [Test]
    public async Task A_call_in_a_loop_is_keyed_unless_a_scope_inside_the_loop_tells_its_iterations_apart()
    {
        const string source =
            Preamble
            + """
                public sealed class Lists : Component<CardProps>
                {
                    protected override void Render(Ui ui, in CardProps p)
                    {
                        foreach (var item in p.Items!)
                            ui.Text(item);
                        for (var i = 0; i < 3; i++)
                            ui.Text("keyed", key: i);
                        foreach (var item in p.Items!)
                            using (ui.Box(key: item))
                                ui.Text(item);
                        foreach (var item in p.Items!)
                            using (ui.Box(key: item))
                            {
                                var open = ui.State(false);
                            }
                        foreach (var item in p.Items!)
                            ui.Text(ui.Format($"{item}"), key: item);
                    }
                }
                """;

        var reported = await Harness.Analyze(new IdentityAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRL3004", Harness.LineOf(source, "ui.Text(item);")),
                ("TRL3004", Harness.LineOf(source, "var open = ui.State(false);")),
            ]);
    }

    [Test]
    public async Task A_method_or_story_that_takes_a_Ui_is_on_the_frame_path()
    {
        const string source =
            Preamble
            + """
                static class Helpers
                {
                    static void Unmarked(Ui ui) { }

                    [FramePath]
                    static void Marked(Ui ui) { }
                }

                [FramePath]
                static class MarkedHelpers
                {
                    static void Inside(Ui ui) { }
                }

                [Stories<Card>("Cards")]
                static class CardStories
                {
                    public static void Plain(Ui ui) { }
                }
                """;

        var reported = await Harness.Analyze(new UiFramePathAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRL3005", Harness.LineOf(source, "void Unmarked")),
                ("TRL3005", Harness.LineOf(source, "void Plain")),
            ]);
    }

    [Test]
    public async Task The_frame_path_fix_marks_the_method()
    {
        const string source = """
            using Tralys.Ui;

            namespace Sample;

            static class Helpers
            {
                /// <summary>Shows nothing.</summary>
                static void Unmarked(Ui ui) { }
            }
            """;

        var fixedSource = await Harness.Fix(
            new UiFramePathAnalyzer(),
            new FramePathCodeFix(),
            source
        );

        await Assert
            .That(fixedSource)
            .IsEqualTo(
                """
                using Tralys.Ui;

                namespace Sample;

                static class Helpers
                {
                    /// <summary>Shows nothing.</summary>
                    [FramePath]
                    static void Unmarked(Ui ui) { }
                }
                """
            );
    }

    [Test]
    public async Task Ui_call_arguments_are_named_and_a_render_leaves_its_roots_placement_to_its_caller()
    {
        const string source =
            Preamble
            + """
                public sealed class Uses : Component<CardProps>
                {
                    static readonly CanvasDraw<Model> Draw = static (dc, model, info) => { };
                    static readonly Slot Aside = Slot.Of(static ui => ui.Text("aside", grow: 1));

                    protected override void Render(Ui ui, in CardProps p)
                    {
                        using (ui.Box(margin: 4))
                        {
                            ui.Text("content first", grow: 1);
                            ui.Card(title: "named");
                            ui.Card("unnamed");
                            ui.Canvas(state: new Model(), draw: Draw);
                            ui.Canvas(new Model(), draw: Draw);
                        }
                    }
                }
                """;

        var reported = await Harness.Analyze(new UiCallAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRL3008", Harness.LineOf(source, "ui.Box(margin: 4)")),
                ("TRL3007", Harness.LineOf(source, "ui.Card(\"unnamed\")")),
                ("TRL3007", Harness.LineOf(source, "ui.Canvas(new Model()")),
            ]);
    }

    [Test]
    public async Task An_argument_named_as_a_placement_member_but_of_another_type_doesnt_place_the_root()
    {
        const string source =
            Preamble
            + """
                public sealed class Rows : Component<CardProps>
                {
                    static readonly IItemsSource<int> Items = ItemsSource.From(new[] { 1, 2 });
                    static readonly KeyOf<int> Key = static i => i;
                    static readonly Slot<int> Row = Slot<int>.Of(static (ui, i, _) => ui.Text("row"));

                    protected override void Render(Ui ui, in CardProps p) =>
                        ui.VirtualList(items: Items, itemKey: Key, row: Row);
                }
                """;

        var reported = await Harness.Analyze(new UiCallAnalyzer(), source);

        await Assert.That(reported).IsEmpty();
    }

    [Test]
    public async Task A_scope_is_the_resource_of_a_using_or_returned_to_one()
    {
        const string source =
            Preamble
            + """
                public sealed class Scopes : Component<CardProps>
                {
                    protected override void Render(Ui ui, in CardProps p)
                    {
                        using (ui.Box()) { }
                        using var row = ui.Box();
                        ui.Grid();
                    }

                    [FramePath]
                    static Scope Panel(Ui ui, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
                        ui.Box(file: file, line: line);
                }
                """;

        var reported = await Harness.Analyze(new ScopeAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([("TRL3010", Harness.LineOf(source, "ui.Grid();"))]);
    }

    [Test]
    public async Task Hooks_keep_values_they_can_compare_and_row_slots_remember_what_they_can_reset()
    {
        const string source =
            Preamble
            + """
                public struct Spot
                {
                    public int X;
                }

                public sealed class Cache;

                public sealed class Reused : IResettable
                {
                    public void Reset() { }
                }

                public sealed class Spread : LayoutAlgorithm<Spot>
                {
                    public static readonly Spread Instance = new();

                    public override Size Measure(
                        ref LayoutContext cx,
                        Spot state,
                        AvailableSpace width,
                        AvailableSpace height
                    ) => default;

                    public override void Arrange(ref LayoutContext cx, Spot state, Size size) { }
                }

                public sealed class Hooks : Component<CardProps>
                {
                    static readonly Slot<string> Row = Slot<string>.Of(static (ui, item, index) =>
                    {
                        var rowCache = ui.Remember(static () => new Cache());
                        var reused = ui.Remember(static () => new Reused());
                    });

                    protected override void Render(Ui ui, in CardProps p)
                    {
                        var count = ui.State(0);
                        var spot = ui.State(new Spot());
                        var cache = ui.Remember(static () => new Cache());
                        ui.Effect(cache, new Spot(), static (c, s, cx) => { });
                        ui.Effect(cache, (cache, 1), static (c, s, cx) => { });
                        ui.Effect(cache, (Cache: cache, Count: 1), static (c, s, cx) => { });
                        ui.Effect(cache, (Cache: cache, Spot: new Spot()), static (c, s, cx) => { });
                        using (ui.Layout(algorithm: Spread.Instance, state: new Spot())) { }
                    }
                }
                """;

        var reported = await Harness.Analyze(new HookArgumentAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRL3011", Harness.LineOf(source, "var rowCache")),
                ("TRL3012", Harness.LineOf(source, "ui.State(new Spot())")),
                ("TRL3012", Harness.LineOf(source, "ui.Effect(cache, new Spot()")),
                ("TRL3012", Harness.LineOf(source, "ui.Effect(cache, (Cache: cache, Spot:")),
                ("TRL3012", Harness.LineOf(source, "ui.Layout(algorithm")),
            ]);
    }
}
