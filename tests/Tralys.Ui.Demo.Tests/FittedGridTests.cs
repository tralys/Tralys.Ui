using Tralys.Ui.Gallery;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Demo.Tests;

public sealed class FittedGridTests
{
    [Test]
    public async Task A_fitted_grid_takes_the_most_columns_up_to_its_child_count_whose_cells_between_their_gaps_hold_its_widest_child()
    {
        using var few = Fitted(100, 3, 20, 10);
        var gap = Gap(few);
        using var wide = Fitted(400, 4, 100 - gap / 2f, 10);

        await Assert
            .That(few.Get("1").ViewBounds.X)
            .IsEqualTo((100f - 2f * gap) / 3f + gap)
            .Within(0.5f);
        await Assert.That(wide.Get("3").ViewBounds.X).IsEqualTo(0f);
        await Assert.That(wide.Get("3").ViewBounds.Y).IsEqualTo(10f + gap);
    }

    [Test]
    public async Task A_fitted_grid_down_to_one_column_makes_each_row_as_tall_as_its_own_tallest_child()
    {
        using var driver = Fitted(400, 3, 300, 48, tallSecond: true);

        await Assert.That(driver.Get("2").ViewBounds.Y).IsEqualTo(48f + 60f + 2f * Gap(driver));
    }

    static UiDriver Fitted(
        float width,
        int count,
        float childWidth,
        float childHeight,
        bool tallSecond = false
    ) =>
        UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (ui.FittedGrid(maxColumns: 4, width: width))
            {
                for (var i = 0; i < count; i++)
                {
                    using (
                        ui.Box(
                            width: childWidth,
                            height: tallSecond && i == 1 ? 60 : childHeight,
                            id: i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            key: i
                        )
                    ) { }
                }
            }
        });

    static float Gap(UiDriver driver) =>
        driver.View.Context.Theme.Dimensions(driver.View.Context.Density).Spacing;
}
