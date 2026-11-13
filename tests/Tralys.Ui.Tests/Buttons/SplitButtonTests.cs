using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class SplitButtonTests
{
    static UiDriver Render(Opens opens, Callback onClick) =>
        UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                {
                    ui.Button(text: "Before", id: "before");
                    ui.SplitButton(
                        text: "Sell",
                        onClick: onClick,
                        menu: MenuButtonTests.Rows,
                        onOpenChange: opens.OnOpenChange,
                        id: "split"
                    );
                    ui.Button(text: "After", id: "after");
                }
            })
            .WithIcons();

    static (Node Primary, Node Chevron) Parts(UiDriver driver)
    {
        var parts = driver.All<SplitButtonPart>();
        return (parts[0], parts[1]);
    }

    [Test]
    public async Task Its_primary_part_runs_the_primary_action_and_its_chevron_toggles_the_menu_against_the_whole_button()
    {
        var log = new Log();
        var opens = new Opens();
        using var driver = Render(opens, Callback.Of(log, static l => l.Add("click")));
        var split = driver.Get("split");
        var (primary, chevron) = Parts(driver);

        driver.Click(primary);
        var ran = (log.ToString(), driver.View.Layers.Length);
        driver.Click(chevron);
        var layer = driver.View.Layers[0].ViewBounds;
        var opened = (
            driver.View.Layers.Length,
            layer.X == split.ViewBounds.X,
            layer.Y >= split.ViewBounds.Bottom,
            MenuButtonTests.Chevron(split)
        );
        driver.Click(chevron);

        await Assert.That(ran).IsEqualTo(("click", 0));
        await Assert.That(opened).IsEqualTo((1, true, true, Transform.Scale(1f, -1f)));
        await Assert
            .That((driver.View.Layers.Length, MenuButtonTests.Chevron(split)))
            .IsEqualTo((0, default(Transform)));
        await Assert.That(opens.Reports).IsEquivalentTo([true, false]);
        await Assert.That(log.ToString()).IsEqualTo("click");
    }

    [Test]
    public async Task It_is_one_tab_stop_with_one_ring_whose_enter_and_space_run_the_primary_action_and_whose_down_opens_the_menu()
    {
        var log = new Log();
        var opens = new Opens();
        using var driver = Render(opens, Callback.Of(log, static l => l.Add("click")));
        var split = driver.Get("split");

        driver.Click(split);
        var clickedRing = ButtonLooks.HasRing(split);
        log.Items.Clear();
        driver.Focus(driver.Get("before"));
        driver.Tab();
        var tabbed = (driver.Focused == split, ButtonLooks.HasRing(split));
        driver.Tab();
        var past = driver.Focused == driver.Get("after");
        driver.Tab(shift: true);
        driver.KeyDown(Key.Enter);
        driver.KeyUp(Key.Enter);
        driver.KeyDown(Key.Space);
        driver.KeyDown(Key.Space, repeat: true);
        driver.KeyUp(Key.Space);
        driver.Press(Key.Enter, ModifierKeys.Control);
        driver.Press(Key.Space, ModifierKeys.Shift);
        var keyed = log.ToString();
        driver.Press(Key.Down);
        var opened = (driver.View.Layers.Length, driver.Focused == Menus.Panel(driver));
        driver.Press(Key.Enter);

        await Assert.That(clickedRing).IsFalse();
        await Assert.That((tabbed, past)).IsEqualTo(((true, true), true));
        await Assert.That(keyed).IsEqualTo("click click");
        await Assert.That(opened).IsEqualTo((1, true));
        await Assert.That(log.ToString()).IsEqualTo("click click");
        await Assert.That(opens.Reports).IsEquivalentTo([true]);
    }

    [Test]
    public async Task A_command_that_cant_run_dims_the_primary_part_and_runs_nothing_while_the_chevron_still_opens()
    {
        var log = new Log();
        var opens = new Opens();
        using var driver = Render(opens, new Command(() => log.Add("command"), static () => false));
        var split = driver.Get("split");
        var (primary, chevron) = Parts(driver);
        var primaryRoot = Nodes.Find(primary, NodeKind.Box);

        driver.Click(primary);
        driver.Focus(split);
        driver.Press(Key.Enter);
        driver.Click(chevron);

        await Assert
            .That(
                (
                    ButtonLooks.IsDisabled(primaryRoot),
                    primaryRoot.Visual.Opacity,
                    split.Visual.Opacity
                )
            )
            .IsEqualTo((true, (float?)0.5f, (float?)null));
        await Assert.That(log.ToString()).IsEqualTo("");
        await Assert.That(opens.Reports).IsEquivalentTo([true]);
    }

    [Test]
    public async Task Its_parts_round_only_their_outer_corners_around_a_faint_divider_and_never_wear_the_skins_art()
    {
        using var driver = UiDriver.Render(ui => ui.SplitButton(text: "Sell", id: "split"));
        driver.View.Context.Skin = new Skin([
            new(
                SkinPart.ButtonPrimary,
                new SkinPartImage(new TestTexture(24, 24), new Thickness(8))
            ),
        ]);
        driver.Settle();
        var t = Themes.Dark;
        var r = t.Dimensions(Density.Pointer).Radius(RadiusToken.Md);
        var look = t.Colors(ColorScheme.Primary, ComponentVariant.Filled, ComponentState.Normal);
        var (primary, chevron) = Parts(driver);
        var primaryRoot = Nodes.Find(primary, NodeKind.Box);
        var chevronRoot = Nodes.Find(chevron, NodeKind.Box);
        Node? divider = null;
        foreach (var child in driver.Get("split").Children)
            divider ??= child.Kind == NodeKind.Box ? child : null;

        await Assert
            .That((primaryRoot.Visual.Radius, chevronRoot.Visual.Radius))
            .IsEqualTo(
                (
                    new CornerRadius(r.TopLeft, 0f, 0f, r.BottomLeft),
                    new CornerRadius(0f, r.TopRight, r.BottomRight, 0f)
                )
            );
        await Assert
            .That((primaryRoot.Layout.Border, chevronRoot.Layout.Border))
            .IsEqualTo((new Thickness(1f, 1f, 0f, 1f), new Thickness(0f, 1f, 1f, 1f)));
        await Assert
            .That((divider!.Bounds.Width, divider.Visual.Background))
            .IsEqualTo((1f, (Paint)look.Foreground.WithOpacity(0.25f)));
        await Assert.That(primaryRoot.Visual.Background).IsEqualTo((Paint)look.Background);
        await Assert.That(ButtonLooks.Images(driver.Get("split"))).IsEqualTo(0);
    }
}
