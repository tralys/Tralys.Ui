using System.Numerics;
using Tralys.Ui.Diagnostics;
using Tralys.Ui.Rendering;
using TUnit.Assertions.Enums;

namespace Tralys.Ui.Engine.Tests;

public sealed class DrawListTests
{
    static readonly Color White = Color.FromRgb(255, 255, 255);
    static readonly GradientStops Fade = new([new(0f, White), new(1f, Color.FromRgb(0, 0, 0))]);

    [Test]
    public async Task NestedClips_ClipToTheirIntersection()
    {
        var list = Flatten(dc =>
        {
            dc.PushClip(new Rect(0, 0, 100, 100));
            dc.PushClip(new Rect(50, 50, 100, 100));
            dc.DrawRectangle(White, new Rect(0, 0, 200, 200));
        });

        await Assert.That(list.Vertices[0].Clip).IsEqualTo(new Vector4(50, 50, 100, 100));
    }

    [Test]
    public async Task ABandCrossedMoreThanSixtyFourTimes_StillFillsToItsBottom()
    {
        var scribble = new PathBuilder();
        const int Strokes = 30;
        for (var k = 0; k < Strokes; k++)
        {
            var top = new Vector2(4 + 4 * (k * 13 % Strokes), 0);
            var bottom = new Vector2(4 + 4 * ((k * 29 + 5) % Strokes), 100);
            if (k == 0)
                scribble.MoveTo(top);
            else
                scribble.LineTo(top);
            scribble.LineTo(bottom);
        }
        scribble.Close();
        var list = Begin();

        list.PushClip(scribble);

        var lowest = 0f;
        foreach (var mask in list.Batches)
        {
            if (mask.Source < 0)
                continue;
            foreach (var vertex in list.Vertices.Slice(mask.FirstVertex, mask.VertexCount))
                lowest = MathF.Max(lowest, vertex.Position.Y);
        }
        await Assert.That(lowest).IsGreaterThan(99.99f);
    }

    [Test]
    public async Task AFillPastOneBatchsVertices_DrawsTheRestInTheNext()
    {
        var notched = new PathBuilder();
        const int Notches = 200;
        notched.MoveTo(new Vector2(0, 0));
        for (var i = 0; i < Notches; i++)
        {
            notched.LineTo(new Vector2(4 * i + 2, 1 + i * 37 % 199));
            notched.LineTo(new Vector2(4 * i + 4, 0));
        }
        notched.LineTo(new Vector2(4 * Notches, 250));
        notched.LineTo(new Vector2(0, 250));
        notched.Close();

        var list = Flatten(dc => dc.DrawPath(notched, White, default));

        await Assert.That(list.Batches.Length).IsGreaterThan(1);
        await Assert.That(CoverageAt(list, new Vector2(1, 240))).IsEqualTo(1f).Within(0.01f);
    }

    [Test]
    public async Task AStarUnderNonzero_FadesAtItsTips()
    {
        var star = new PathBuilder { FillRule = FillRule.Nonzero };
        star.MoveTo(new Vector2(50, 0));
        star.LineTo(new Vector2(79, 90));
        star.LineTo(new Vector2(2, 35));
        star.LineTo(new Vector2(98, 35));
        star.LineTo(new Vector2(21, 90));
        star.Close();

        var list = Flatten(dc => dc.DrawPath(star, White, default));

        var outward = Vector2.Normalize(new Vector2(90, -29));
        var beside = new Vector2(52.9f, 9f) + outward * 0.25f;
        await Assert.That(CoverageAt(list, beside)).IsBetween(0.1f, 0.4f);
    }

    [Test]
    public async Task OverlappingFiguresUnderNonzero_CoverWhereOneCrossesTheOtherOnce()
    {
        var pair = new PathBuilder { FillRule = FillRule.Nonzero };
        AddSquare(pair, new Vector2(0, 0), 40);
        AddSquare(pair, new Vector2(20, 20), 40);

        var list = Flatten(dc => dc.DrawPath(pair, White, default));

        await Assert.That(CoverageAt(list, new Vector2(40, 30))).IsEqualTo(1f).Within(0.01f);
        await Assert.That(CoverageAt(list, new Vector2(20, 30))).IsEqualTo(1f).Within(0.01f);
    }

