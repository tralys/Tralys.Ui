using System.Numerics;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class SizeClassTests
{
    [Test]
    public async Task A_view_opens_in_the_class_inside_its_safe_insets_and_its_first_update_renders_no_reader_again()
    {
        using var fonts = UiDriver.Render(static _ => { });
        using var context = new UiContext(fonts.View.Context.Text);
        using var view = new UiView(context, new NoHost())
        {
            Size = new Size(1000, 500),
            SafeInsets = new Thickness(0f, 30f, 0f, 0f),
        };
        var opened = view.SizeClass;
        var seen = new List<SizeClass>();

        view.Mount(
            Slot.Of(
                seen,
                static (ui, s) =>
                {
                    using (ui.Box(direction: Direction.Column))
                        ui.ClassReader(seen: s);
                }
            )
        );
        view.Update();

        await Assert.That(opened).IsEqualTo(SizeClass.Compact);
        await Assert.That(seen).IsEquivalentTo([SizeClass.Compact]);
    }

    [Test]
    public async Task The_class_holds_within_24_units_of_a_threshold_and_changes_past_it_either_way()
    {
        using var driver = Mount(null, 1000, 500);
        var classes = new List<SizeClass>();

        foreach (var height in (int[])[457, 450, 490, 510])
        {
            driver.View.Size = new Size(1000, height);
            driver.Settle();
            classes.Add(Bar(driver));
        }

        await Assert
            .That(classes)
            .IsEquivalentTo([
                SizeClass.Medium,
                SizeClass.Compact,
                SizeClass.Compact,
                SizeClass.Medium,
            ]);
    }

    [Test]
    public async Task A_change_renders_its_readers_before_that_updates_layout_and_waits_for_a_finger_to_lift()
    {
        var seen = new List<SizeClass>();
        using var driver = Mount(seen, 1000, 500);

        driver.View.Size = new Size(1000, 450);
        driver.Frame();
        var laidOut = Bar(driver);
        var finger = driver.Hold(new Vector2(500f, 300f), PointerKind.Touch);
        driver.View.Size = new Size(1000, 560);
        driver.Settle();
        var underFinger = Bar(driver);
        finger.Release();

        await Assert.That(laidOut).IsEqualTo(SizeClass.Compact);
        await Assert.That(underFinger).IsEqualTo(SizeClass.Compact);
        await Assert
            .That(seen)
            .IsEquivalentTo([SizeClass.Medium, SizeClass.Compact, SizeClass.Medium]);
        await Assert.That(Bar(driver)).IsEqualTo(SizeClass.Medium);
    }

    [Test]
    public async Task Clearing_the_override_classifies_afresh_rather_than_holding_the_forced_class()
    {
        using var driver = Mount(null, 970, 800);

        driver.View.SizeClassOverride = SizeClass.Medium;
        driver.Settle();
        var forced = Bar(driver);
        driver.View.SizeClassOverride = null;
        driver.Settle();

        await Assert.That(forced).IsEqualTo(SizeClass.Medium);
        await Assert.That(Bar(driver)).IsEqualTo(SizeClass.Expanded);
    }

    [Test]
    public async Task A_change_is_told_once_from_its_update_before_render_so_what_a_listener_writes_lays_out_in_it()
    {
        var model = new Model { Count = (int)SizeClass.Expanded };
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                using (ui.Box(width: 10 * (1 + model.Count), height: 10, id: "bar")) { }
            },
            size: new Size(1000, 800)
        );
        var told = new List<SizeClass>();
        driver.View.SizeClassChanged += (sender, _) =>
        {
            if (sender is not UiView view)
                throw new InvalidOperationException("A size class change came from no view.");
            told.Add(view.SizeClass);
            model.Count = (int)view.SizeClass;
        };

        driver.View.Size = new Size(1000, 450);
        driver.Frame();
        var laidOut = Bar(driver);
        driver.Settle();

        await Assert.That(laidOut).IsEqualTo(SizeClass.Compact);
        await Assert.That(told).IsEquivalentTo([SizeClass.Compact]);
    }

    internal static UiDriver Mount(List<SizeClass>? seen, int width, int height) =>
        UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                    ui.ClassReader(seen: seen, id: "bar");
            },
            size: new Size(width, height)
        );

    internal static SizeClass Bar(UiDriver driver) =>
        (SizeClass)(driver.Get("bar").Bounds.Width / 10f - 1f);

    sealed class NoHost : IViewHost
    {
        public void SetCursor(Cursor? cursor) { }

        public string? GetClipboardText() => null;

        public void SetClipboardText(ReadOnlySpan<char> text) { }

        public void StartTextInput(TextInputKind kind, Rect area, bool byContact) { }

        public void StopTextInput() { }
    }
}

public readonly record struct ClassReaderProps
{
    public List<SizeClass>? Seen { get; init; }
}

/// <summary>A bar 10 wide for each step of the view's size class, from 10 for compact; it logs each class it renders
/// with.</summary>
public sealed class ClassReader : Component<ClassReaderProps>
{
    protected override void Render(Ui ui, in ClassReaderProps p)
    {
        var sizeClass = ui.SizeClass;
        p.Seen?.Add(sizeClass);
        using (ui.Box(width: 10 * (1 + (int)sizeClass), height: 10)) { }
    }
}
