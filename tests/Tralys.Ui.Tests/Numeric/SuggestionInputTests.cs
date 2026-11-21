using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class SuggestionInputTests
{
    static readonly string[] Projects =
    [
        "Dreamline",
        "Dreamland",
        "Nimbus",
        "Sunrise",
        "Driftwood",
        "Aurora",
        "Ørn Systems",
        "dreamline",
    ];

    static ISuggestionSource Local(string[] names) =>
        new LocalSuggestions(names.Length == 0 ? static _ => { } : n => n.AddRange(names));

    [Test]
    public async Task Its_rows_show_its_item_slot_and_its_field_ends_in_its_trailing_slot()
    {
        var owner = new SuggestOwner
        {
            Source = Local(Projects),
            Item = Slot<SuggestionSlot>.Of(
                static (ui, slot, _) => ui.Text(ui.Format($"> {slot.Text}"))
            ),
            Trailing = Slot.Of(static ui =>
            {
                using (ui.Box(width: 16, height: 16, id: "pick")) { }
            }),
        };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));

        driver.Type("nim");
        var field = driver.Get("field").ViewBounds;
        var pick = driver.Get("pick").ViewBounds;

        await Assert.That(driver.GetText("> Nimbus")).IsNotNull();
        await Assert
            .That(
                pick.X >= field.X
                    && pick.Right <= field.Right
                    && pick.Y >= field.Y
                    && pick.Bottom <= field.Bottom
            )
            .IsTrue();
    }

    [Test]
    public async Task Typing_offers_the_names_starting_with_the_text_then_those_holding_it_ignoring_case_once_each()
    {
        var owner = new SuggestOwner { Source = Local(Projects) };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));
        var shown = new List<string>();
        foreach (
            var key in (string[])["d", "r", "\b", "\b", "n", "i", "x", "\b", "\b", "\b", " ", " dr"]
        )
        {
            if (key == "\b")
                driver.Press(Key.BackSpace);
            else if (key == " dr")
            {
                driver.Press(Key.A, ModifierKeys.Control);
                driver.Type(key);
            }
            else
                driver.Type(key);
            shown.Add(string.Join("|", Suggests.Rows(driver)));
        }

        await Assert
            .That(shown)
            .IsEquivalentTo([
                "Dreamline|Dreamland|Driftwood",
                "Dreamline|Dreamland|Driftwood",
                "Dreamline|Dreamland|Driftwood",
                "",
                "Nimbus|Dreamline|Dreamland|Sunrise|Ørn Systems",
                "Nimbus",
                "",
                "Nimbus",
                "Nimbus|Dreamline|Dreamland|Sunrise|Ørn Systems",
                "",
                "",
                "Dreamline|Dreamland|Driftwood",
            ]);
    }

    [Test]
    public async Task No_rows_show_once_typed_back_to_blank_left_by_the_focus_or_given_text_while_unfocused()
    {
        var owner = new SuggestOwner { Source = Local(Projects) };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));

        driver.Type("d");
        driver.Press(Key.BackSpace);
        var blanked = Suggests.Rows(driver).Count;
        driver.Type("d");
        var again = Suggests.Rows(driver).Count;
        driver.Tab(shift: true);
        var left = Suggests.Rows(driver).Count;
        owner.Text.Set("dr");
        driver.Settle();

        await Assert.That((blanked, again, left)).IsEqualTo((0, 3, 0));
        await Assert.That(Suggests.Rows(driver)).IsEmpty();
    }

    [Test]
    public async Task The_arrows_wrap_round_the_rows_the_pointer_lights_one_too_and_enter_tab_or_a_click_takes_the_lit_row()
    {
        var owner = new SuggestOwner { Source = Local(Projects) };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));
        driver.Type("dr");

        var lit = new List<string> { Suggests.Lit(driver) ?? "" };
        foreach (
            var (key, modifiers) in ((Key, ModifierKeys)[])
                [
                    (Key.Up, default),
                    (Key.Down, default),
                    (Key.Down, default),
                    (Key.Down, ModifierKeys.Control),
                ]
        )
        {
            driver.Press(key, modifiers);
            lit.Add(Suggests.Lit(driver) ?? "");
        }
        driver.Press(Key.Tab);
        var tabbed = (
            owner.Text.ToString(),
            Suggests.Rows(driver).Count,
            driver.Focused == Suggests.Text(driver),
            Suggests.Selection(driver)
        );
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("dr");
        driver.Hover(Suggests.Row(driver, 2));
        var hovered = Suggests.Lit(driver);
        driver.Press(Key.Up);
        driver.Press(Key.Enter);
        var entered = (owner.Text.ToString(), Suggests.Rows(driver).Count);
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("ni");
        driver.Click(Suggests.Row(driver, 0));

        await Assert
            .That(lit)
            .IsEquivalentTo(["Dreamline", "Driftwood", "Dreamline", "Dreamland", "Dreamland"]);
        await Assert.That(tabbed).IsEqualTo(("Dreamland", 0, true, (9, 9)));
        await Assert.That(hovered).IsEqualTo("Driftwood");
        await Assert.That(entered).IsEqualTo(("Dreamland", 0));
        await Assert.That(owner.Submits).IsEmpty();
        await Assert.That(owner.Text.ToString()).IsEqualTo("Nimbus");
        await Assert.That(driver.Focused).IsSameReferenceAs(Suggests.Text(driver));
    }

    [Test]
    public async Task Keys_a_row_cannot_take_stay_the_fields_and_shift_tab_steps_back_taking_none()
    {
        var owner = new SuggestOwner { Source = Local(["Nightfall"]), EmptyText = "No match" };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));

        driver.Type("Nightfall");
        driver.Press(Key.Enter);
        var matched = (owner.Submits.Count, Suggests.Rows(driver).Count);
        driver.Type("x");
        var empty = Suggests.IsOpen(driver);
        var up = driver.KeyDown(Key.Up);
        driver.KeyUp(Key.Up);
        driver.Press(Key.Enter);
        var closed = Suggests.IsOpen(driver);
        driver.Press(Key.BackSpace);
        driver.Tab(shift: true);

        await Assert.That(matched).IsEqualTo((1, 0));
        await Assert.That((empty, up, closed)).IsEqualTo((true, false, false));
        await Assert.That(owner.Submits).IsEquivalentTo(["Nightfall", "Nightfallx"]);
        await Assert.That(owner.Text.ToString()).IsEqualTo("Nightfall");
        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("before"));
    }

    [Test]
    public async Task Escape_closes_only_the_rows_with_any_modifier_and_the_next_reaches_the_overlay_around_the_field()
    {
        var closes = new Counter();
        var source = Local(Projects);
        var text = new ObservableText();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Overlay(
                    open: true,
                    onOpenChange: Callback<bool>.Of(closes, static (c, _) => c.Value++)
                )
            )
                ui.SuggestionInput(text: text, source: source, width: 200, id: "field");
        });
        driver.Focus(Suggests.Text(driver));

        driver.Type("d");
        driver.Press(Key.Escape);
        var first = (Suggests.Rows(driver).Count, closes.Value);
        driver.Type("r");
        driver.Press(Key.Escape, ModifierKeys.Shift);
        var shifted = (Suggests.Rows(driver).Count, closes.Value);
        driver.Press(Key.Escape);

        await Assert.That((first, shifted)).IsEqualTo(((0, 0), (0, 0)));
        await Assert.That(closes.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_groups_caption_and_a_right_click_take_no_row_and_a_tap_takes_one_keeping_the_fields_focus_and_caret()
    {
        var owner = new SuggestOwner { Source = new GroupedNames() };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));
        driver.Type("xyz");
        driver.Press(Key.Left);
        driver.Press(Key.Down);

        var caption = driver.GetText("Towns");
        driver.Hover(caption);
        var hovered = Suggests.Lit(driver);
        driver.Click(caption);
        var clicked = (
            owner.Text.ToString(),
            Suggests.Selection(driver),
            driver.Focused == Suggests.Text(driver)
        );
        driver.Click(Suggests.Row(driver, 0), button: MouseButton.Right);
        var right = owner.Text.ToString();
        driver.Tap(Suggests.Row(driver, 1));

        await Assert.That(hovered).IsEqualTo("Brightwater");
        await Assert.That(clicked).IsEqualTo(("xyz", (2, 2), true));
        await Assert.That(right).IsEqualTo("xyz");
        await Assert.That(owner.Text.ToString()).IsEqualTo("Brightwater");
        await Assert.That(driver.Focused).IsSameReferenceAs(Suggests.Text(driver));
    }

    [Test]
    public async Task The_popup_opens_below_the_field_as_wide_as_it_counts_the_matches_past_its_rows_and_holds_its_footer()
    {
        var label = new Entry("first");
        var owner = new SuggestOwner
        {
            Source = Local(["Aaron Long Name Longer Than The Field", "Ab", "Ac", "Ad"]),
            MaxSuggestions = 3,
            Footer = Slot.Of(label, static (ui, l) => ui.Text(l.Value)),
            Width = 120f,
            MinPopupWidth = 160f,
            MaxPopupHeight = 60f,
        };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));

        driver.Type("a");
        var field = driver.Get("field").ViewBounds;
        var popup = Nodes.Find(driver.Get<SuggestionRows>(), NodeKind.Box).ViewBounds;
        var more = driver.GetText("+1 more · keep typing").ViewBounds;
        var cut = driver.GetText("Aaron Long Name Longer Than The Field").ViewBounds;
        label.Value = "second";
        driver.Settle();

        await Assert.That((popup.X, popup.Y)).IsEqualTo((field.X, field.Bottom));
        await Assert.That(popup.Width).IsEqualTo(160f);
        await Assert.That(popup.Height).IsEqualTo(70f);
        await Assert.That(cut.Right).IsLessThan(popup.Right);
        await Assert.That(more.Width).IsGreaterThan(0f);
        await Assert.That(driver.GetText("second").ViewBounds.Width).IsGreaterThan(0f);
    }

    [Test]
    public async Task A_remote_source_asks_once_typing_pauses_and_drops_an_answer_to_text_since_changed()
    {
        var server = new FakeServer();
        var owner = new SuggestOwner { Source = server.Source() };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));

        driver.Type("d", settle: false);
        driver.Frame(0.1f);
        driver.Type("r", settle: false);
        driver.Frame(0.1f);
        var early = server.Asks.Count;
        driver.Frame(0.2f);
        driver.Frame(0f);
        driver.Type("x", settle: false);
        server.Answer(0, "Dreamland");
        driver.Frame(0f);

        await Assert.That(early).IsEqualTo(0);
        await Assert.That(server.Queries).IsEquivalentTo(["dr"]);
        await Assert.That(Suggests.IsOpen(driver)).IsFalse();
    }

    [Test]
    public async Task Rows_an_owner_fills_as_its_text_changes_ask_once_typing_pauses_and_a_taken_row_goes_to_the_owner()
    {
        var server = new FakeServer();
        var source = server.Source();
        var taken = new List<string>();
        var owner = new SuggestOwner
        {
            Rows = new SuggestionList(5),
            OnAccept = Callback<SuggestionSlot>.Of(taken, static (t, slot) => t.Add(slot.Text)),
        };
        owner.Text.Changed += () => source.Suggest(owner.Text.Span, owner.Rows);
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));

        driver.Type("d", settle: false);
        driver.Frame(0.1f);
        var early = server.Asks.Count;
        driver.Frame(0.3f);
        driver.Frame(0f);
        server.Answer(0, "Dreamland");
        driver.Frame(0f);
        driver.Press(Key.Enter);

        await Assert.That(early).IsEqualTo(0);
        await Assert.That(server.Queries).IsEquivalentTo(["d"]);
        await Assert.That(taken).IsEquivalentTo(["Dreamland"]);
        await Assert.That(owner.Text.ToString()).IsEqualTo("d");
    }

    [Test]
    public async Task Rows_shown_only_through_suggestion_rows_that_an_owner_fills_as_its_text_changes_ask_once_typing_pauses()
    {
        var server = new FakeServer();
        var source = server.Source();
        var text = new ObservableText();
        var rows = new SuggestionList(5);
        text.Changed += () => source.Suggest(text.Span, rows);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, width: 200))
            {
                ui.Input(text: text, id: "field");
                ui.SuggestionRows(rows: rows);
            }
        });
        driver.Focus(Suggests.Text(driver));

        driver.Type("d", settle: false);
        driver.Frame(0.1f);
        var early = server.Asks.Count;
        driver.Frame(0.3f);
        driver.Frame(0f);

        await Assert.That(early).IsEqualTo(0);
        await Assert.That(server.Queries).IsEquivalentTo(["d"]);
    }

    [Test]
    public async Task Rows_a_field_shows_wait_for_typing_to_pause_after_other_rows_showing_them_go()
    {
        var server = new FakeServer();
        var source = server.Source();
        var owner = new SuggestOwner { Rows = new SuggestionList(5) };
        owner.Text.Changed += () => source.Suggest(owner.Text.Span, owner.Rows);
        var echoed = true;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.SuggestionInput(text: owner.Text, rows: owner.Rows, width: 200, id: "field");
                if (echoed)
                    ui.SuggestionRows(rows: owner.Rows);
            }
        });
        echoed = false;
        driver.Rerender();
        driver.Focus(Suggests.Text(driver));

        driver.Type("d", settle: false);
        driver.Frame(0.1f);

        await Assert.That(server.Asks.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Unmounting_the_field_closes_rows_its_owner_keeps_and_tells_the_owner()
    {
        var text = new ObservableText();
        var rows = new SuggestionList(5);
        var dismissals = new List<int>();
        text.Changed += () =>
        {
            rows.BeginRows();
            rows.AddRow("Dreamland");
            rows.EndRows(final: true);
        };
        var shown = true;
        using var driver = UiDriver.Render(ui =>
        {
            if (shown)
                ui.SuggestionInput(
                    text: text,
                    rows: rows,
                    onDismiss: Callback.Of(dismissals, static d => d.Add(0)),
                    width: 200,
                    id: "field"
                );
        });
        driver.Focus(Suggests.Text(driver));
        driver.Type("d");
        var before = dismissals.Count;

        shown = false;
        driver.Rerender();

        await Assert.That(rows.IsOpen).IsFalse();
        await Assert.That(dismissals.Count - before).IsEqualTo(1);
    }

    [Test]
    public async Task Blanking_the_text_closes_rows_its_owner_keeps_and_tells_the_owner()
    {
        var text = new ObservableText();
        var rows = new SuggestionList(5);
        var dismissals = new List<int>();
        text.Changed += () =>
        {
            if (text.Span.IsEmpty)
                return;
            rows.BeginRows();
            rows.AddRow("Dreamland");
            rows.EndRows(final: true);
        };
        using var driver = UiDriver.Render(ui =>
            ui.SuggestionInput(
                text: text,
                rows: rows,
                onDismiss: Callback.Of(dismissals, static d => d.Add(0)),
                width: 200,
                id: "field"
            )
        );
        driver.Focus(Suggests.Text(driver));

        driver.Type("d");
        var shown = Suggests.Rows(driver).Count;
        var before = dismissals.Count;
        driver.Press(Key.BackSpace);

        await Assert.That(shown).IsEqualTo(1);
        await Assert.That(rows.IsOpen).IsFalse();
        await Assert.That(dismissals.Count - before).IsEqualTo(1);
    }

    [Test]
    public async Task An_answer_from_another_thread_writes_its_rows_on_the_views_and_a_failure_throws_there_only_for_the_text_still_shown()
    {
        var server = new FakeServer();
        var owner = new SuggestOwner { Source = server.Source() };
        using var driver = owner.Render();
        var view = Environment.CurrentManagedThreadId;
        driver.Focus(Suggests.Text(driver));

        driver.Type("d", settle: false);
        driver.Frame(0.3f);
        driver.Frame(0f);
        var answering = new Thread(() => server.Answer(0, "Dreamland"));
        answering.Start();
        answering.Join();
        var before = Suggests.Rows(driver).Count;
        driver.Frame(0f);
        var written = (server.Writer, Suggests.Rows(driver).Count);
        driver.Type("r", settle: false);
        driver.Frame(0.3f);
        driver.Frame(0f);
        driver.Type("x", settle: false);
        server.Fail(1, "dr");
        driver.Frame(0.3f);
        driver.Frame(0f);
        server.Fail(2, "drx");
        var thrown = Assert.Throws<InvalidOperationException>(() => driver.Frame(0f));

        await Assert.That(before).IsEqualTo(0);
        await Assert.That(written).IsEqualTo((view, 1));
        var failure = await Assert.That(thrown).IsNotNull();
        await Assert.That(failure.Message).IsEqualTo("drx");
    }

    [Test]
    public async Task Escape_drops_an_answer_on_its_way_and_the_ask_waiting_on_the_pause()
    {
        var server = new FakeServer();
        var owner = new SuggestOwner { Source = server.Source() };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));

        driver.Type("d", settle: false);
        driver.Frame(0.3f);
        driver.Frame(0f);
        driver.Press(Key.Escape, settle: false);
        server.Answer(0, "Dreamland");
        driver.Frame(0f);
        var dropped = Suggests.IsOpen(driver);
        driver.Type("r", settle: false);
        driver.Frame(0.3f);
        driver.Frame(0f);
        server.Answer(1, "Dreamland", "Drakkar");
        driver.Frame(0f);
        var shown = Suggests.Rows(driver).Count;
        driver.Type("a", settle: false);
        var taken = driver.KeyDown(Key.Escape, settle: false);
        driver.KeyUp(Key.Escape, settle: false);
        driver.Frame(0.5f);
        driver.Frame(0f);

        await Assert.That((dropped, shown, taken)).IsEqualTo((false, 2, true));
        await Assert.That(server.Queries).IsEquivalentTo(["d", "dr"]);
        await Assert.That(Suggests.IsOpen(driver)).IsFalse();
    }

    [Test]
    public async Task Typing_past_a_remote_answer_narrows_its_rows_in_any_case_behind_the_clients_own_keeping_a_groups_header()
    {
        var server = new FakeServer { Header = "Guilds" };
        var owner = new SuggestOwner
        {
            Source = server.Source(
                local: static (q, rows) =>
                {
                    if ("Drakkar".AsSpan().StartsWith(q, StringComparison.OrdinalIgnoreCase))
                        rows.AddRow("Drakkar");
                }
            ),
        };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));

        driver.Type("d", settle: false);
        driver.Frame(0f);
        var keystroke = Suggests.Rows(driver).ToList();
        driver.Frame(0.3f);
        driver.Frame(0f);
        server.Answer(0, "Dreamland", "Dragonfire", "Dragonfire Keep");
        driver.Frame(0f);
        var answered = Suggests.Rows(driver).ToList();
        driver.Type("RAGONFIRE", settle: false);
        driver.Frame(0f);
        var narrowed = Suggests.Rows(driver).ToList();
        var header = Suggests.Header(driver, 0);

        await Assert.That(keystroke).IsEquivalentTo(["Drakkar"]);
        await Assert
            .That(answered)
            .IsEquivalentTo(["Drakkar", "Dreamland", "Dragonfire", "Dragonfire Keep"]);
        await Assert.That(narrowed).IsEquivalentTo(["Dragonfire", "Dragonfire Keep"]);
        await Assert.That(header).IsEqualTo("Guilds");
        await Assert.That(server.Queries).IsEquivalentTo(["d"]);
    }

    [Test]
    public async Task A_fresh_answer_keeps_a_row_the_arrows_lit_a_default_pick_follows_it_and_a_refused_one_never_opens()
    {
        var server = new FakeServer();
        var owner = new SuggestOwner { Source = server.Source(), EmptyText = "No guilds" };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));

        void Answer(int ask, params string[] names)
        {
            driver.Frame(0.3f);
            driver.Frame(0f);
            server.Answer(ask, names);
            driver.Frame(0f);
        }

        driver.Type("dr", settle: false);
        Answer(0, "Dragonfire", "Drakkar");
        driver.Type("a", settle: false);
        driver.Press(Key.Down, settle: false);
        Answer(1, "Dragonfire", "Drakkar");
        var kept = Suggests.Lit(driver);
        driver.Press(Key.BackSpace, settle: false);
        Answer(2, "Drakkar", "Dragonfire");
        var followed = Suggests.Lit(driver);
        driver.Type("x", settle: false);
        driver.Frame(0.3f);
        driver.Frame(0f);
        server.Refuse(3);
        driver.Frame(0f);

        await Assert.That((kept, followed)).IsEqualTo(("Drakkar", "Drakkar"));
        await Assert.That(server.Queries).IsEquivalentTo(["dr", "dra", "dr", "drx"]);
        await Assert.That(Suggests.IsOpen(driver)).IsFalse();
    }

    [Test]
    public async Task Swapping_the_source_with_an_answer_on_its_way_closes_the_rows_and_drops_it()
    {
        var server = new FakeServer();
        var owner = new SuggestOwner { Source = server.Source() };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));
        driver.Type("d", settle: false);
        driver.Frame(0.3f);
        driver.Frame(0f);
        server.Answer(0, "Dreamland");
        driver.Frame(0f);
        driver.Type("r", settle: false);
        driver.Frame(0.3f);
        driver.Frame(0f);
        var open = Suggests.IsOpen(driver);

        owner.Source = Local([]);
        driver.Rerender();
        server.Answer(1, "Dreamland");
        driver.Frame(0f);

        await Assert.That(open).IsTrue();
        await Assert.That(Suggests.IsOpen(driver)).IsFalse();
    }

    sealed class GroupedNames : ISuggestionSource
    {
        public void Suggest(ReadOnlySpan<char> query, SuggestionList rows)
        {
            rows.BeginRows();
            rows.AddRow("Ashford", "Towns");
            rows.AddRow("Brightwater");
            rows.EndRows(final: true);
        }

        public void Cancel() { }
    }
}

