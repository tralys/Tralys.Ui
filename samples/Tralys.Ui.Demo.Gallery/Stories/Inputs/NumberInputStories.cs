using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<NumberInput>("Inputs")]
static class NumberInputStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 16))
        {
            ui.NumberInput(value: 42, width: 200);
            ui.NumberInput(value: 10, width: 200, disabled: true);
        }
    }

    [FramePath]
    public static void MinMax(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 16))
        {
            ui.NumberInput(value: 50, max: 100, width: 200);
            ui.NumberInput(value: 5, min: 1, max: 10, width: 200);
        }
    }

    [FramePath]
    public static void Stepper(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 16))
        {
            ui.NumberInput(
                value: 5,
                min: 1,
                max: 10,
                steppers: true,
                textAlign: TextAlign.Center,
                width: 200
            );
            ui.NumberInput(
                value: 0,
                mode: NumberMode.Decimal,
                max: 5,
                step: 0.5,
                decimalPlaces: 1,
                steppers: true,
                zeroIsEmpty: false,
                width: 200
            );
        }
    }

    [FramePath]
    public static void Decimal(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 16))
        {
            ui.NumberInput(value: 0.55, mode: NumberMode.Decimal, max: 1, width: 200);
            ui.NumberInput(
                value: 12.3456,
                mode: NumberMode.Decimal,
                min: -1000,
                max: 1000,
                decimalPlaces: 4,
                width: 200
            );
            ui.NumberInput(
                value: 1234,
                mode: NumberMode.Decimal,
                max: 5000,
                decimalPlaces: 0,
                width: 200
            );
        }
    }

    [FramePath]
    public static void Format(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 16))
        {
            ui.NumberInput(value: 3.14159, mode: NumberMode.Decimal, format: "F3", width: 200);
            ui.NumberInput(value: 0.42, mode: NumberMode.Decimal, format: "0.0%", width: 200);
            ui.NumberInput(value: 125.5, mode: NumberMode.Decimal, format: "0.00 'ms'", width: 200);
        }
    }

    [FramePath]
    public static void ZeroIsEmpty(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 16))
        {
            ui.NumberInput(value: 0, zeroIsEmpty: true, width: 200);
            ui.NumberInput(value: 0, zeroIsEmpty: false, width: 200);
        }
    }
}