    [Test]
    public async Task AFigureEndingOnItsStartWithoutClosing_FillsAsTheClosedFigure()
    {
        var open = new PathBuilder();
        AddSquare(open, new Vector2(10, 10), 100, close: false);
        open.LineTo(new Vector2(10, 10));
        var closed = new PathBuilder();
        AddSquare(closed, new Vector2(10, 10), 100);

        var opened = Flatten(dc => dc.DrawPath(open, White, default));
        var shut = Flatten(dc => dc.DrawPath(closed, White, default));

        foreach (var point in new[] { new Vector2(11, 9.4f), new Vector2(9.4f, 11) })
            await Assert
                .That(CoverageAt(opened, point))
                .IsEqualTo(CoverageAt(shut, point))
                .Within(0.01f);
    }

    [Test]
    public async Task AFillThinnerThanAPixel_CoversOnlyItsThickness()
    {
        var bar = new PathBuilder();
        bar.MoveTo(new Vector2(10, 50));
        bar.LineTo(new Vector2(110, 50));
        bar.LineTo(new Vector2(110, 50.2f));
        bar.LineTo(new Vector2(10, 50.2f));
        bar.Close();

        var list = Flatten(dc => dc.DrawPath(bar, White, default));

        var covered = 0f;
        for (var y = 47f; y < 53f; y += 0.01f)
            covered += CoverageAt(list, new Vector2(60, y)) * 0.01f;
        await Assert.That(covered).IsEqualTo(0.2f).Within(0.03f);
    }

    [Test]
    public async Task DotsAroundAClosedFigureWholePeriodsLong_DrawItsStartOnce()
    {
        var square = new PathBuilder();
        AddSquare(square, new Vector2(10, 10), 100);
        var dotted = new Pen(White, 1)
        {
            DashArray = new DashArray(0f, 2f),
            DashCap = PenLineCap.Round,
        };

        var list = Flatten(dc => dc.DrawPath(square, default, dotted));

        var beside = new Vector2(0.13f, 0.07f);
        var next = CoverageAt(list, new Vector2(12, 10) + beside);
        await Assert
            .That(CoverageAt(list, new Vector2(10, 10) + beside))
            .IsEqualTo(next)
            .Within(0.01f);
    }

    [Test]
    public async Task ASquareCapsCorner_IsDrawnInAClipPastTheMiterLimitsReach()
    {
        var line = new PathBuilder();
        line.MoveTo(new Vector2(10, 10));
        line.LineTo(new Vector2(50, 50));
        var capped = new Pen(
            White,
            20,
            PenLineJoin.Miter,
            PenLineCap.Square,
            PenLineCap.Square,
            MiterLimit: 1f
        );
        var list = Begin();

        list.PushClip(new Rect(62, 0, 66, 128));
        list.Draw(Record(dc => dc.DrawPath(line, default, capped)));

        await Assert.That(CoverageAt(list, new Vector2(63, 50.5f))).IsGreaterThan(0.5f);
    }

    [Test]
    public async Task AnImagesMipLevel_CountsTheShrinkOfItsTransform()
    {
        var buffer = new RenderCommandBuffer();
        buffer.Open().DrawImage(new TestTexture(64, 64), new Rect(0, 0, 64, 64));
        var list = Begin();

        list.PushTransform(Matrix3x2.CreateScale(0.25f));
        list.Draw(buffer);

        await Assert.That(list.Vertices[0].ImageSample.X).IsEqualTo(1.5f).Within(0.001f);
    }

    [Test]
    public async Task AnEdgeOfANineSliceSqueezedAlongItsLength_SamplesAtItsCornersLevel()
    {
        var list = Flatten(dc =>
            dc.DrawImage(
                new TestTexture(144, 144),
                new Rect(0, 0, 30, 100),
                new Rect(0, 0, 144, 144),
                new NineSlice(new Thickness(48), Scale: 0.25f)
            )
        );

        await Assert.That(list.QuadCount).IsEqualTo(9);
        await Assert
            .That(list.Vertices[1 * 4].ImageSample.X)
            .IsEqualTo(list.Vertices[0].ImageSample.X);
    }

