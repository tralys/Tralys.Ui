$input v_local, v_bounds, v_shape, v_innerBox, v_innerRadii, v_params, v_fill, v_stroke, v_window, v_safe, v_center

#include <bgfx_shader.sh>
#include "glyph.sh"

SAMPLER2D(s_texture, 0);
SAMPLER2DARRAY(s_textureArray, 2);
SAMPLER2D(s_glyph, 6);

float coverage(float _distance, float _aa)
{
    return clamp(0.5 - _distance / _aa, 0.0, 1.0);
}

// Corner radii run clockwise from the top left; y grows downward.
float sdRoundRect(vec2 _p, vec2 _half, vec4 _radii)
{
    vec2 side = _p.x > 0.0 ? _radii.yz : _radii.xw;
    float radius = min(_p.y > 0.0 ? side.y : side.x, min(_half.x, _half.y));
    vec2 q = abs(_p) - _half + vec2(radius, radius);
    return min(max(q.x, q.y), 0.0) + length(max(q, vec2(0.0, 0.0))) - radius;
}

float boxCoverage(vec2 _p, vec4 _box, vec4 _radii, float _aa)
{
    vec2 extent = _box.zw * 0.5;
    return coverage(sdRoundRect(_p - (_box.xy + extent), extent, _radii), _aa);
}

// Past both of its corner bands and half a filter from every edge: the box covers this pixel fully, on any shape.
bool fullyInside(vec2 _p, vec2 _center, vec4 _safe)
{
    vec2 from = abs(_p - _center);
    vec2 edge = from - _safe.xy;
    vec2 corner = from - _safe.zw;
    return max(edge.x, edge.y) <= 0.0 && min(corner.x, corner.y) <= 0.0;
}

void main()
{
    // Derivatives are taken before any branch.
    vec2 texelsAcross = dFdx(v_local.zw) * v_shape.xy;
    vec2 texelsDown = dFdy(v_local.zw) * v_shape.xy;
    float kind = v_params.x;
    vec4 color;
    if (kind > 12.5)
    {
        color = v_fill;
    }
    else if (kind < 0.5)
    {
        vec2 p = v_local.xy;
        if (fullyInside(p, v_center, v_safe))
        {
            color = v_fill;
        }
        else
        {
            float aa = v_params.z;
            float outer = boxCoverage(p, v_bounds, v_shape, aa);
            // An inner box of no area would still cover half of a pixel centred on it.
            float inner = v_innerBox.z > 0.0 && v_innerBox.w > 0.0 ? min(outer, boxCoverage(p, v_innerBox, v_innerRadii, aa)) : 0.0;
            color = v_fill * inner + v_stroke * (outer - inner);
        }
    }
    else if (kind < 4.5 && kind > 3.5)
    {
        // Half a texel over, the field's border reads only the cleared gap beside it, never the next glyph.
        vec2 halfTexel = 0.5 / v_shape.xy;
        float texel = texture2DLod(s_glyph, v_local.zw, 0.0).r;
        vec2 slope = vec2(
            texture2DLod(s_glyph, v_local.zw + vec2(halfTexel.x, 0.0), 0.0).r,
            texture2DLod(s_glyph, v_local.zw + vec2(0.0, halfTexel.y), 0.0).r) - texel;
        color = v_fill * glyphCoverage(texel, glyphTexelsPerPixel(slope, texelsAcross, texelsDown));
    }
    else
    {
        vec2 uv = clamp(v_local.zw, v_window.xy, v_window.zw);
        vec4 texel = kind < 3.5
            ? texture2DLod(s_texture, uv, v_params.w)
            : texture2DArrayLod(s_textureArray, vec3(uv, v_shape.x), v_params.w);
        color = (v_params.y < 0.5 ? vec4(texel.rgb * texel.a, texel.a) : texel) * v_fill;
        if (!fullyInside(v_local.xy, v_center, v_safe))
        {
            color *= boxCoverage(v_local.xy, v_innerBox, vec4(0.0, 0.0, 0.0, 0.0), v_params.z);
        }
    }
    gl_FragColor = color;
}