internal sealed class FakeServer
{
    public readonly List<string> Queries = [];
    public readonly List<TaskCompletionSource<string[]>> Asks = [];
    public string Header = "";
    public int Writer;

    public ISuggestionSource Source(SuggestionWriter? local = null) =>
        new RemoteSuggestions<string[]>(
            query =>
            {
                Queries.Add(query);
                var reply = new TaskCompletionSource<string[]>();
                Asks.Add(reply);
                return new ValueTask<string[]>(reply.Task);
            },
            (reply, rows) =>
            {
                Writer = Environment.CurrentManagedThreadId;
                for (var i = 0; i < reply.Length; i++)
                    rows.AddRow(reply[i], i == 0 ? Header : "");
                return reply.Length > 0;
            },
            local
        );

    public void Answer(int ask, params string[] names) => Asks[ask].SetResult(names);

    public void Refuse(int ask) => Asks[ask].SetResult([]);

    public void Fail(int ask, string message) =>
        Asks[ask].SetException(new InvalidOperationException(message));
}

internal sealed class SuggestOwner
{
    public readonly ObservableText Text = new();
    public readonly List<string> Submits = [];
    public ISuggestionSource? Source;
    public SuggestionList? Rows;
    public Callback<SuggestionSlot> OnAccept;
    public TextValue EmptyText;
    public int? MaxSuggestions;
    public Slot Footer;
    public Slot<SuggestionSlot> Item;
    public Slot Trailing;
    public float Width = 200f;
    public float MinPopupWidth;
    public Length MaxPopupHeight;

