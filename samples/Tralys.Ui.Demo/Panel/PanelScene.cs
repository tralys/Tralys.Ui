using System.Numerics;
using Tralys.Ui.Bgfx;
using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Demo.Panel;

sealed class PanelScene : IScene
{
    public const int PanelWidth = 480;
    public const int PanelHeight = 300;

    readonly BgfxViewRenderer _renderer;
    readonly BgfxRenderTarget _target;
    readonly PanelImage _image;

    public PanelScene(BgfxRenderDevice device, UiContext context)
    {
        _renderer = new BgfxViewRenderer(device);
        try
        {
            _target = new BgfxRenderTarget(PanelWidth, PanelHeight);
        }
        catch (InvalidOperationException e)
        {
            _renderer.Dispose();
            throw new DemoException(e.Message);
        }
        _image = new PanelImage(context, _target);
    }

    public UiView View => _image.View;

    public PanelModel Model => _image.Model;

    public BgfxViewRenderer Renderer => _renderer;

    public string Title(UiContext context) => "Tralys.Ui panel";

    public void Mount(UiView view) =>
        view.Mount(
            Slot.Of(_image, static (ui, image) => ui.PanelView(image: image, grow: 1, basis: 0))
        );

    [FramePath]
    public void Frame(UiView view, string renderer, in FrameTiming last) => _image.View.Update();

    [FramePath]
    public void Draw(ushort viewId, ushort firstLayerView, ushort layerViews) =>
        _renderer.RenderTo(_image.View, _target, viewId, firstLayerView, layerViews);

    public bool Shortcut(UiView view, Key key) => false;

    public void Dispose()
    {
        _image.Dispose();
        _renderer.Dispose();
        _target.Dispose();
    }
}

sealed partial class PanelModel : Mvvm.ObservableObject
{
    public PanelModel() => Value = 0.5f;

    [Mvvm.ObservableProperty]
    public partial int Presses { get; set; }

    [Mvvm.ObservableProperty]
    public partial float Value { get; set; }
}

[FramePath]
sealed partial class PanelImage : IViewHost, IDisposable
{
    public const float TurnDegrees = -8f;

    public static readonly Transform Turn = Transform.Rotate(TurnDegrees);

    public readonly Handler<PointerEvent> Down;
    public readonly Handler<PointerEvent> Move;
    public readonly Handler<PointerEvent> Up;
    public readonly Handler<PointerEvent> Cancel;
    public readonly Callback<bool> Hovered;
    readonly State _cursor = new();
    int _mouseButtons;

    public PanelImage(UiContext context, RenderTexture? texture)
    {
        Texture = texture;
        View = new UiView(context, this)
        {
            Size = new Size(PanelScene.PanelWidth, PanelScene.PanelHeight),
        };
        View.Mount(Slot.Of(Model, static (ui, model) => ui.PanelContent(model: model, grow: 1)));
        Down = Handler<PointerEvent>.Of(this, static (p, ref e) => p.OnDown(ref e));
        Move = Handler<PointerEvent>.Of(this, static (p, ref e) => p.OnMove(ref e));
        Up = Handler<PointerEvent>.Of(this, static (p, ref e) => p.OnUp(ref e));
        Cancel = Handler<PointerEvent>.Of(this, static (p, ref e) => p.OnCancel(ref e));
        Hovered = Callback<bool>.Of(this, static (p, over) => p.OnHover(over));
    }

    public UiView View { get; }

    public PanelModel Model { get; } = new();

    public RenderTexture? Texture { get; }

    public Cursor? Cursor => _cursor.Value;

    void OnDown(ref PointerEvent e)
    {
        if (e.Pointer.Kind == PointerKind.Mouse)
            _mouseButtons++;
        e.Capture();
        View.PointerDown(e.Pointer, ToPanel(e), e.Button, e.Modifiers, e.Time, e.ClickCount);
    }

    void OnMove(ref PointerEvent e)
    {
        if (e.Phase == PointerPhase.Move)
            View.PointerMove(e.Pointer, ToPanel(e), e.Buttons, e.IsInContact, e.Modifiers, e.Time);
    }

    void OnUp(ref PointerEvent e)
    {
        View.PointerUp(e.Pointer, ToPanel(e), e.Button, e.Modifiers, e.Time);
        if (e.Pointer.Kind == PointerKind.Mouse)
        {
            _mouseButtons = Math.Max(_mouseButtons - 1, 0);
            if (_mouseButtons > 0)
                return;
        }
        e.ReleaseCapture();
    }

    void OnCancel(ref PointerEvent e)
    {
        if (e.Pointer.Kind == PointerKind.Mouse)
        {
            if (_mouseButtons == 0)
                return;
            _mouseButtons = 0;
        }
        View.PointerCancel(e.Pointer);
    }

    void OnHover(bool over)
    {
        if (!over)
            View.PointerLeave(Pointer.Mouse);
    }

    static Vector2 ToPanel(in PointerEvent e) =>
        new(
            e.Position.X * PanelScene.PanelWidth / e.Size.Width,
            e.Position.Y * PanelScene.PanelHeight / e.Size.Height
        );

    public void SetCursor(Cursor? cursor) => _cursor.Value = cursor;

    public string? GetClipboardText() => null;

    public void SetClipboardText(ReadOnlySpan<char> text) { }

    public void StartTextInput(TextInputKind kind, Rect area, bool byContact) { }

    public void StopTextInput() { }

    public void Dispose() => View.Dispose();

    sealed partial class State : Mvvm.ObservableObject
    {
        [Mvvm.ObservableProperty]
        public partial Cursor? Value { get; set; }
    }
}

readonly record struct PanelViewProps
{
    public PanelImage? Image { get; init; }
}

sealed class PanelView : Component<PanelViewProps>
{
    protected override void Render(Ui ui, in PanelViewProps p)
    {
        var image = p.Image!;
        using (ui.Box(align: Align.Center, justify: Justify.Center))
        {
            using (
                ui.Box(
                    width: PanelScene.PanelWidth,
                    height: PanelScene.PanelHeight,
                    transform: PanelImage.Turn,
                    cursor: image.Cursor,
                    onPointerDown: image.Down,
                    onPointerMove: image.Move,
                    onPointerUp: image.Up,
                    onPointerCancel: image.Cancel,
                    onHoverChange: image.Hovered,
                    id: "panel-image"
                )
            )
            {
                if (image.Texture is { } texture)
                    ui.Image(source: texture, fit: ObjectFit.Fill, grow: 1);
            }
        }
    }
}

readonly record struct PanelContentProps
{
    public PanelModel? Model { get; init; }
}

sealed class PanelContent : Component<PanelContentProps>
{
    protected override void Render(Ui ui, in PanelContentProps p)
    {
        var model = p.Model!;
        var t = ui.Theme;
        var weight = t.Weight(WeightToken.Interface);
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 16,
                padding: 24,
                radius: 16,
                background: t.Color(ColorToken.BgBase)
            )
        )
        {
            ui.Text("A view in a texture", variant: TextVariant.Title, fontWeight: weight);
            ui.Text(
                model.Presses == 0 ? "Not pressed yet" : ui.Format($"Presses: {model.Presses}"),
                fontWeight: weight
            );
            using (ui.Box())
                ui.Button(
                    text: "Press me",
                    onClick: Callback.Of(model, static m => m.Presses++),
                    id: "press"
                );
            ui.Slider(value: model.Value, onValueChange: model.SetValue, max: 1f, id: "slider");
        }
    }
}