    [Test]
    public async Task ABevelWhoseInnerCornerTurnsThroughItsPoint_CoversTheCornerOfItsFirstSide()
    {
        var turn = new PathBuilder();
        turn.MoveTo(new Vector2(8, 40));
        turn.LineTo(new Vector2(40, 40));
        turn.LineTo(
            new Vector2(40, 40)
                + new Vector2(-MathF.Sin(MathF.PI / 18), -MathF.Cos(MathF.PI / 18)) * 3
        );
        var list = Flatten(dc => dc.DrawPath(turn, default, new Pen(White, 12, PenLineJoin.Bevel)));

        var thinnest = float.MaxValue;
        for (var y = 34.6f; y < 44f; y += 0.05f)
        for (var x = 36f; x < 40f; x += 0.01f)
            thinnest = MathF.Min(thinnest, CoverageAt(list, new Vector2(x, y)));
        await Assert.That(thinnest).IsGreaterThanOrEqualTo(0.999f);
    }

    [Test]
    public async Task ImagesAndGlyphsInTurn_ShareOneDraw()
    {
        var image = new TestTexture(4, 4);
        var atlas = new TestTexture(4, 4);
        var glyph = new GlyphQuad(new Rect(0, 0, 4, 4), new Rect(0, 0, 4, 4));

        var list = Flatten(dc =>
        {
            dc.DrawImage(image, new Rect(0, 0, 4, 4));
            dc.DrawGlyphs(atlas, [glyph], White);
            dc.DrawImage(image, new Rect(8, 0, 4, 4));
            dc.DrawGlyphs(atlas, [glyph], White);
        });

        await Assert.That(list.Batches.Length).IsEqualTo(1);
        await Assert.That(list.Batches[0].Texture).IsSameReferenceAs(image);
        await Assert.That(list.Batches[0].GlyphTexture).IsSameReferenceAs(atlas);
    }

    [Test]
    public async Task Pop_RestoresTheTransformBeforeItsPush()
    {
        var list = Flatten(dc =>
        {
            dc.PushTransform(Matrix3x2.CreateTranslation(10, 0));
            dc.DrawRectangle(White, new Rect(0, 0, 4, 4));
            dc.Pop();
            dc.DrawRectangle(White, new Rect(0, 0, 4, 4));
        });

        await Assert.That(list.Vertices[0].Position).IsEqualTo(new Vector2(10, 0));
        await Assert.That(list.Vertices[4].Position).IsEqualTo(new Vector2(0, 0));
    }

    [Test]
    public async Task APushLeftOpenInABuffer_EndsWithThatBuffer()
    {
        var shifted = Record(dc =>
        {
            dc.PushTransform(Matrix3x2.CreateTranslation(10, 0));
            dc.DrawRectangle(White, new Rect(0, 0, 4, 4));
        });
        var sibling = Record(dc => dc.DrawRectangle(White, new Rect(0, 0, 4, 4)));
        var list = Begin();

        list.Draw(shifted);
        list.Draw(sibling);

        await Assert.That(list.Vertices[4].Position).IsEqualTo(new Vector2(0, 0));
    }

    [Test]
    public async Task Opacity_ScalesEveryPremultipliedChannel()
    {
        var list = Flatten(dc =>
        {
            dc.PushOpacity(0.5f);
            dc.DrawRectangle(White, new Rect(0, 0, 4, 4));
        });

        await Assert
            .That(list.Vertices[0].Fill)
            .IsEqualTo(0x80808080u)
            .Because("scaling alpha alone leaves premultiplied colour brighter than its alpha");
    }

    [Test]
    public async Task ALayerOfOnePrimitive_FoldsItsOpacityIntoThePrimitive()
    {
        var list = Begin();
        list.PushLayer(0.5f);
        list.Draw(Record(dc => dc.DrawRectangle(White, new Rect(0, 0, 4, 4))));
        list.Pop();

        await Assert.That(list.Layers[0].Pass).IsEqualTo(-1);
        await Assert.That(list.Batches[0].Target).IsEqualTo((short)-1);
        await Assert.That(list.Vertices[0].Fill).IsEqualTo(0x80808080u);
    }