    public UiDriver Render() =>
        UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 8))
            {
                using (ui.Box(width: 40, height: 20, focusable: true, id: "before")) { }
                ui.SuggestionInput(
                    text: Text,
                    source: Source,
                    rows: Rows,
                    onAccept: OnAccept,
                    emptyText: EmptyText,
                    maxSuggestions: MaxSuggestions,
                    footer: Footer,
                    item: Item,
                    trailing: Trailing,
                    minPopupWidth: MinPopupWidth,
                    maxPopupHeight: MaxPopupHeight,
                    onSubmit: Callback.Of(this, static o => o.Submits.Add(o.Text.ToString())),
                    width: Width,
                    id: "field"
                );
            }
        });
}

internal static class Suggests
{
    public static Node Text(UiDriver driver) =>
        Nodes.Find(driver.Get("field"), NodeKind.EditableText);

    static SuggestionRowProps Props(Node row) => row.Props<SuggestionRowProps>();

    public static List<string> Rows(UiDriver driver)
    {
        var rows = new List<string>();
        foreach (var row in driver.All<SuggestionRow>())
            rows.Add(Props(row).Text ?? "");
        return rows;
    }

    public static bool IsOpen(UiDriver driver) =>
        !Nodes.Find(driver.Get<SuggestionRows>(), NodeKind.Box).Place.Collapsed;

    public static Node Row(UiDriver driver, int index) =>
        Sliders.Kids(Nodes.Find(driver.All<SuggestionRow>()[index], NodeKind.Box))[^1];

    public static string? Lit(UiDriver driver)
    {
        foreach (var row in driver.All<SuggestionRow>())
        {
            if (Props(row).Lit)
                return Props(row).Text;
        }
        return null;
    }

    public static string? Header(UiDriver driver, int index) =>
        Props(driver.All<SuggestionRow>()[index]).Header;

    public static (int Anchor, int Caret) Selection(UiDriver driver)
    {
        if (Text(driver).Payload is not EditorPayload editor)
            throw new InvalidOperationException("The field holds no editor.");
        return (editor.Selection.Anchor, editor.Selection.Caret);
    }
}
