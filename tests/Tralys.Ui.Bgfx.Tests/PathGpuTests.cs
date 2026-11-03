using System.Numerics;
using Tralys.Ui.Rendering;
using static Tralys.Ui.Bgfx.Tests.TestColors;

namespace Tralys.Ui.Bgfx.Tests;

[NotInParallel(GpuDevice.Key)]
public sealed class PathGpuTests
{
    [Test]
    public async Task AFilledPath_FadesOverOnePixelCentredOnEachEdge_HolesIncluded()
    {
        using var gpu = new GpuDevice();
        var path = new PathBuilder();
        path.AddRectangle(Rect.FromEdges(8.5f, 8.5f, 55.5f, 55.5f));
        path.AddRectangle(Rect.FromEdges(24.5f, 24.5f, 39.5f, 39.5f));

        var frame = gpu.Render(64, 64, dc => dc.DrawPath(path, White, default));

        await Assert.That(frame[7, 32]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[8, 32].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(8);
        await Assert.That(frame[9, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[23, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[24, 32].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(8);
        await Assert.That(frame[25, 32]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task AFillBandOfAFewMillionthsOfAPixel_StillCoversTheRowItSnapsOnto()
    {
        using var gpu = new GpuDevice();
        var halfSnap = 20.5f + 1f / 512;
        var path = new PathBuilder();
        path.MoveTo(new Vector2(8, 4));
        path.LineTo(new Vector2(56, 4));
        path.LineTo(new Vector2(56, MathF.BitIncrement(halfSnap)));
        path.LineTo(new Vector2(56, 60));
        path.LineTo(new Vector2(8, 60));
        path.LineTo(new Vector2(8, MathF.BitDecrement(halfSnap)));
        path.Close();

        var frame = gpu.Render(64, 64, dc => dc.DrawPath(path, White, default));

        for (var x = 12; x < 52; x++)
            await Assert.That(frame[x, 20]).IsEqualTo(Pixel.Opaque(White));
    }

    [Test]
    public async Task AFillSlitToAPointAndBack_LeavesNoSeam()
    {
        using var gpu = new GpuDevice();
        var slit = new PathBuilder();
        slit.MoveTo(new Vector2(8, 8));
        slit.LineTo(new Vector2(56, 8));
        slit.LineTo(new Vector2(56, 56));
        slit.LineTo(new Vector2(32, 56));
        slit.LineTo(new Vector2(32, 16));
        slit.LineTo(new Vector2(32, 56));
        slit.LineTo(new Vector2(8, 56));
        slit.Close();

        var frame = gpu.Render(64, 64, dc => dc.DrawPath(slit, HalfWhite, default));

        var plain = frame[20, 40];
        var worst = 0;
        for (var y = 24; y < 52; y++)
        for (var x = 30; x < 34; x++)
            worst = Math.Max(worst, frame[x, y].DistanceTo(plain));
        await Assert.That(worst).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task NestedFiguresWoundAlike_LeaveAHoleOnlyUnderEvenOdd()
    {
        using var gpu = new GpuDevice();
        var evenOdd = new PathBuilder { FillRule = FillRule.EvenOdd };
        evenOdd.AddRectangle(new Rect(0, 0, 32, 32));
        evenOdd.AddRectangle(new Rect(8, 8, 16, 16));
        var nonzero = new PathBuilder { FillRule = FillRule.Nonzero };
        nonzero.AddRectangle(new Rect(32, 0, 32, 32));
        nonzero.AddRectangle(new Rect(40, 8, 16, 16));

        var frame = gpu.Render(
            64,
            32,
            dc =>
            {
                dc.DrawPath(evenOdd, White, default);
                dc.DrawPath(nonzero, White, default);
            }
        );

        await Assert.That(frame[16, 16]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[48, 16]).IsEqualTo(Pixel.Opaque(White));
    }

    [Test]
    public async Task AStroke_IsItsThicknessWide_AndAFlatCapEndsAtTheEndPoint()
    {
        using var gpu = new GpuDevice();
        var line = new PathBuilder();
        line.MoveTo(new Vector2(8, 32));
        line.LineTo(new Vector2(56, 32));

        var frame = gpu.Render(64, 64, dc => dc.DrawPath(line, default, new Pen(White, 4)));

        await Assert.That(frame[32, 29]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[32, 30]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[32, 33]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[32, 34]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[55, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[56, 32]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task AMiterJoin_FillsTheOuterCorner_WhereABevelCutsIt()
    {
        using var gpu = new GpuDevice();
        var miter = Corner(new Vector2(8, 8));
        var bevel = Corner(new Vector2(72, 8));

        var frame = gpu.Render(
            128,
            64,
            dc =>
            {
                dc.DrawPath(miter, default, new Pen(White, 8, PenLineJoin.Miter));
                dc.DrawPath(bevel, default, new Pen(White, 8, PenLineJoin.Bevel));
            }
        );

        await Assert.That(frame[42, 5]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[107, 5]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[105, 7]).IsEqualTo(Pixel.Opaque(White));
    }

    [Test]
    public async Task ASharpBevel_FadesOverOnePixelAcrossItsCut()
    {
        using var gpu = new GpuDevice();
        var lean = new Vector2(MathF.Sin(MathF.PI / 12), MathF.Cos(MathF.PI / 12));
        var tip = new Vector2(32, 10.75f + 8 * lean.X);
        var vee = new PathBuilder();
        vee.MoveTo(tip + new Vector2(-lean.X, lean.Y) * 40);
        vee.LineTo(tip);
        vee.LineTo(tip + lean * 40);

        var frame = gpu.Render(
            64,
            64,
            dc => dc.DrawPath(vee, default, new Pen(White, 16, PenLineJoin.Bevel))
        );

        await Assert
            .That(frame[32, 10].DistanceTo(new Pixel(64, 64, 64, 255)))
            .IsLessThanOrEqualTo(8)
            .Because("its centre lies a quarter pixel outside the cut");
        await Assert.That(frame[32, 11]).IsEqualTo(Pixel.Opaque(White));
    }

    [Test]
    public async Task ATriangleCap_FadesOverOnePixelAcrossItsSides()
    {
        using var gpu = new GpuDevice();
        var line = new PathBuilder();
        line.MoveTo(new Vector2(8, 32.35355f));
        line.LineTo(new Vector2(40, 32.35355f));

        var frame = gpu.Render(
            64,
            64,
            dc => dc.DrawPath(line, default, new Pen(White, 16, EndLineCap: PenLineCap.Triangle))
        );

        await Assert
            .That(frame[44, 28].DistanceTo(new Pixel(64, 64, 64, 255)))
            .IsLessThanOrEqualTo(8)
            .Because("its centre lies a quarter pixel outside the cap's upper side");
    }

    [Test]
    public async Task AClosedFigureTurningBackAtItsStart_FadesAlongBothEdges()
    {
        using var gpu = new GpuDevice();
        var flat = Segment(32);
        flat.Close();

        var frame = gpu.Render(64, 64, dc => dc.DrawPath(flat, default, new Pen(White, 5)));

        await Assert.That(frame[32, 29].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(8);
        await Assert.That(frame[32, 34].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(8);
    }

    [Test]
    public async Task AStrokeTurningAlmostStraightBack_CoversItsCoreEvenly()
    {
        using var gpu = new GpuDevice();
        var path = Segment(32);
        path.LineTo(new Vector2(8, 33));

        var frame = gpu.Render(64, 64, dc => dc.DrawPath(path, default, new Pen(HalfWhite, 8)));

        var first = frame[12, 30];
        var worst = 0;
        for (var y = 30; y <= 33; y++)
        for (var x = 12; x <= 52; x++)
            worst = Math.Max(worst, frame[x, y].DistanceTo(first));
        await Assert.That(worst).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task ARoundJoinTurningStraightBack_RoundsPastTheTurn()
    {
        using var gpu = new GpuDevice();
        var path = new PathBuilder();
        path.MoveTo(new Vector2(56, 32));
        path.LineTo(new Vector2(16, 32));
        path.LineTo(new Vector2(56, 32));

        var frame = gpu.Render(
            64,
            64,
            dc => dc.DrawPath(path, default, new Pen(White, 8, PenLineJoin.Round))
        );

        await Assert.That(frame[13, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[11, 32]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task Dashes_AreMeasuredInThicknesses_FromTheirOffsetIntoThePattern()
    {
        using var gpu = new GpuDevice();
        var dashes = new DashArray(2, 1);
        var top = Segment(16);
        var bottom = Segment(48);

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.DrawPath(top, default, new Pen(White, 4) { DashArray = dashes });
                dc.DrawPath(
                    bottom,
                    default,
                    new Pen(White, 4) { DashArray = dashes, DashOffset = 1 }
                );
            }
        );

        await Assert.That(frame[12, 16]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[18, 16]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[22, 16]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[14, 48]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[18, 48]).IsEqualTo(Pixel.Opaque(White));
    }

    [Test]
    public async Task ADashThroughAClosedFiguresStart_JoinsAcrossIt()
    {
        using var gpu = new GpuDevice();
        var square = new PathBuilder();
        square.AddRectangle(Rect.FromEdges(8, 8, 56, 56));

        var frame = gpu.Render(
            64,
            64,
            dc =>
                dc.DrawPath(
                    square,
                    default,
                    new Pen(White, 4) { DashArray = new DashArray(4, 2), DashOffset = 2 }
                )
        );

        await Assert
            .That(frame[7, 7])
            .IsEqualTo(Pixel.Opaque(White))
            .Because("the corner is mitred");
        await Assert.That(frame[20, 8]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task AnExcludedPath_KeepsOnlyTheFirstOperandOutsideTheSecond()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            64,
            64,
            dc =>
                dc.DrawPath(
                    Combined(
                        GeometryCombineMode.Exclude,
                        Rect.FromEdges(8, 8, 40, 56),
                        Rect.FromEdges(24, 8, 56, 56)
                    ),
                    White,
                    default
                )
        );

        await Assert.That(frame[16, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[32, 32]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[48, 32]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task ACombinedPath_FadesOverOnePixelCentredOnEachEdgeOfTheCombination()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            64,
            64,
            dc =>
                dc.DrawPath(
                    Combined(
                        GeometryCombineMode.Exclude,
                        Rect.FromEdges(8.5f, 8.5f, 55.5f, 55.5f),
                        Rect.FromEdges(24.5f, 24.5f, 39.5f, 39.5f)
                    ),
                    White,
                    default
                )
        );

        await Assert.That(frame[7, 32]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[8, 32].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(8);
        await Assert.That(frame[9, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[23, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[24, 32].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(8);
        await Assert.That(frame[25, 32]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task ACombinedStroke_OutlinesOnlyWhereTheCombinationMeetsItsOutside()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            64,
            64,
            dc =>
                dc.DrawPath(
                    Combined(
                        GeometryCombineMode.Union,
                        Rect.FromEdges(8, 8, 40, 40),
                        Rect.FromEdges(24, 24, 56, 56)
                    ),
                    default,
                    new Pen(White, 2)
                )
        );

        await Assert.That(frame[39, 16]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[16, 39]).IsEqualTo(Pixel.Opaque(White));
        await Assert
            .That(frame[39, 32])
            .IsEqualTo(Pixel.Clear)
            .Because("it lies inside the second");
        await Assert.That(frame[24, 32]).IsEqualTo(Pixel.Clear).Because("it lies inside the first");
    }

    static PathBuilder Combined(GeometryCombineMode mode, Rect first, Rect second)
    {
        var a = new PathBuilder();
        a.AddRectangle(first);
        var b = new PathBuilder();
        b.AddRectangle(second);
        a.Combine(b, mode);
        return a;
    }

    static PathBuilder Segment(float y)
    {
        var path = new PathBuilder();
        path.MoveTo(new Vector2(8, y));
        path.LineTo(new Vector2(56, y));
        return path;
    }

    static PathBuilder Corner(Vector2 origin)
    {
        var path = new PathBuilder();
        path.MoveTo(origin);
        path.LineTo(origin + new Vector2(32, 0));
        path.LineTo(origin + new Vector2(32, 32));
        return path;
    }
}
