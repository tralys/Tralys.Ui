using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;
using static Tralys.Ui.Tests.ToggleLooks;

namespace Tralys.Ui.Tests;

public sealed class CollapsibleTests
{
    [Test]
    public async Task A_header_click_space_or_enter_reports_the_opposite_of_open_and_tab_reaches_the_content_only_while_open()
    {
        var choices = new Choices();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                using (
                    ui.Collapsible(
                        header: "Section",
                        open: choices.Open,
                        onOpenChange: choices.SetOpen,
                        id: "section"
                    )
                )
                    ui.Button(text: "Inside", id: "inside");
                ui.Button(text: "After", id: "after");
            }
        });
        var header = Header(driver.Get("section"));
        string Focused() => driver.Focused?.Id ?? (driver.Focused == header ? "header" : "none");

        driver.Focus(header);
        driver.Tab();
        var skipped = Focused();
        driver.Click(header);
        var opened = choices.Open;
        driver.Tab();
        var inside = Focused();
        driver.Tab(shift: true);
        driver.Press(Key.Space);
        var closed = !choices.Open;
        driver.Press(Key.Enter);

        await Assert
            .That((skipped, opened, inside, closed))
            .IsEqualTo(("after", true, "inside", true));
        await Assert.That(choices.Open).IsTrue();
    }

    [Test]
    public async Task Its_content_sits_at_the_padding_its_caller_gives()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (
                ui.Collapsible(
                    header: "Parts",
                    open: true,
                    contentPadding: default(Thickness),
                    id: "section"
                )
            )
            using (ui.Box(width: 100, height: 20, id: "body")) { }
        });
        var section = driver.Get("section").ViewBounds;
        var body = driver.Get("body").ViewBounds;

        await Assert.That((body.X, section.Bottom - body.Bottom)).IsEqualTo((section.X, 0f));
    }

    [Test]
    public async Task Clicked_every_few_frames_it_turns_round_from_the_share_shown_with_its_arrow_turned_as_far_and_what_follows_sliding_through()
    {
        var log = new Log();
        var choices = new Choices();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, width: 200))
            {
                using (
                    ui.Collapsible(
                        header: "Tall",
                        open: choices.Open,
                        onOpenChange: Callback<bool>.Of(
                            log,
                            choices,
                            static (l, c, open) =>
                            {
                                l.Add(open ? "open" : "shut");
                                c.Open = open;
                            }
                        ),
                        id: "tall"
                    )
                )
                using (ui.Box(height: 120, id: "content")) { }
                using (ui.Box(width: 10, height: 10, id: "after")) { }
            }
        });
        var header = Header(driver.Get("tall"));
        var closedY = driver.Get("after").ViewBounds.Y;
        var openY = closedY + 136f;
        var between = 0;
        var mismatch = 0f;
        var jump = 0f;
        var last = 0f;

        for (var frame = 0; frame < 60; frame++)
        {
            if (frame % 5 == 0 && frame < 40)
                driver.Click(header, settle: false);
            if (frame == 22)
                driver.View.Context.Theme = Themes.Light;
            driver.Frame();
            var turn = Turn(driver.Get("tall"));
            var reveal = await Assert.That(driver.Get("content").Parent?.Parent).IsNotNull();
            var share = reveal.Place.Collapsed ? 0f : reveal.Bounds.Height / 136f;
            var y = driver.Get("after").ViewBounds.Y;
            if (y > closedY + 1f && y < openY - 1f)
                between++;
            mismatch = MathF.Max(mismatch, MathF.Abs(turn - share));
            jump = MathF.Max(jump, MathF.Abs(turn - last));
            last = turn;
        }

        await Assert.That(log.ToString()).IsEqualTo("open shut open shut open shut open shut");
        await Assert.That(between).IsGreaterThan(20);
        await Assert.That(mismatch).IsLessThan(0.02f);
        await Assert.That(jump).IsLessThan(0.3f);
    }

    [Test]
    public async Task One_mounted_open_stands_fully_open_on_its_first_frame_and_one_opened_after_turns_over_the_themes_duration()
    {
        var choices = new Choices();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (
                    ui.Collapsible(
                        header: "Section",
                        open: choices.Open,
                        id: "section",
                        key: choices.Count
                    )
                )
                using (ui.Box(height: 120)) { }
                using (ui.Box(width: 10, height: 10, id: "after")) { }
            }
        });
        var closed = driver.Get("after").ViewBounds.Y;
        var duration = Themes.Dark.Duration(DurationToken.Disclosure);

        choices.Open = true;
        choices.Count = 1;
        driver.Frame(0f);
        var mounted = (Turn(driver.Get("section")), driver.Get("after").ViewBounds.Y - closed);
        choices.Open = false;
        driver.Settle();
        choices.Open = true;
        driver.Frame(0f);
        driver.Frame((float)duration / 2f);
        var halfway = Turn(driver.Get("section"));
        driver.Frame((float)duration / 2f + 0.01f);

        await Assert.That(mounted).IsEqualTo((1f, 136f));
        await Assert.That(halfway).IsGreaterThan(0f).And.IsLessThan(1f);
        await Assert.That(Turn(driver.Get("section"))).IsEqualTo(1f).Within(0.001f);
    }

    [Test]
    public async Task A_header_lights_under_the_pointer_holds_while_pressed_rings_only_when_tabbed_to_and_greys_and_ignores_clicks_while_disabled()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                using (ui.Collapsible(header: "Section", id: "section"))
                    ui.Text("Inside");
                using (
                    ui.Collapsible(
                        header: "Locked",
                        disabled: true,
                        onOpenChange: Callback<bool>.Of(log, static (l, _) => l.Add("reported")),
                        id: "locked"
                    )
                )
                    ui.Text("Unreachable");
            }
        });
        var header = Header(driver.Get("section"));
        var dark = Themes.Dark;
        bool Arrow(ColorToken token) =>
            driver
                .Draw()
                .Lines.Any(l =>
                    l.Contains("/Canvas") && l.Contains($"stroke 1.17 {Hex(dark.Color(token))}")
                );

        var rest = (header.Visual.Background, Arrow(ColorToken.Icon));
        driver.Tab();
        var ringed = ButtonLooks.HasRing(header);
        driver.Hover(header);
        var lit = (header.Visual.Background, Arrow(ColorToken.IconHover), driver.Cursor);
        var held = driver.Hold(header);
        var pressed = (header.Visual.Background, ButtonLooks.HasRing(header));
        held.Release();
        var released = header.Visual.Background;
        driver.Unhover();
        driver.Click(Header(driver.Get("locked")));

        await Assert.That(rest).IsEqualTo((default(Paint), true));
        await Assert.That(ringed).IsTrue();
        await Assert.That(lit).IsEqualTo((Dark(ColorToken.StateHover), true, Cursors.Hand));
        await Assert.That(pressed).IsEqualTo((Dark(ColorToken.StatePressed), false));
        await Assert.That(released).IsEqualTo(Dark(ColorToken.StateHover));
        await Assert.That(header.Visual.Background).IsEqualTo(default(Paint));
        await Assert
            .That((driver.Get("locked").Inherit.Color, Arrow(ColorToken.IconDisabled)))
            .IsEqualTo((Dark(ColorToken.FgDisabled), true));
        await Assert.That(log.Items).IsEmpty();
    }

    [Test]
    public async Task Its_content_grows_on_the_side_it_opens_inset_past_the_arrow_or_all_round_clipped_rather_than_squashed()
    {
        var choices = new Choices { Open = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Stretch, gap: 16, width: 400))
                foreach (var direction in Directions)
                    using (
                        ui.Collapsible(
                            header: Names[(int)direction],
                            open: choices.Open,
                            direction: direction,
                            id: Names[(int)direction],
                            key: (long)direction
                        )
                    )
                    using (ui.Box(width: 60, height: 40, id: Names[(int)direction] + " content"))
                    { }
        });
        Rect HeaderOf(ExpandDirection d) => Header(driver.Get(Names[(int)d])).ViewBounds;
        Rect ContentOf(ExpandDirection d) => driver.Get(Names[(int)d] + " content").ViewBounds;
        // macOS's libm lands a half turn a few ulps short of 180°.
        float Angle(ExpandDirection d) => MathF.Round(Turn(driver.Get(Names[(int)d])) * 90f, 2);

        var down = (
            ContentOf(ExpandDirection.Down).X - HeaderOf(ExpandDirection.Down).X,
            ContentOf(ExpandDirection.Down).Y - HeaderOf(ExpandDirection.Down).Bottom
        );
        var up = (ContentOf(ExpandDirection.Up).Bottom, HeaderOf(ExpandDirection.Up).Y - 8f);
        var right = (
            ContentOf(ExpandDirection.Right).X - HeaderOf(ExpandDirection.Right).Right,
            ContentOf(ExpandDirection.Right).Y - HeaderOf(ExpandDirection.Right).Y
        );
        var left = (
            HeaderOf(ExpandDirection.Left).X - ContentOf(ExpandDirection.Left).Right,
            ContentOf(ExpandDirection.Left).Y - HeaderOf(ExpandDirection.Left).Y
        );
        var angles = (
            Angle(ExpandDirection.Down),
            Angle(ExpandDirection.Up),
            MathF.Abs(Angle(ExpandDirection.Right)),
            MathF.Abs(Angle(ExpandDirection.Left))
        );
        choices.Open = false;
        driver.Frame(0f);
        driver.Frame(0.08f);
        var partUp = (ContentOf(ExpandDirection.Up), HeaderOf(ExpandDirection.Up));
        var partLeft = (ContentOf(ExpandDirection.Left), HeaderOf(ExpandDirection.Left));

        await Assert.That(down).IsEqualTo((34f, 8f));
        await Assert.That(up.Item1).IsEqualTo(up.Item2);
        await Assert.That(right).IsEqualTo((8f, 8f));
        await Assert.That(left).IsEqualTo((8f, 8f));
        await Assert.That(angles).IsEqualTo((90f, -90f, 180f, 180f));
        await Assert
            .That((partUp.Item1.Height, partUp.Item1.Bottom))
            .IsEqualTo((40f, partUp.Item2.Y - 8f));
        await Assert
            .That((partLeft.Item1.Width, partLeft.Item1.Right))
            .IsEqualTo((60f, partLeft.Item2.X - 8f));
        await Assert
            .That(Has(driver.Root, static n => n.Layout.Overflow == Overflow.Clip))
            .IsTrue();
    }

    static readonly ExpandDirection[] Directions = Enum.GetValues<ExpandDirection>();

    static readonly string[] Names = Enum.GetNames<ExpandDirection>();

    static Node Header(Node collapsible)
    {
        if (Tick(collapsible).Parent?.Parent is not { } header)
            throw new InvalidOperationException("The tick sits in no header.");
        return header;
    }

    static float Turn(Node collapsible)
    {
        var m = Tick(collapsible).Visual.Transform.Matrix;
        return MathF.Atan2(m.M12, m.M11) * 180f / MathF.PI / 90f;
    }

    static string Hex(Color c) =>
        c.A == 255 ? $"#{c.R:x2}{c.G:x2}{c.B:x2}" : $"#{c.R:x2}{c.G:x2}{c.B:x2}{c.A:x2}";
}
