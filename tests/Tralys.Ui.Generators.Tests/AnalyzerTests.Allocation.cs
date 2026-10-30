using Tralys.Ui.Analyzers;

namespace Tralys.Ui.Generators.Tests;

public sealed partial class AnalyzerTests
{
    [Test]
    public async Task EachFramePathBan_IsReportedWhereItIsWritten()
    {
        const string source = """
            namespace Sample
            {
                using System.Collections.Generic;
                using System.Linq;

                class Hud
                {
                    int _hp;
                    float? _shrink;
                    List<int> _values = new();
                    IEnumerable<int> _sequence = new List<int>();

                    [Tralys.Ui.FramePath]
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

                    [Tralys.Ui.FramePath]
                    IEnumerable<int> Values()
                    {
                        yield return _hp;
                    }

                    static void Log(string text, params int[] values) { }
                }
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
    public async Task AllocationFreeCode_AndCodeOffTheFramePath_AreNotReported()
    {
        const string source = """
            namespace Sample
            {
                using System;
                using System.Collections.Generic;
                using System.Linq;

                class Hud
                {
                    int _hp;
                    List<int> _values = new();
                    int[] _slots = new int[4];
                    char[] _text = new char[16];

                    [Tralys.Ui.FramePath]
                    int Frame()
                    {
                        var total = 0;
                        foreach (var value in _values)
                            total += value;
                        foreach (var slot in _slots)
                            total += slot;
                        foreach (var letter in "hp")
                            total += letter;
                        _text.AsSpan().TryWrite($"{_hp}" + $"/{total}", out var written);
                        Func<int, int> twice = x => x * 2;
                        Action<Hud> raise = static hud => hud._hp++;
                        const string label = "hp" + ":";
                        return Same(total, total) ? twice(total) : label.Length;
                    }

                    static bool Same<T>(T a, T b) where T : IEquatable<T> => a.Equals(b);

                    int Setup() => _values.Where(v => v > _hp).Count();
                }
            }
            """;

        var reported = await Harness.Analyze(new FramePathAnalyzer(), source);

        await Assert.That(reported).IsEmpty();
    }

    [Test]
    public async Task AnAttributesArguments_AreOffTheFramePath_ThoughItsMemberIsOn()
    {
        const string source = """
            namespace Sample
            {
                [System.AttributeUsage(System.AttributeTargets.All)]
                class TunedAttribute : System.Attribute
                {
                    public TunedAttribute(object value, params int[] steps) { }
                }

                [Tuned(0, 1, 2)]
                [Tralys.Ui.FramePath]
                class Panel
                {
                    [Tuned(0)]
                    int Level { get; set; }

                    [Tuned(0)]
                    void Frame(int value)
                    {
                        object boxed = value;
                    }
                }
            }
            """;

        var reported = await Harness.Analyze(new FramePathAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([("TRL2006", Harness.LineOf(source, "object boxed"))]);
    }

    [Test]
    public async Task OverridesAndImplementationsOfAFramePathMember_AreOnTheFramePath()
    {
        const string source = """
            namespace Sample
            {
                using System.Linq;

                interface IScene
                {
                    [Tralys.Ui.FramePath]
                    int Frame();

                    [Tralys.Ui.FramePath]
                    int Count { get; }
                }

                abstract class Element
                {
                    [Tralys.Ui.FramePath]
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
    public async Task What_a_frame_path_member_calls_is_held_to_its_bans_naming_the_member_that_reaches_it()
    {
        const string source = """
            namespace Sample
            {
                using System.Linq;

                sealed class Shop
                {
                    readonly int[] _prices = new int[4];
                    string _name = "";

                    public Shop(string name) => _name = name + "!";

                    public int Total => _prices.Sum();

                    public string Name
                    {
                        get => _name;
                        set => _name = value + "?";
                    }

                    public string Label(string unit) => Format(unit);

                    static string Format(string unit) => unit + "s";

                    public int Unused() => _prices.Max();
                }

                sealed class View
                {
                    [Tralys.Ui.FramePath]
                    void Frame(Shop shop)
                    {
                        var made = new Shop("a");
                        var total = shop.Total;
                        var name = shop.Name;
                        var label = shop.Label(name);
                    }
                }
            }
            """;

        var reported = await Harness.Reported(new FramePathReachAnalyzer(), source);

        await Assert
            .That(reported.Select(d => d.Located()))
            .IsEquivalentTo([
                ("TRL2004", Harness.LineOf(source, "name + \"!\"")),
                ("TRL2001", Harness.LineOf(source, "_prices.Sum()")),
                ("TRL2004", Harness.LineOf(source, "unit + \"s\"")),
            ]);
        await Assert.That(reported.All(d => d.GetMessage().Contains("View.Frame"))).IsTrue();
    }

    [Test]
    public async Task What_a_frame_path_member_only_hands_on_as_a_delegate_or_handler_is_not_held_to_its_bans()
    {
        const string source = """
            namespace Sample
            {
                using System;
                using System.Linq;

                sealed class View
                {
                    static readonly int[] Prices = new int[4];
                    static event Action? Changed;
                    Func<int>? _later;

                    [Tralys.Ui.FramePath]
                    void Frame()
                    {
                        Changed += OnChanged;
                        _later = Later;
                        Func<int> total = static () => Total();
                        Run(static () => Total());
                        int Local() => Total();
                        _later = Local;
                    }

                    static void OnChanged() => Prices.Sum();

                    static int Later() => Prices.Max();

                    static int Total() => Prices.Min();

                    static void Run(Func<int> run) { }
                }
            }
            """;

        var reported = await Harness.Analyze(new FramePathReachAnalyzer(), source);

        await Assert.That(reported).IsEmpty();
    }

    [Test]
    public async Task A_value_calling_what_enum_value_type_or_object_declares_is_boxed_on_a_frame_path_and_off_it_where_reached()
    {
        const string source = """
            namespace Sample
            {
                enum Mood { Calm }

                struct Plain { public int X; }

                struct Spelled
                {
                    public override string ToString() => "s";
                }

                sealed class View
                {
                    Mood _mood;
                    Plain _plain;
                    Spelled _spelled;

                    [Tralys.Ui.FramePath]
                    int Frame()
                    {
                        var name = _mood.ToString();
                        var hash = _plain.GetHashCode();
                        var kind = _plain.GetType();
                        var own = _spelled.ToString();
                        var number = _plain.X.ToString();
                        return Label().Length + Named(_mood).Length + Free(_plain).Length + hash;
                    }

                    string Label() => _mood.ToString();

                    static string Named<T>(T value) where T : struct, System.Enum => value.ToString();

                    static string Free<T>(T value) where T : struct => value.ToString();
                }
            }
            """;

        var direct = await Harness.Analyze(new FramePathAnalyzer(), source);
        var reached = await Harness.Analyze(new FramePathReachAnalyzer(), source);

        await Assert
            .That(direct)
            .IsEquivalentTo([
                ("TRL2006", Harness.LineOf(source, "var name")),
                ("TRL2006", Harness.LineOf(source, "var hash")),
                ("TRL2006", Harness.LineOf(source, "var kind")),
            ]);
        await Assert
            .That(reached)
            .IsEquivalentTo([
                ("TRL2006", Harness.LineOf(source, "string Label()")),
                ("TRL2006", Harness.LineOf(source, "string Named<T>")),
            ]);
    }

    [Test]
    public async Task CreatingABclObservableCollection_IsFlagged()
    {
        const string source = """
            using System.Collections.ObjectModel;

            namespace Sample;

            class Items : ObservableCollection<int>;

            class Inventory
            {
                public Items Slots { get; } = new Items();

                public ObservableCollection<string> Names { get; } = [];
            }
            """;

        var lines = await Harness.Analyze(new CollectionChangedAnalyzer(), source);

        await Assert
            .That(lines)
            .IsEquivalentTo([
                ("TRL2101", Harness.LineOf(source, "new Items()")),
                ("TRL2101", Harness.LineOf(source, "Names { get; } = []")),
            ]);
    }

    [Test]
    public async Task AnObservableListSeenThroughAWiderElementType_IsFlagged()
    {
        const string source = """
            using System.Collections.Generic;
            using Tralys.Ui.Mvvm;

            namespace Sample;

            interface IAnimal;

            sealed class Dog : IAnimal;

            class Shelter
            {
                readonly ObservableList<Dog> _pups = new();

                public IReadOnlyList<IAnimal> Young => _pups;

                public IReadOnlyList<Dog> Pups => _pups;

                public IEnumerable<IAnimal> Walked => _pups;
            }
            """;

        var lines = await Harness.Analyze(new ObservableCovarianceAnalyzer(), source);

        await Assert
            .That(lines)
            .IsEquivalentTo([("TRL2102", Harness.LineOf(source, "Young => _pups"))]);
    }
}
