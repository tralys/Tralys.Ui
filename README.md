# Tralys.Ui

A UI library for .NET game clients, rendered through bgfx. Views are components written in C#: each renders its
nodes from its props and the observable models it reads, and renders again only when what it read changes. **A frame
allocates nothing once warm**, and analyzers keep it that way.

| Package | Role |
|---|---|
| `Tralys.Ui` | the core: components and the `Ui` they render through, layout (flex and grid), input and focus, text and editing, virtual lists, overlays, animation, theming and the built-in components; carries the generators and analyzers its consumers build with |
| `Tralys.Ui.Engine` | what the core and renderers build on: draw lists and tessellation, fonts and text layout, text editing, input contracts, items sources, popup placement, themes and their token compiler, `AllocationGate` |
| `Tralys.Ui.Bgfx` | draws through bgfx, its shaders compiled for SPIR-V, D3D11, Metal and WGSL and embedded |
| `Tralys.Ui.FreeType` | the FreeType font backend; Android's `libfreetype.so` included, desktop libraries from FreeTypeSharp |
| `Tralys.Ui.Mvvm` | `ObservableObject`, `[ObservableProperty]`, `[NotifyPropertyChangedFor]`, `[DelegateCommand]`, with their generators, so a view-model library needs no other package |
| `Tralys.Ui.Docking` | panels in tab groups that split, float and tear out into windows; layouts save and load |
| `Tralys.Ui.DevTools` | an in-app inspector: the node tree with components and their props, render counts and what each read, layout, picking and repaint flashing |
| `Tralys.Ui.Testing` | `UiDriver`, which mounts UI headless and clicks, types, drags and presses as a user would |

`Tralys.Ui.DevTools` and `Tralys.Ui.Testing` reach into the core, and the core into `Tralys.Ui.Engine`, so each needs exactly the
version of its own release, which its package pins.

Targets `net11.0`; the runtime packages are trim- and NativeAOT-compatible.

## Install

```sh
dotnet add package Tralys.Ui
dotnet add package Tralys.Ui.Bgfx
dotnet add package Tralys.Ui.FreeType
```

## Use

```csharp
sealed partial class CounterModel : ObservableObject
{
    [ObservableProperty]
    public partial int Count { get; set; }

    [DelegateCommand]
    void Increment() => Count++;
}

readonly record struct CounterProps
{
    public CounterModel? Model { get; init; }
}

sealed class Counter : Component<CounterProps>
{
    protected override void Render(Ui ui, in CounterProps p)
    {
        var model = p.Model!;
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            ui.Text(ui.Format($"{model.Count}"));
            ui.Button(text: "Add", onClick: model.IncrementCommand);
        }
    }
}
```

```csharp
var context = new UiContext(textServices);
var view = new UiView(context, host) { Size = new Size(1280, 720) };
view.Mount<Counter, CounterProps>(new CounterProps { Model = model });

// Each frame:
context.Advance(now);
view.Update();
device.BeginFrame();
renderer.Render(view, viewId);
```

- A component is a sealed class over a `readonly record struct` of props. The generators give it a call of its own,
  `ui.Counter(model: model)`, taking its props and the placement every node takes (`grow`, `margin`, `id`, ...) by
  name; the analyzers make arguments named, callbacks and slots `static`, loops keyed, and render paths allocation-free.
- A model property read while rendering is observed, so changing it re-renders only the components that read it. A
  component's own state is `ui.State`, and `ui.Remember`, `ui.Effect` and `ui.Loop` keep values, run effects and
  animate.
- The host is an `IViewHost` (cursor, clipboard, text input) and feeds the view input through `PointerMove`,
  `PointerDown`, `PointerUp`, `Wheel`, `KeyDown`, `KeyUp`, `TextInput`, `Gamepad` and `Stick`.
- `Themes.Dark` and `Themes.Light` ship in `Tralys.Ui.Engine`. For your own, add W3C design tokens: `*.tokens.json` files
  and one `*.resolver.json` as `AdditionalFiles`, and the build emits a `Themes` class holding a `Theme` per theme.
  Your tokens lay over Tralys.Ui's own, so they set only what they change: a theme context Tralys.Ui also has starts from
  Tralys.Ui's, any other from Tralys.Ui's default theme. Give it to the `UiContext`.