    [Test]
    public async Task LayersThatNeedNoneOfEachOther_ShareAPass_SoAFrameTakesAsManyAsItsLayersNest()
    {
        var list = Begin();
        for (var i = 0; i < 6; i++)
        {
            var slot = new Rect(i * 20, 0, 12, 12);
            list.PushDropShadow(new DropShadow(White, ShadowDepth: 2, BlurRadius: 3));
            list.Draw(Record(dc => dc.DrawRectangle(White, slot)));
            list.Pop();
        }
        list.PushLayer(0.5f);
        list.Draw(Record(dc => dc.DrawRectangle(White, new Rect(0, 40, 12, 12))));
        list.PushDropShadow(new DropShadow(White, ShadowDepth: 2, BlurRadius: 3));
        list.Draw(Record(dc => dc.DrawRectangle(White, new Rect(20, 40, 12, 12))));
        list.Pop();
        list.Pop();

        var passes = new int[list.Layers.Length];
        for (var i = 0; i < passes.Length; i++)
            passes[i] = list.Layers[i].Pass;
        await Assert.That(passes.Max()).IsEqualTo(2);
        await Assert.That(passes.Count(pass => pass == 0)).IsEqualTo(7);
    }

    [Test]
    public async Task ARelativeGradientOnACurve_SpansTheCurveNotItsControlPoints()
    {
        var arch = new PathBuilder();
        arch.MoveTo(new Vector2(0, 0));
        arch.QuadraticTo(new Vector2(50, 100), new Vector2(100, 0));
        arch.Close();
        var list = Flatten(dc =>
            dc.DrawPath(arch, Paint.Linear(Fade, Vector2.Zero, Vector2.UnitY), default)
        );

        await Assert.That(list.Vertices[0].FillGradient).IsEqualTo(new Vector4(0, 0, 0, 50));
    }

    [Test]
    public async Task AReusedList_KeepsItsRampRowsOnlyWhileNoOtherListHasTakenThem()
    {
        var ramps = new GradientRampAtlas(rows: 1);
        var mine = new GradientStops([new(0f, White), new(1f, Color.FromRgb(0, 0, 0))]);
        var theirs = new GradientStops([new(0f, Color.FromRgb(255, 0, 0)), new(1f, White)]);
        var reused = new DrawList(ramps);
        var other = new DrawList(ramps);
        var mineBox = Record(dc =>
            dc.DrawRectangle(Paint.Linear(mine, Vector2.Zero, Vector2.One), new Rect(0, 0, 4, 4))
        );
        var theirBox = Record(dc =>
            dc.DrawRectangle(Paint.Linear(theirs, Vector2.Zero, Vector2.One), new Rect(0, 0, 4, 4))
        );
        ramps.BeginFrame();
        reused.Begin(64, 64);
        reused.Draw(mineBox);

        ramps.BeginFrame();
        var kept = reused.RetainRamps();
        other.Begin(64, 64);
        other.Draw(theirBox);

        await Assert.That(kept).IsTrue();
        await Assert
            .That(other.Vertices[0].Params.Y)
            .IsEqualTo(UiPaint.Solid)
            .Because("the reused list's row was not free to take");

        ramps.BeginFrame();
        other.Begin(64, 64);
        other.Draw(theirBox);

        await Assert.That(reused.RetainRamps()).IsFalse();
    }

    [Test]
    public async Task TwoFramesDrawingOtherGradientsFromTheSameRowOfTheirOwnAtlases_Differ()
    {
        var other = new GradientStops([new(0f, Color.FromRgb(255, 0, 0)), new(1f, White)]);
        var drawn = new DrawList(new GradientRampAtlas());
        var rebuilt = new DrawList(new GradientRampAtlas());
        drawn.Ramps.BeginFrame();
        rebuilt.Ramps.BeginFrame();
        drawn.Begin(64, 64);
        drawn.Draw(
            Record(dc =>
                dc.DrawRectangle(
                    Paint.Linear(Fade, Vector2.Zero, Vector2.One),
                    new Rect(0, 0, 4, 4)
                )
            )
        );
        rebuilt.Begin(64, 64);
        rebuilt.Draw(
            Record(dc =>
                dc.DrawRectangle(
                    Paint.Linear(other, Vector2.Zero, Vector2.One),
                    new Rect(0, 0, 4, 4)
                )
            )
        );

        await Assert.That(DrawListDifference.Of(drawn, rebuilt)).IsNotNull();
    }

