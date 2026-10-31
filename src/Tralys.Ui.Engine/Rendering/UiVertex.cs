using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Tralys.Ui.Rendering;

/// <summary>One vertex of a draw-list quad or mesh. Every backend's vertex stream declares these fields in this order,
/// and its shaders read <see cref="Params"/> with the codes in <see cref="UiPrimitive"/> and <see cref="UiPaint"/>.</summary>
[Experimental("TRL5001")]
[StructLayout(LayoutKind.Sequential)]
public struct UiVertex
{
    /// <summary>Position in device pixels.</summary>
    public Vector2 Position;

    /// <summary>Position in the primitive's local space (xy) and texture coordinate (zw).</summary>
    public Vector4 Local;

    /// <summary>The primitive's local bounds: x, y, width, height; a shadow's caster; an image's outer edges, which it
    /// softens; a clip layer's shape.</summary>
    public Vector4 Bounds;

    /// <summary>A box's or clip layer's corner radii, clockwise from the top left (a shadow's are its caster's); an
    /// arc's radius, thickness, and the cosine and sine of the rotation that turns its bisector to +Y; an array image's
    /// layer in x; a glyph's atlas width and height in texels in xy.</summary>
    public Vector4 Shape;

    /// <summary>A box's border-inset bounds; a shadow's offset and spread box; an ellipse's stroke width in x; an
    /// arc's half-sweep sine and cosine, and 1 in z when it is a full ring; a mesh vertex's coverage in x.</summary>
    public Vector4 Inner;

    /// <summary>A box's border-inset corner radii; a shadow box's corner radii.</summary>
    public Vector4 InnerRadii;

    /// <summary>A linear gradient's start and end, or a radial gradient's centre and radii, in local space; an image
    /// fill's scale and offset from local space to texture space.</summary>
    public Vector4 FillGradient;

    /// <summary>The stroke's gradient, as <see cref="FillGradient"/>; a shadow's blur standard deviation in x.</summary>
    public Vector4 StrokeGradient;

    /// <summary>The <see cref="UiPrimitive"/> code; the fill paint code plus 4 times the stroke paint code, plus
    /// whichever of <see cref="UiPaint.PremultipliedTexture"/>, <see cref="UiPaint.FillImage"/> and
    /// <see cref="UiPaint.StrokeImage"/> apply; the fill ramp row's v, or an image fill's array layer, or outside a
    /// <see cref="DrawBatch.Rare"/> batch the width in local units that an edge softens across; the stroke ramp row's v,
    /// or an image stroke's array layer.</summary>
    public Vector4 Params;

    /// <summary>The device-space clip: left, top, right, bottom.</summary>
    public Vector4 Clip;

    /// <summary>Premultiplied RGBA8, red in the low byte; a gradient's or texture's opacity in every channel.</summary>
    public uint Fill;

    public uint Stroke;

    /// <summary>An image's window in texture coordinates, left, top, right, bottom: it paints nothing outside it.</summary>
    public Vector4 ImageWindow;

    /// <summary>The mip level an image samples in x; in yz, half a texel of the coarsest level that reads, in texture
    /// coordinates, which it samples that far inside <see cref="ImageWindow"/> so filtering stays in the window.</summary>
    public Vector3 ImageSample;
}

[Experimental("TRL5001")]
public static class UiPrimitive
{
    public const float Box = 0f;
    public const float Ellipse = 1f;
    public const float Arc = 2f;
    public const float Image = 3f;
    public const float Glyph = 4f;
    public const float BoxShadow = 5f;
    public const float InsetBoxShadow = 6f;

    /// <summary>An <see cref="Image"/> sampled from a layer of a <see cref="RenderTextureArray"/>.</summary>
    public const float ArrayImage = 7f;

    /// <summary>Triangles of a tessellated path, painted with the fill paint times the coverage in
    /// <see cref="UiVertex.Inner"/>'s x.</summary>
    public const float Mesh = 8f;

    /// <summary>A layer composited into its parent: the layer's texel at the layer pixel in
    /// <see cref="UiVertex.Local"/>'s zw, times the fill paint's alpha.</summary>
    public const float Layer = 9f;

    /// <summary>A layer's alpha blurred along <see cref="UiVertex.Shape"/>'s xy in layer pixels, by a Gaussian of
    /// standard deviation <see cref="UiVertex.Shape"/>'s z, painted with the fill colour. Each sample averages two
    /// taps <see cref="UiVertex.Shape"/>'s w layer pixels either side across the blur when w is above zero; with xy
    /// zero, the alpha is read unblurred.</summary>
    public const float Blur = 10f;

