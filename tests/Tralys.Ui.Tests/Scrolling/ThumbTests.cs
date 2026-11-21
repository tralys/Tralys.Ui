using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ThumbTests
{
    [Test]
    public async Task Each_move_reports_the_pointers_distance_from_its_press_from_where_the_thumb_was_last_laid_out()
    {
        var thumb = new Dragged(new Vector2(10f, 10f));
        using var driver = thumb.Render();
        var start = driver.Get("thumb").ViewBounds;

        var held = driver.Hold(driver.Get("thumb"), settle: false);
        held.MoveBy(new Vector2(10f, 5f), settle: false);
        held.MoveBy(new Vector2(10f, 5f), settle: false);
        held.MoveBy(Vector2.Zero, settle: false);
        held.Release();

        var end = driver.Get("thumb").ViewBounds;
        await Assert
            .That(new Vector2(end.X - start.X, end.Y - start.Y))
            .IsEqualTo(new Vector2(20f, 10f));
        await Assert.That(thumb.Log.ToString()).IsEqualTo("start 10,5 20,10 end");
    }

    [Test]
    public async Task Only_a_left_press_or_a_contact_drags_and_a_second_finger_meanwhile_reports_nothing()
    {
        var thumb = new Dragged(default);
        using var driver = thumb.Render();
        var node = driver.Get("thumb");

        var right = driver.Hold(node, button: MouseButton.Right);
        right.MoveBy(new Vector2(20f, 0f));
        right.Release();
        var ignored = thumb.Log.ToString();
        var first = driver.Hold(node, PointerKind.Touch);
        first.MoveBy(new Vector2(10f, 0f));
        var second = driver.Hold(node, PointerKind.Touch);
        second.MoveBy(new Vector2(0f, 10f));
        second.Release();
        first.Cancel();
        driver.Tap(node);

        await Assert.That(ignored).IsEmpty();
        await Assert.That(thumb.Log.ToString()).IsEqualTo("start 10,0 cancelled start end");
    }

    [Test]
    public async Task A_finger_on_a_thumb_in_an_area_that_pans_both_ways_drags_the_thumb_not_the_content()
    {
        var thumb = new Dragged(default);
        var scroll = new ScrollState();
        using var driver = UiDriver.Render(
            ui =>
            {
                scroll.Outer = ui.ScrollHandle();
                using (ui.ScrollArea(scroll: scroll.Outer, width: 100, height: 100))
                using (ui.Box(width: 400, height: 400))
                    thumb.Thumb(ui);
            },
            new Size(200, 200)
        );

        driver.Drag(driver.Get("thumb"), new Vector2(-40f, -30f), PointerKind.Touch);

        await Assert
            .That(thumb.Log.ToString())
            .IsEqualTo("start -10,-7.5 -20,-15 -30,-22.5 -40,-30 end");
        var outer = await Assert.That(scroll.Outer).IsNotNull();
        await Assert.That(outer.Offset).IsEqualTo(Vector2.Zero);
    }
}

/// <summary>A thumb placed at its laid-out position plus each change it reports, logging what it reports.</summary>
internal sealed class Dragged(Vector2 start)
{
    public readonly ValueLog<(string? Word, Vector2 By)> Log = new(static step =>
        step.Word ?? $"{step.By.X},{step.By.Y}"
    );
    public Vector2 LaidOut;

    public UiDriver Render() =>
        UiDriver.Render(ui =>
        {
            using (ui.Box(width: 300, height: 200))
                Thumb(ui);
        });

    [FramePath]
    public void Thumb(Ui ui, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        using var fragment = ui.Fragment(file: file, line: line);
        var at = ui.State(start);
        LaidOut = at.Value;
        using (
            ui.Thumb(
                onDragStart: Callback.Of(this, static d => d.Log.Add(("start", default))),
                onDragDelta: Callback<Vector2>.Of(this, at, static (d, at, by) => d.Moved(at, by)),
                onDragEnd: Callback<bool>.Of(
                    this,
                    static (d, cancelled) => d.Log.Add((cancelled ? "cancelled" : "end", default))
                ),
                width: 40,
                height: 40,
                margin: 5,
                position: Position.Absolute,
                left: at.Value.X,
                top: at.Value.Y,
                id: "thumb"
            )
        ) { }
    }

    void Moved(State<Vector2> at, Vector2 by)
    {
        Log.Add((null, by));
        at.Value = LaidOut + by;
    }
}