    [Test]
    public async Task ARelativeGradientOnAnIntersection_SpansTheIntersection()
    {
        var left = new PathBuilder();
        left.AddRectangle(new Rect(0, 0, 100, 100));
        var right = new PathBuilder();
        right.AddRectangle(new Rect(50, 0, 100, 100));
        left.Combine(right, GeometryCombineMode.Intersect);

        var list = Flatten(dc =>
            dc.DrawPath(left, Paint.Linear(Fade, Vector2.Zero, Vector2.UnitX), default)
        );

        await Assert.That(list.Vertices[0].FillGradient).IsEqualTo(new Vector4(50, 0, 100, 0));
    }

    [Test]
    public async Task Batches_BreakOnlyWhereTheTextureChanges()
    {
        var a = new TestTexture(8, 8);
        var b = new TestTexture(8, 8);
        var list = Flatten(dc =>
        {
            dc.DrawRectangle(White, new Rect(0, 0, 4, 4));
            dc.DrawImage(a, new Rect(0, 0, 4, 4));
            dc.DrawRectangle(White, new Rect(0, 0, 4, 4));
            dc.DrawImage(a, new Rect(0, 0, 4, 4));
            dc.DrawImage(b, new Rect(0, 0, 4, 4));
            dc.DrawRectangle(White, new Rect(0, 0, 4, 4));
        });

        await Assert
            .That(list.Batches.ToArray())
            .IsEquivalentTo(
                [new DrawBatch(a, 0, 16, 0, 24), new DrawBatch(b, 16, 8, 24, 12)],
                CollectionOrdering.Matching
            );
    }

    [Test]
    public async Task ABoxStrokedWithAGradient_TakesTheFullProgram()
    {
        var stops = new GradientStops([new(0f, White), new(1f, White)]);
        var list = Flatten(dc =>
            dc.DrawBox(
                new Rect(0, 0, 8, 8),
                default,
                new Thickness(2),
                White,
                Paint.Linear(stops, new Vector2(0, 0), new Vector2(1, 0))
            )
        );

        await Assert.That(list.Batches[0].Rare).IsTrue();
    }

    [Test]
    public async Task ABoxWhoseBandIsUnpainted_StillFillsInsideTheBand()
    {
        var list = Flatten(dc =>
            dc.DrawBox(new Rect(0, 0, 8, 8), default, new Thickness(2), White, default)
        );

        await Assert.That(list.Vertices[0].Inner).IsEqualTo(new Vector4(2, 2, 4, 4));
    }

    [Test]
    public async Task LayersOfOneArray_ShareABatch_EachQuadNamingItsLayer()
    {
        var icons = new TestTextureArray(16, 16, 4);
        var list = Flatten(dc =>
        {
            dc.DrawImage(new RenderTextureLayer(icons, 1), new Rect(0, 0, 4, 4));
            dc.DrawImage(new RenderTextureLayer(icons, 3), new Rect(0, 0, 4, 4));
        });

        await Assert
            .That(list.Batches.ToArray())
            .IsEquivalentTo([new DrawBatch(null, 0, 8, 0, 12) { ArrayTexture = icons }]);
        await Assert.That(list.Vertices[0].Shape.X).IsEqualTo(1f);
        await Assert.That(list.Vertices[4].Shape.X).IsEqualTo(3f);
    }

    [Test]
    public async Task AQuadItsTransformMovesIntoTheClip_IsKept()
    {
        var list = Flatten(dc =>
        {
            dc.PushTransform(Matrix3x2.CreateTranslation(60, 0));
            dc.DrawRectangle(White, new Rect(-50, 0, 10, 10));
        });

        await Assert.That(list.QuadCount).IsEqualTo(1);
    }

    [Test]
    public async Task ACounterClockwiseSweep_DrawsTheRingOfItsClockwiseMirror()
    {
        var counterClockwise = Flatten(dc =>
            dc.DrawArc(new Vector2(32, 32), 20, 4, 0f, -MathF.PI / 2, White)
        );
        var clockwise = Flatten(dc =>
            dc.DrawArc(new Vector2(32, 32), 20, 4, -MathF.PI / 2, MathF.PI / 2, White)
        );

        await Assert
            .That(counterClockwise.Vertices[0].Shape)
            .IsEqualTo(clockwise.Vertices[0].Shape);
        await Assert
            .That(counterClockwise.Vertices[0].Inner)
            .IsEqualTo(clockwise.Vertices[0].Inner);
    }