    /// <summary>A <see cref="Layer"/> composited through the coverage of the rounded box in
    /// <see cref="UiVertex.Bounds"/> and <see cref="UiVertex.Shape"/>.</summary>
    public const float ClipLayer = 11f;

    /// <summary>A <see cref="Layer"/> composited through the triangles of a tessellated path, by the coverage in
    /// <see cref="UiVertex.Inner"/>'s x.</summary>
    public const float MeshClipLayer = 12f;

    /// <summary>A rectangle whose edges lie on device pixels, in the fill colour alone: nothing about it needs
    /// filtering.</summary>
    public const float Rect = 13f;
}

[Experimental("TRL5001")]
public static class UiPaint
{
    public const float None = 0f;
    public const float Solid = 1f;
    public const float LinearGradient = 2f;
    public const float RadialGradient = 3f;
    public const float PremultipliedTexture = 16f;

    /// <summary>The fill, coded <see cref="Solid"/>, paints the batch's <see cref="DrawBatch.PaintTexture"/> at the
    /// texture coordinate <see cref="UiVertex.FillGradient"/> takes the local position to (xy a scale, zw an offset),
    /// inside <see cref="UiVertex.ImageWindow"/>, times its colour; the fill ramp's v is the array layer, or -1 for a
    /// plain texture. With <see cref="PremultipliedTexture"/>, the texels are already premultiplied.</summary>
    public const float FillImage = 32f;

    /// <summary>As <see cref="FillImage"/>, for the stroke, through <see cref="UiVertex.StrokeGradient"/> and the
    /// stroke ramp's v.</summary>
    public const float StrokeImage = 64f;
}

/// <summary>A run of triangles drawn with one texture per sampler: <see cref="IndexCount"/> indices from
/// <see cref="FirstIndex"/>, each counted from <see cref="FirstVertex"/>. A primitive joins the open batch unless that
/// batch holds a different texture for the sampler it reads. <see cref="Target"/> is the <see cref="DrawList.Layers"/> index it draws into, or -1 for the view;
/// <see cref="Source"/> is the layer whose texture it composites, or -1.</summary>
[Experimental("TRL5001")]
public readonly record struct DrawBatch(
    RenderTexture? Texture,
    int FirstVertex,
    int VertexCount,
    int FirstIndex,
    int IndexCount,
    short Target = -1,
    short Source = -1
)
{
    /// <summary>The texture the batch's image paints sample, or null; primitives without one join whichever batch is
    /// open.</summary>
    public RenderTexture? PaintTexture { get; init; }

    /// <summary>The array the batch's array images sample, or null.</summary>
    public RenderTextureArray? ArrayTexture { get; init; }

    /// <summary>The atlas page the batch's glyphs sample, or null.</summary>
    public RenderTexture? GlyphTexture { get; init; }

    /// <summary>Whether it needs the full program: shapes other than boxes, gradients, image paints, layers, or a clip
    /// that only the fragment can apply. A batch without it draws with a far cheaper one, so a backend must not swap
    /// them.</summary>
    public bool Rare { get; init; }
}

/// <summary>An offscreen target a frame draws into before compositing it: its bounds in its own pixels, its pass
/// (each pass renders before any pass after it and before the view), and the layer it composites into, or -1 for the
/// view. A layer's pass is one after the last pass of the layers it composites or filters, so layers that need none
/// of each other share a pass, and the passes a frame takes grow with how deeply its layers nest, not with how many
/// there are. A layer whose <see cref="Pass"/> is -1 was folded away or drew nothing.
/// A layer with an <see cref="Input"/> only filters that layer, as a blur pass does; without its own pass it draws
/// nothing, where a content layer without one draws straight into its parent. A content layer's pixels are the
/// device's; a filter layer's may be coarser, a whole number of device pixels each, with its bounds counted in
/// them.</summary>
[Experimental("TRL5001")]
public readonly record struct DrawLayer(
    int X,
    int Y,
    int Width,
    int Height,
    int Pass,
    int Parent,
    int Input = -1
);

/// <summary><see cref="Count"/> of <see cref="DrawList.Vertices"/> from <see cref="First"/>.</summary>
[Experimental("TRL5001")]
public readonly record struct VertexRange(int First, int Count);