- Icons come from your icon font: add its `IconCodepoints.json` (or a `*.icons.json`) as an `AdditionalFiles` item
  and name its font in `<TralysIconFonts>lucide=lucide</TralysIconFonts>`, and the build emits an `Icons` class with a
  glyph per icon and `Icons.Set`, which draws Tralys.Ui's own icon roles once given to `UiContext.Icons`. A role the set
  leaves out is a build error.
- Text maps each character to its font's glyph and kerns the pairs, left to right: it applies no OpenType
  substitutions, bidirectional reordering or colour glyphs, so ligatures, scripts that join or reorder their letters,
  right-to-left text and emoji sequences don't render as they should. Editing still moves the caret and selects by
  whole user-perceived characters.
- `new Inspector(view, host)` inspects a `UiView` from a view of its own beside it.
- `UiDriver.Render(ui => ui.Counter(model: model))` mounts a component headless for a test, and `Click`, `Type`,
  `Press`, `Drag` and `Settle` drive it through real hit testing.
- Mark your own per-frame code `[FramePath]` and the analyzers reject what would allocate in it and in every member
  of your assembly it calls; mark `[ColdPath]` a member it calls that runs once for what it makes, such as a cache
  filled on first use.
- Each `ui` call tells nodes apart by its caller's source path, which the compiler writes into your assembly. Set
  `<TralysStripCallerPaths>true</TralysStripCallerPaths>` for the builds you ship, and every rooted `.cs` path in the
  assembly's string literals becomes `AssemblyName#N`, decoded by line N of the `AssemblyName.callerpaths.txt` the
  build writes to `obj`; the inspector can then no longer open the source. Tralys.Ui's own assemblies keep their
  paths, which start `/_/src/`.
- A renderer of your own reads a `DrawList`'s vertices, batches and layers, which stay `[Experimental]` as TRL5001
  while the vertex format may change: opt in with `<NoWarn>$(NoWarn);TRL5001</NoWarn>`.

[`tests/Tralys.Ui.PackageSmoke`](https://github.com/tralys/Tralys.Ui/tree/master/tests/Tralys.Ui.PackageSmoke) is a
complete headless consumer: fonts, renderer, a component bound to a model, a command, the inspector and a test
driver.

## Rendering to a texture

A `BgfxRenderTarget` works in both directions. To show what your game draws in the UI, such as a minimap or, with
`depth: true` for a depth buffer, a 3D preview, draw into its `FrameBuffer` and show it as an image:
`ui.Image(target)`. An image reads its texels as premultiplied.

To show a view on a surface in your game's world, draw the view into a target and sample `target.Color`:

```csharp
var target = new BgfxRenderTarget(512, 512, mips: true);
var renderer = new BgfxViewRenderer(device); // the BgfxRenderDevice every view's renderer shares
var view = new UiView(context, host) { Size = new Size(target.Width, target.Height) };
view.Mount<Panel, PanelProps>(props);

// Each frame, after device.BeginFrame() and on ids that sort before the view sampling the target:
view.Update();
if (renderer.RenderTo(view, target, (ushort)(nextView + LayerViews), nextView, LayerViews))
    nextView += LayerViews + 1;
```

- One `BgfxRenderDevice` holds what every view's renderer shares, such as gradients and layer targets. Make it once
  after bgfx starts, and call its `BeginFrame` once a frame before any view renders.
- `RenderTo` draws only when the view changed, and the target keeps its pixels otherwise, so an idle panel takes no
  draw calls and no view ids. Pass `force: true` when a texture the view draws has new pixels, such as a live scene.
- Texture coordinate (0, 0) is the view's top left. Texels are premultiplied and gamma-encoded: blend with one and
  inverse source alpha, and in a lit or tone-mapped pipeline convert them in your own shader.
- For input, turn your ray's hit into view pixels and call `PointerMove`, `PointerDown` and `PointerUp`. While a
  press you sent is held, keep sending moves when the ray leaves the panel; otherwise call `PointerLeave`. Call
  `PointerCancel` when a press ends without a release, such as a touch the system cancels. `WindowFocus` tells the
  view whether it has the keyboard.

## Demo

```sh
dotnet run --project samples/Tralys.Ui.Demo -c Release
```

A gallery of every component's stories, [also in the browser](https://vrimar.github.io/Tralys.Ui/) where WebGPU is
available; `?story=Switch.States` opens a story there. `--help` lists every option; F12 toggles the inspector.

## Contributing

Building, testing and releasing: [CLAUDE.md](https://github.com/tralys/Tralys.Ui/blob/master/CLAUDE.md).

## License

MIT.