    [Test]
    public async Task ADiagonalLinesAbsoluteEllipticalGradient_KeepsTheEllipseOnLocalAxes()
    {
        var centre = new Vector2(32, 32);
        var radius = new Vector2(24, 6);
        var list = Flatten(dc =>
            dc.DrawLine(
                new Vector2(8, 8),
                new Vector2(56, 56),
                4,
                Paint.Radial(Fade, centre, radius, GradientSpace.Absolute)
            )
        );

        var worst = 0f;
        foreach (var vertex in list.Vertices)
        {
            var gradient = vertex.FillGradient;
            var local = new Vector2(vertex.Local.X, vertex.Local.Y);
            var drawn = (
                (local - new Vector2(gradient.X, gradient.Y)) / new Vector2(gradient.Z, gradient.W)
            ).Length();
            var wanted = ((vertex.Position - centre) / radius).Length();
            worst = MathF.Max(worst, MathF.Abs(drawn - wanted));
        }
        await Assert.That(list.Vertices.Length).IsGreaterThan(0);
        await Assert.That(worst).IsLessThan(1e-3f);
    }

    [Test]
    public async Task ALinesImagePaint_FillsTheLinesOwnBox()
    {
        var list = Flatten(dc =>
            dc.DrawLine(
                new Vector2(8, 8),
                new Vector2(56, 8),
                4,
                Paint.FromImage(new PaintImage(new TestTexture(8, 8)))
            )
        );

        await Assert.That(list.Vertices[0].Fill).IsNotEqualTo(0u);
        await Assert.That(list.Vertices[0].FillGradient.X).IsEqualTo(1f / 48f).Within(1e-5f);
        await Assert.That(list.Vertices[0].FillGradient.Y).IsEqualTo(1f / 4f).Within(1e-5f);
    }

    [Test]
    public async Task AFlatPathsImageStroke_FillsTheStrokesOwnBox()
    {
        var flat = new PathBuilder();
        flat.MoveTo(new Vector2(8, 8));
        flat.LineTo(new Vector2(56, 8));

        var list = Flatten(dc =>
            dc.DrawPath(
                flat,
                default,
                new Pen(Paint.FromImage(new PaintImage(new TestTexture(8, 8))), 4f)
            )
        );

        await Assert.That(list.Vertices[0].Fill).IsNotEqualTo(0u);
        await Assert.That(list.Vertices[0].FillGradient.Y).IsEqualTo(1f / 4f).Within(1e-5f);
    }

    [Test]
    public async Task AGlyphRunRecordedBeforeItsAtlasWasRepacked_DrawsNothingAndIsStale()
    {
        var atlas = new TestTexture(64, 64);
        var label = Record(dc =>
            dc.DrawGlyphs(
                atlas,
                [new GlyphQuad(new Rect(0, 0, 8, 12), new Rect(0, 0, 8, 12))],
                White
            )
        );
        await Assert.That(label.IsStale).IsFalse();

        atlas.Repack();
        var list = Begin();
        list.Draw(label);

        await Assert.That(list.QuadCount).IsEqualTo(0);
        await Assert.That(label.IsStale).IsTrue();
    }

    [Test]
    public async Task ANineSlice_KeepsEachCornerAtItsOwnInsetTimesTheScale()
    {
        var texture = new TestTexture(32, 32);
        var list = Flatten(dc =>
            dc.DrawImage(
                texture,
                new Rect(0, 0, 100, 60),
                new Rect(0, 0, 32, 32),
                new NineSlice(new Thickness(4, 6, 8, 10), Scale: 2f)
            )
        );

        var bottomRight = list.Vertices.Slice(8 * 4, 4).ToArray();
        await Assert.That(list.QuadCount).IsEqualTo(9);
        await Assert.That(bottomRight[0].Position).IsEqualTo(new Vector2(84, 40));
        await Assert.That(bottomRight[0].ImageWindow.Z).IsEqualTo(1f);
        await Assert.That(bottomRight[3].Bounds).IsEqualTo(new Vector4(0, 0, 100, 60));
        await Assert.That(bottomRight[0].Local).IsEqualTo(new Vector4(84, 40, 0.75f, 0.6875f));
    }

