using System.Globalization;
using System.Numerics;
using System.Text;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Fonts.Tests;

public sealed class TextAllocationTests
{
    static readonly Color White = Color.FromRgb(255, 255, 255);
    static readonly Color Black = Color.FromRgb(0, 0, 0);
    static readonly Color Gold = Color.FromRgb(240, 200, 60);
    static readonly Color Grey = Color.FromRgb(160, 160, 170);

    [Test]
    public async Task ABlankGlyphAskedForWithANewStrokeEveryTime_RemembersOnlySoMany()
    {
        using var fonts = new TestFonts();
        using var atlas = new GlyphAtlas(new MemoryTextureFactory());
        var space = fonts.Inter.Face.GetGlyphIndex(new Rune(' '));
        var stroke = 1f;

        void Frame()
        {
            for (var i = 0; i < 256; i++)
            {
                stroke += 1f / 64f;
                atlas.TryGet(fonts.Inter, space, GlyphAtlas.EmSize, stroke, out _);
            }
        }

        // Every window has to grow a table with no cap past its capacity, or the last one reads zero.
        var worst = AllocationGate.WorstFrameBytes(Frame, windows: 3, warmup: 8);

        await Assert.That(worst).IsEqualTo(0L);
    }

    [Test]
    public async Task ACooldownReformattedEveryFrame_AllocatesNothingOnceWarm()
    {
        using var fonts = new TestFonts();
        using var atlas = new GlyphAtlas(new MemoryTextureFactory());
        var ramps = new GradientRampAtlas();
        var list = new DrawList(ramps);
        var label = new RenderCommandBuffer();
        var buffer = new TextBuffer(new TextStyle(fonts.Inter, 18f, White, 2f, Black));
        var layout = new TextLayout { Scale = 1.25f };
        var tenths = 99;

        void Frame()
        {
            tenths = tenths == 0 ? 99 : tenths - 1;
            Span<char> seconds = stackalloc char[8];
            (tenths / 10f).TryFormat(
                seconds,
                out var written,
                "0.0s",
                CultureInfo.InvariantCulture
            );
            buffer.SetText(seconds[..written]);
            layout.Update(buffer);
            layout.Draw(label.Open(), atlas, new Vector2(12f, 8f));

            ramps.BeginFrame();
            list.Begin(1280, 720);
            list.PushTransform(Matrix3x2.CreateScale(1.25f));
            list.Draw(label);
            list.Pop();
        }

        var worst = AllocationGate.WorstFrameBytes(Frame, warmup: 100);

        await Assert.That(list.QuadCount).IsGreaterThan(4);
        await Assert.That(worst).IsEqualTo(0L);
    }

    [Test]
    public async Task AChatLineAppendedEveryFrame_AllocatesNothingOnceWarm()
    {
        const int Lines = 8;
        string[] names = ["Aria", "Borin", "Cass"];
        string[] messages =
        [
            "on my way",
            "need heals at the bridge, two adds incoming from the left side",
            "你好，世界！",
            "pull in 3",
        ];

        using var fonts = new TestFonts();
        using var atlas = new GlyphAtlas(new MemoryTextureFactory());
        var ramps = new GradientRampAtlas();
        var list = new DrawList(ramps);
        var system = new TextStyle(fonts.Inter, 14f, Grey);
        var name = new TextStyle(fonts.Inter, 14f, Gold, 1f, Black);
        var body = new TextStyle(fonts.Inter, 14f, White);
        var buffers = new TextBuffer[Lines];
        var layouts = new TextLayout[Lines];
        var recorded = new RenderCommandBuffer[Lines];
        for (var i = 0; i < Lines; i++)
        {
            buffers[i] = new TextBuffer(body);
            layouts[i] = new TextLayout { MaxWidth = 280f, Wrapping = TextWrapping.Wrap };
            recorded[i] = new RenderCommandBuffer();
        }

        var head = 0;
        var tick = 0;

        void Frame()
        {
            tick++;
            var line = head;
            head = (head + 1) % Lines;

            var buffer = buffers[line];
            buffer.Clear();
            buffer.Append("[", system);
            Span<char> stamp = stackalloc char[5];
            (tick % 100000).TryFormat(stamp, out var written, "D5", CultureInfo.InvariantCulture);
            buffer.Append(stamp[..written], system);
            buffer.Append("] ", system);
            buffer.Append(names[tick % names.Length], name);
            buffer.Append(": ", system);
            buffer.Append(messages[tick % messages.Length], body);
            layouts[line].Update(buffer);
            layouts[line].Draw(recorded[line].Open(), atlas);

            for (var i = 0; i < Lines; i++)
            {
                if (recorded[i].IsStale)
                    layouts[i].Draw(recorded[i].Open(), atlas);
            }

            ramps.BeginFrame();
            list.Begin(1280, 720);
            var y = 0f;
            for (var k = 0; k < Lines; k++)
            {
                var index = (head + k) % Lines;
                list.PushTransform(Matrix3x2.CreateTranslation(16f, 400f + y));
                list.Draw(recorded[index]);
                list.Pop();
                y += layouts[index].Size.Height;
            }
        }

        var worst = AllocationGate.WorstFrameBytes(Frame, warmup: 200);

        await Assert.That(list.QuadCount).IsGreaterThan(100);
        await Assert.That(worst).IsEqualTo(0L);
    }

    [Test]
    public async Task LabelsThatOutgrowTheAtlasPages_StopStartingThemOver_AndAllocateNothing()
    {
        using var fonts = new TestFonts();
        using var atlas = new GlyphAtlas(new MemoryTextureFactory(), pageSize: 128, maxPages: 1);
        var ramps = new GradientRampAtlas();
        var list = new DrawList(ramps);
        var labels = new TextBuffer[2];
        var layouts = new TextLayout[2];
        var recorded = new RenderCommandBuffer[2];
        labels[0] = new TextBuffer(new TextStyle(fonts.Inter, 48f, White, 2f, Black));
        labels[0].SetText("Status");
        labels[1] = new TextBuffer(new TextStyle(fonts.Inter, 48f, Gold));
        labels[1].SetText("Mail 你好");
        for (var i = 0; i < 2; i++)
        {
            layouts[i] = new TextLayout();
            layouts[i].Update(labels[i]);
            recorded[i] = new RenderCommandBuffer();
            layouts[i].Draw(recorded[i].Open(), atlas);
        }

        void Frame()
        {
            atlas.BeginFrame();
            for (var i = 0; i < 2; i++)
            {
                if (recorded[i].IsStale)
                    layouts[i].Draw(recorded[i].Open(), atlas);
            }

            ramps.BeginFrame();
            list.Begin(1280, 720);
            list.Draw(recorded[0]);
            list.Draw(recorded[1]);
        }

        for (var i = 0; i < 20; i++)
            Frame();
        var resets = atlas.ResetCount;
        var worst = AllocationGate.WorstFrameBytes(Frame);

        await Assert.That(atlas.ResetCount).IsEqualTo(resets);
        await Assert.That(worst).IsEqualTo(0L);
    }
}
