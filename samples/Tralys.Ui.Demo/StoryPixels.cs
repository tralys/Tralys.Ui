using Bgfx.Net;
using Tralys.Ui.Bgfx;
using Tralys.Ui.Gallery.Textures;
using Tralys.Ui.Theming;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Demo;

/// <summary>Draws stories on a renderer without a window, from the same <see cref="StoryStage"/> the layout capture
/// reads, over the theme's background, once every image it asked for has loaded, one pixel to a layout unit: as RGBA
/// rows for the goldens, or beside each layout <see cref="StoryCapture"/> writes, as
/// <c>&lt;Component&gt;.&lt;Name&gt;.tga</c>, 24-bit and top-down.</summary>
sealed class StoryPixels : IDisposable
{
    const ushort FirstLayerView = 0;
    const ushort ViewId = FirstLayerView + BgfxDevice.LayerViews;
    const ushort ReadBackView = ViewId + 1;

    readonly SdlWindow _window;
    readonly BgfxDevice _device;
    readonly BgfxViewRenderer _renderer;
    readonly OffscreenTarget _target;
    readonly BgfxTextureFactory _glyphs = new();
    readonly AssetTextures _images = new();
    readonly Action<UiView> _draw;
    uint _frameNumber;

    StoryPixels(SdlWindow window, BgfxDevice device)
    {
        _window = window;
        _device = device;
        _renderer = new BgfxViewRenderer(device.Rendering);
        _target = new OffscreenTarget(StoryStage.Width, StoryStage.Height);
        _draw = Present;
        Bx.SetViewFrameBuffer(ViewId, _target.FrameBuffer);
    }

    /// <summary>Writes each story's layout and frame under <paramref name="directory"/>, drawn on
    /// <paramref name="renderer"/> on the device <paramref name="vendor"/> and <paramref name="deviceId"/> pick.</summary>
    /// <returns>The process's exit code.</returns>
    /// <exception cref="DemoException">The renderer could not start.</exception>
    public static int Run(
        string directory,
        string? key,
        RendererType renderer,
        Theme theme,
        Density density,
        PciIdFlags vendor = PciIdFlags.None,
        ushort deviceId = 0
    )
    {
        using var pixels = Start(renderer, vendor, deviceId);
        return StoryCapture.Run(
            directory,
            key,
            (story, path) => pixels.Capture(story, path, theme, density)
        );
    }

    /// <summary>A renderer of stories on <paramref name="renderer"/>, on the device <paramref name="vendor"/> and
    /// <paramref name="deviceId"/> pick.</summary>
    /// <exception cref="DemoException">The renderer could not start.</exception>
    public static StoryPixels Start(
        RendererType renderer,
        PciIdFlags vendor = PciIdFlags.None,
        ushort deviceId = 0
    )
    {
        var window = SdlWindow.OpenHeadless(StoryStage.Width, StoryStage.Height);
        BgfxDevice? device = null;
        try
        {
            device = new BgfxDevice(
                window,
                renderer,
                headless: true,
                vsync: false,
                vendor,
                deviceId
            );
            return new StoryPixels(window, device);
        }
        catch
        {
            device?.Dispose();
            window.Dispose();
            throw;
        }
    }

    /// <summary>The frame <paramref name="story"/> settles to in <paramref name="theme"/>, as top-down RGBA rows
    /// <see cref="StoryStage.Width"/> pixels wide and <see cref="StoryStage.Height"/> tall, valid until the next
    /// draw.</summary>
    public ReadOnlySpan<byte> Draw(Story story, Theme theme)
    {
        using var stage = Settle(story, theme, Density.Pointer);
        ReadBack();
        return _target.Pixels;
    }

    void Capture(Story story, string path, Theme theme, Density density)
    {
        using var stage = Settle(story, theme, density);
        stage.Layout().Save(path + StoryCapture.LayoutExtension);
        if (_renderer.DroppedLayers > 0)
            Console.Error.WriteLine(
                $"{story.Key} drew {_renderer.DroppedLayers} layers without their effect, past its {BgfxDevice.LayerViews} layer views or out of room in a target."
            );

        ReadBack();
        _target.Save(path + ".tga");
    }

    StoryStage Settle(Story story, Theme theme, Density density)
    {
        BgfxDevice.ClearView(ViewId, theme.Color(ColorToken.Bg));
        var stage = new StoryStage(_glyphs, _images, _draw);
        stage.Context.Theme = theme;
        stage.Context.Density = density;
        stage.Settle(story);
        while (_images.IsLoading)
            stage.Frame();
        return stage;
    }

    void ReadBack()
    {
        var ready = _target.Read(ReadBackView);
        while (_frameNumber < ready)
            _frameNumber = Bx.Frame(0);
    }

    [FramePath]
    void Present(UiView view)
    {
        _images.Pump();
        _device.Rendering.BeginFrame();
        _renderer.Render(view, ViewId, FirstLayerView, BgfxDevice.LayerViews);
        _frameNumber = Bx.Frame(0);
    }

    public void Dispose()
    {
        _images.Dispose();
        _renderer.Dispose();
        _target.Dispose();
        _device.Dispose();
        _window.Dispose();
    }
}