    [Test]
    public async Task NineSliceCornersWiderThanTheDestination_ShrinkToMeetInIt()
    {
        var texture = new TestTexture(32, 32);
        var list = Flatten(dc =>
            dc.DrawImage(
                texture,
                new Rect(0, 0, 12, 32),
                new Rect(0, 0, 32, 32),
                new NineSlice(new Thickness(4, 0, 8, 0), Scale: 2f)
            )
        );

        await Assert.That(list.QuadCount).IsEqualTo(2);
        await Assert.That(list.Vertices[1].Position.X).IsEqualTo(4f);
        await Assert.That(list.Vertices[4].Position.X).IsEqualTo(4f);
    }

    [Test]
    public async Task ATiledNineSlice_CutsItsLastTileShortInsteadOfSquashingIt()
    {
        var texture = new TestTexture(30, 10);
        var list = Flatten(dc =>
            dc.DrawImage(
                texture,
                new Rect(0, 0, 45, 10),
                new Rect(0, 0, 30, 10),
                new NineSlice(new Thickness(10, 0, 10, 0), Fill: SliceFill.Tile)
            )
        );

        var lastCentreTile = list.Vertices.Slice(3 * 4, 4).ToArray();
        await Assert.That(list.QuadCount).IsEqualTo(5);
        await Assert.That(lastCentreTile[1].Position.X).IsEqualTo(35f);
        await Assert.That(lastCentreTile[1].Local.Z).IsEqualTo(0.5f);
    }

    [Test]
    public async Task ATiledNineSliceShorterThanItsSlices_SquashesItsEdgesAsItsCorners()
    {
        var texture = new TestTexture(30, 30);
        var list = Flatten(dc =>
            dc.DrawImage(
                texture,
                new Rect(0, 0, 100, 15),
                new Rect(0, 0, 30, 30),
                new NineSlice(new Thickness(10), Fill: SliceFill.Tile)
            )
        );

        var corner = list.Vertices.Slice(0, 4).ToArray();
        var firstEdgeTile = list.Vertices.Slice(4, 4).ToArray();
        await Assert.That(firstEdgeTile[2].Position.Y).IsEqualTo(corner[2].Position.Y);
        await Assert.That(firstEdgeTile[2].Local.W).IsEqualTo(corner[2].Local.W);
    }

    static float CoverageAt(DrawList list, Vector2 point)
    {
        var sum = 0f;
        foreach (var batch in list.Batches)
        {
            var vertices = list.Vertices.Slice(batch.FirstVertex, batch.VertexCount);
            var indices = list.Indices.Slice(batch.FirstIndex, batch.IndexCount);
            for (var i = 0; i < indices.Length; i += 3)
            {
                var a = vertices[indices[i]];
                var b = vertices[indices[i + 1]];
                var c = vertices[indices[i + 2]];
                var low = Vector2.Min(a.Position, Vector2.Min(b.Position, c.Position));
                var high = Vector2.Max(a.Position, Vector2.Max(b.Position, c.Position));
                if (point.X < low.X || point.Y < low.Y || point.X > high.X || point.Y > high.Y)
                    continue;
                var area = Cross(b.Position - a.Position, c.Position - a.Position);
                if (MathF.Abs(area) < 1e-9f)
                    continue;
                var wa = Cross(b.Position - point, c.Position - point) / area;
                var wb = Cross(c.Position - point, a.Position - point) / area;
                var wc = 1f - wa - wb;
                if (wa >= 0f && wb >= 0f && wc >= 0f)
                    sum += wa * a.Inner.X + wb * b.Inner.X + wc * c.Inner.X;
            }
        }
        return sum;
    }

    static void AddSquare(PathBuilder path, Vector2 corner, float side, bool close = true)
    {
        path.MoveTo(corner);
        path.LineTo(corner + new Vector2(side, 0));
        path.LineTo(corner + new Vector2(side, side));
        path.LineTo(corner + new Vector2(0, side));
        if (close)
            path.Close();
    }

    static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    static DrawList Begin()
    {
        var ramps = new GradientRampAtlas();
        var list = new DrawList(ramps);
        ramps.BeginFrame();
        list.Begin(128, 128);
        return list;
    }

    static RenderCommandBuffer Record(Action<DrawingContext> draw)
    {
        var buffer = new RenderCommandBuffer();
        draw(buffer.Open());
        return buffer;
    }

    static DrawList Flatten(Action<DrawingContext> draw)
    {
        var list = Begin();
        list.Draw(Record(draw));
        return list;
    }
}
