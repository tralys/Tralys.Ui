using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ComponentAllocationTests
{
    static readonly TestTexture Art = new(48, 24);

    [Test]
    public async Task A_combobox_opened_lit_picked_and_faded_out_over_and_over_allocates_nothing()
    {
        using var driver = UiDriver.Render(ui =>
        {
            var value = ui.State<UiKey>("Apple");
            ui.Combobox(
                items: Fruits.Items,
                itemKey: Fruits.ByName,
                itemText: Fruits.Named,
                value: value.Value,
                onValueChange: value.Setter,
                width: 200
            );
        });
        driver.Focus(Nodes.Find(driver.Get<Combobox<string>>(), NodeKind.EditableText));

        var bytes = driver.WorstFrameBytes(static d =>
        {
            d.Press(Key.Down, settle: false);
            d.Frame();
            d.Press(Key.Down, settle: false);
            d.Press(Key.PageUp, settle: false);
            d.Press(Key.Down, settle: false);
            d.Press(Key.Enter, settle: false);
            d.Frame();
        });

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_filtered_combobox_whose_items_change_every_frame_allocates_nothing()
    {
        var names = new ObservableList<string>();
        foreach (var name in Fruits.Names)
            names.Add(name);
        using var driver = UiDriver.Render(ui =>
            ui.Combobox(
                items: ItemsSource.From(names),
                itemKey: Fruits.ByName,
                itemText: Fruits.Named,
                width: 200
            )
        );
        driver.Focus(Nodes.Find(driver.Get<Combobox<string>>(), NodeKind.EditableText));
        driver.Type("an");

        var bytes = driver.WorstFrameBytes(
            names,
            static (d, n) =>
            {
                n[1] = n[1] == "Banana" ? "Plantain" : "Banana";
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Images_changing_source_and_a_skin_swapped_every_frame_allocate_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(gap: 4))
                {
                    ui.Image(
                        model.Count % 2 == 0 ? "icon" : "missing",
                        fit: ObjectFit.Contain,
                        width: 40,
                        height: 40
                    );
                    ui.Image(
                        (model.Count % 3) switch
                        {
                            0 => "layer 0",
                            1 => "layer 1",
                            _ => "icon",
                        }
                    );
                    ui.Image(Art, slice: new NineSlice(new Thickness(8)), width: 60, height: 30);
                    ui.Switch(value: true);
                }
            },
            textures: new CountingTextures()
        );
        var swap = new SkinSwap(
            model,
            new Skin([new(SkinPart.ToggleTrackOn, new SkinPartImage(Art, new Thickness(8)))])
        );

        var bytes = driver.WorstFrameBytes(
            swap,
            static (d, s) =>
            {
                d.View.Context.Skin = d.View.Context.Skin is null ? s.Skin : null;
                s.Model.Count++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_sliced_image_resized_and_a_sprite_stepping_its_frames_every_frame_allocate_nothing()
    {
        var model = new Model();
        var textures = new Sheets();
        using var driver = UiDriver.Render(
            ui =>
            {
                var n = model.Count;
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 4))
                {
                    ui.Image(
                        "panel",
                        slice: new NineSlice(new Thickness(48), 0.5f, SliceFill.Tile, 0.5f),
                        width: 200 + n % 40,
                        height: 120 + n % 24,
                        id: "panel"
                    );
                    ui.Image(
                        "sheet",
                        fit: ObjectFit.Fill,
                        sourceRect: new Rect(n % 8 * 64, 0, 64, 64),
                        width: 96,
                        height: 96
                    );
                }
            },
            scale: 1.25f,
            textures: textures
        );
        var asked = textures.Asked;

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(textures.Asked).IsEqualTo(asked);
        var panel = driver.Get("panel").Bounds;
        await Assert.That(panel.Width).IsEqualTo(200f + model.Count % 40).Within(0.5f);
        await Assert.That(panel.Height).IsEqualTo(120f + model.Count % 24).Within(0.5f);
    }

    [Test]
    public async Task Inputs_taking_the_focus_in_turn_and_typed_into_allocate_nothing()
    {
        var a = new ObservableText("a");
        var b = new ObservableText("b");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, gap: 8))
            {
                ui.Input(text: a, id: "first", width: 200);
                ui.Input(text: b, invalid: true, id: "second", width: 200);
            }
        });
        var fields = new Fields(
            Nodes.Find(driver.Get("first"), NodeKind.EditableText),
            Nodes.Find(driver.Get("second"), NodeKind.EditableText)
        );

        var bytes = driver.WorstFrameBytes(
            fields,
            static (d, f) =>
            {
                d.Focus(f.First, settle: false);
                d.Type("x", settle: false);
                d.Focus(f.Second, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Presses_on_an_inputs_padding_allocate_nothing()
    {
        var hello = new ObservableText("hello");
        using var driver = UiDriver.Render(ui => ui.Input(text: hello, width: 200));
        var input = driver.Get<Input>();

        var bytes = driver.WorstFrameBytes(
            input,
            static (d, input) =>
            {
                d.Click(input, new Vector2(3, 3), settle: false);
                d.Click(input, new Vector2(197, 3), settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    sealed record SkinSwap(Model Model, Skin Skin);

    sealed record Fields(Node First, Node Second);

    sealed class Sheets : ITextureProvider
    {
        static readonly TestTexture Panel = new(128, 128);
        static readonly TestTexture Sheet = new(512, 320);

        public int Asked;

        public RenderTexture? Resolve(string source)
        {
            Asked++;
            return source == "sheet" ? Sheet : Panel;
        }
    }
}
