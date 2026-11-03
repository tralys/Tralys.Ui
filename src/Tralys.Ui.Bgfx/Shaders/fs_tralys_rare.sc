$input v_local, v_bounds, v_shape, v_inner, v_innerRadii, v_fillGradient, v_strokeGradient, v_params, v_clip, v_device, v_fill, v_stroke, v_window, v_sample

#include <bgfx_shader.sh>
#include "glyph.sh"

SAMPLER2D(s_texture, 0);
SAMPLER2D(s_ramp, 1);
SAMPLER2DARRAY(s_textureArray, 2);
SAMPLER2D(s_layer, 3);
SAMPLER2D(s_paint, 4);
SAMPLER2DARRAY(s_paintArray, 5);
SAMPLER2D(s_glyph, 6);
uniform vec4 u_layer;
uniform vec4 u_layerRegion;

float coverage(float _distance, float _aa)
{
    return clamp(0.5 - _distance / _aa, 0.0, 1.0);
}

// Corner radii run clockwise from the top left; y grows downward.
float cornerRadius(vec2 _p, vec2 _half, vec4 _radii)
{
    vec2 side = _p.x > 0.0 ? _radii.yz : _radii.xw;
    return min(_p.y > 0.0 ? side.y : side.x, min(_half.x, _half.y));
}

float sdRoundRect(vec2 _p, vec2 _half, vec4 _radii)
{
    float radius = cornerRadius(_p, _half, _radii);
    vec2 q = abs(_p) - _half + vec2(radius, radius);
    return min(max(q.x, q.y), 0.0) + length(max(q, vec2(0.0, 0.0))) - radius;
}

float boxCoverage(vec2 _p, vec4 _box, vec4 _radii, float _aa)
{
    vec2 extent = _box.zw * 0.5;
    return coverage(sdRoundRect(_p - (_box.xy + extent), extent, _radii), _aa);
}

vec4 premultiply(vec4 _texel, float _premultiplied)
{
    return _premultiplied < 0.5 ? vec4(_texel.rgb * _texel.a, _texel.a) : _texel;
}

float sdEllipse(vec2 _p, vec2 _radii)
{
    vec2 r = max(_radii, vec2(0.0001, 0.0001));
    float k0 = length(_p / r);
    float k1 = length(_p / (r * r));
    // The estimate is 0/0 at the centre, which would cover half of a pixel centred there.
    return k1 > 0.0 ? k0 * (k0 - 1.0) / k1 : -min(r.x, r.y);
}

vec2 erf2(vec2 _x)
{
    vec2 s = sign(_x);
    vec2 a = abs(_x);
    vec2 d = 1.0 + (0.278393 + (0.230389 + 0.078108 * (a * a)) * a) * a;
    d *= d;
    return s - s / (d * d);
}

// Exact across x through erf; across y, four samples where the kernel has weight.
float blurredBox(vec2 _p, vec2 _half, float _radius, float _sigma)
{
    float start = clamp(-3.0 * _sigma, _p.y - _half.y, _p.y + _half.y);
    float end = clamp(3.0 * _sigma, _p.y - _half.y, _p.y + _half.y);
    float dy = (end - start) / 4.0;
    float y = start + dy * 0.5;
    float weight = dy / (2.50662827 * _sigma);
    float scale = 0.70710678 / _sigma;
    float total = 0.0;
    for (int i = 0; i < 4; i++)
    {
        float row = _p.y - y;
        float corner = min(_half.y - _radius - abs(row), 0.0);
        float extent = _half.x - _radius + sqrt(max(_radius * _radius - corner * corner, 0.0));
        vec2 span = 0.5 + 0.5 * erf2((vec2(_p.x, _p.x) + vec2(-extent, extent)) * scale);
        total += (span.y - span.x) * exp(-(y * y) / (2.0 * _sigma * _sigma)) * weight;
        y += dy;
    }
    return total;
}

// Layer pixels to UVs, kept inside the layer's box of a shared target.
vec2 layerUv(vec2 _pixel)
{
    vec2 at = clamp(_pixel, vec2(0.5, 0.5), u_layerRegion.zw - vec2(0.5, 0.5)) + u_layerRegion.xy;
    return at * u_layer.xy;
}

// Clear past the layer's box, as a target of its own would read.
float layerAlpha(vec2 _pixel)
{
    vec2 inside = step(vec2(0.0, 0.0), _pixel) * step(_pixel, u_layerRegion.zw);
    return texture2DLod(s_layer, layerUv(_pixel), 0.0).a * inside.x * inside.y;
}

// A spread averages two samples that far either side across the blur, covering the rows a coarser target skips.
float blurredAlpha(vec2 _pixel, vec2 _direction, float _sigma, float _spread)
{
    if (dot(_direction, _direction) < 0.5)
    {
        return layerAlpha(_pixel);
    }

    float sigma = max(_sigma, 0.5);
    float spacing = max(1.0, sigma / 8.0);
    vec2 stride = _direction * spacing;
    vec2 across = vec2(_direction.y, _direction.x) * _spread;
    float total = 0.0;
    float weights = 0.0;
    for (int i = -24; i <= 24; i++)
    {
        float x = float(i) * spacing;
        float w = exp(-(x * x) / (2.0 * sigma * sigma));
        vec2 at = _pixel + stride * float(i);
        float alpha = layerAlpha(at - across);
        if (_spread > 0.0)
        {
            alpha = 0.5 * (alpha + layerAlpha(at + across));
        }
        total += alpha * w;
        weights += w;
    }
    return total / weights;
}

vec2 insideWindow(vec2 _uv, vec4 _window, vec3 _sample)
{
    return clamp(_uv, _window.xy + _sample.yz, _window.zw - _sample.yz);
}

// The map takes local space to texture space; the ramp's v names the array layer, or is -1 for a plain texture.
vec4 imagePaint(vec4 _map, float _layer, vec2 _p, vec4 _color, vec4 _window, vec3 _sample, float _premultiplied)
{
    vec2 uv = insideWindow(_p * _map.xy + _map.zw, _window, _sample);
    vec4 texel = _layer > -0.5
        ? texture2DArrayLod(s_paintArray, vec3(uv, _layer), _sample.x)
        : texture2DLod(s_paint, uv, _sample.x);
    return premultiply(texel, _premultiplied) * _color;
}

// Taken as a minimum with a shape's coverage: where the window's edge lies on the shape's, a product would fade it twice.
float imageReach(vec4 _map, vec4 _window, vec2 _p, float _aa)
{
    vec2 low = (_window.xy - _map.zw) / _map.xy;
    vec2 high = (_window.zw - _map.zw) / _map.xy;
    vec2 past = max(low - _p, _p - high);
    return coverage(max(past.x, past.y), _aa);
}

vec4 paint(float _kind, vec4 _gradient, float _rampV, vec2 _p, vec4 _color, vec4 _window, vec3 _sample, float _image, float _premultiplied)
{
    if (_image > 0.5)
    {
        return imagePaint(_gradient, _rampV, _p, _color, _window, _sample, _premultiplied);
    }
    if (_kind < 1.5)
    {
        return _color;
    }

    float t;
    if (_kind < 2.5)
    {
        vec2 axis = _gradient.zw - _gradient.xy;
        t = dot(_p - _gradient.xy, axis) / max(dot(axis, axis), 0.000001);
    }
    else
    {
        t = length((_p - _gradient.xy) / max(_gradient.zw, vec2(0.000001, 0.000001)));
    }

    float u = clamp(t, 0.0, 1.0) * (255.0 / 256.0) + 0.5 / 256.0;
    return texture2DLod(s_ramp, vec2(u, _rampV), 0.0) * _color;
}

void main()
{
    // Derivatives are taken before any branch: every branch below samples with an explicit LOD.
    vec2 p = v_local.xy;
    float aa = max(length(fwidth(p)) * 0.70710678, 0.0001);
    vec2 texelsAcross = dFdx(v_local.zw) * v_shape.xy;
    vec2 texelsDown = dFdy(v_local.zw) * v_shape.xy;

    float kind = v_params.x;
    float flags = v_params.y;
    float fillKind = mod(flags, 4.0);
    float strokeKind = mod(floor(flags / 4.0), 4.0);
    float premultiplied = mod(floor(flags / 16.0), 2.0);
    float fillImage = mod(floor(flags / 32.0), 2.0);
    float strokeImage = mod(floor(flags / 64.0), 2.0);
    vec4 fill = paint(fillKind, v_fillGradient, v_params.z, p, v_fill, v_window, v_sample, fillImage, premultiplied);
    float fillReach = fillImage > 0.5 ? imageReach(v_fillGradient, v_window, p, aa) : 1.0;
    vec4 color;

    if (kind < 1.5)
    {
        float outer;
        float inner;
        if (kind < 0.5)
        {
            outer = boxCoverage(p, v_bounds, v_shape, aa);
            // An inner box of no area would still cover half of a pixel centred on it.
            inner = v_inner.z > 0.0 && v_inner.w > 0.0 ? min(outer, boxCoverage(p, v_inner, v_innerRadii, aa)) : 0.0;
        }
        else
        {
            vec2 center = v_bounds.xy + v_bounds.zw * 0.5;
            vec2 radii = v_bounds.zw * 0.5;
            outer = coverage(sdEllipse(p - center, radii), aa);
            vec2 innerRadii = radii - vec2(v_inner.x, v_inner.x);
            inner = v_inner.x <= 0.0 ? outer
                : innerRadii.x > 0.0 && innerRadii.y > 0.0 ? min(outer, coverage(sdEllipse(p - center, innerRadii), aa))
                : 0.0;
        }
        vec4 stroke = paint(strokeKind, v_strokeGradient, v_params.w, p, v_stroke, v_window, v_sample, strokeImage, premultiplied);
        float strokeReach = strokeImage > 0.5 ? imageReach(v_strokeGradient, v_window, p, aa) : 1.0;
        color = fill * min(inner, fillReach) + stroke * min(outer - inner, strokeReach);
    }
    else if (kind < 2.5)
    {
        vec2 d = p - (v_bounds.xy + v_bounds.zw * 0.5);
        float ring = abs(length(d) - (v_shape.x - v_shape.y * 0.5)) - v_shape.y * 0.5;
        vec2 q = vec2(v_shape.z * d.x - v_shape.w * d.y, v_shape.w * d.x + v_shape.z * d.y);
        q.x = abs(q.x);
        vec2 sector = v_inner.xy;
        float wedge = length(q - sector * max(dot(q, sector), 0.0)) * sign(sector.y * q.x - sector.x * q.y);
        float distance = v_inner.z > 0.5 ? ring : max(ring, wedge);
        color = fill * min(coverage(distance, aa), fillReach);
    }
    else if (kind > 10.5)
    {
        float through = kind > 11.5 ? v_inner.x : boxCoverage(p, v_bounds, v_shape, aa);
        color = texture2DLod(s_layer, layerUv(v_local.zw), 0.0) * fill.a * through;
    }
    else if (kind > 9.5)
    {
        color = v_fill * blurredAlpha(v_local.zw, v_shape.xy, v_shape.z, v_shape.w);
    }
    else if (kind > 8.5)
    {
        color = texture2DLod(s_layer, layerUv(v_local.zw), 0.0) * fill.a * fillReach;
    }
    else if (kind > 7.5)
    {
        color = fill * min(v_inner.x, fillReach);
    }
    else if (kind < 3.5 || kind > 6.5)
    {
        vec2 uv = insideWindow(v_local.zw, v_window, v_sample);
        vec4 texel = kind < 3.5
            ? texture2DLod(s_texture, uv, v_sample.x)
            : texture2DArrayLod(s_textureArray, vec3(uv, v_shape.x), v_sample.x);
        color = premultiply(texel, premultiplied) * v_fill * boxCoverage(p, v_bounds, vec4(0.0, 0.0, 0.0, 0.0), aa);
    }
    else if (kind < 4.5)
    {
        // Half a texel over, the field's border reads only the cleared gap beside it, never the next glyph.
        vec2 halfTexel = 0.5 / v_shape.xy;
        float texel = texture2DLod(s_glyph, v_local.zw, 0.0).r;
        vec2 slope = vec2(
            texture2DLod(s_glyph, v_local.zw + vec2(halfTexel.x, 0.0), 0.0).r,
            texture2DLod(s_glyph, v_local.zw + vec2(0.0, halfTexel.y), 0.0).r) - texel;
        color = fill * min(glyphCoverage(texel, glyphTexelsPerPixel(slope, texelsAcross, texelsDown)), fillReach);
    }
    else
    {
        vec2 extent = v_inner.zw * 0.5;
        vec2 q = p - (v_inner.xy + extent);
        float shadow = blurredBox(q, extent, cornerRadius(q, extent, v_innerRadii), max(v_strokeGradient.x, aa * 0.5));
        float caster = boxCoverage(p, v_bounds, v_shape, aa);
        color = fill * min(kind < 5.5 ? shadow * (1.0 - caster) : caster * (1.0 - shadow), fillReach);
    }

    float inside = step(v_clip.x, v_device.x) * step(v_clip.y, v_device.y) * step(v_device.x, v_clip.z) * step(v_device.y, v_clip.w);
    gl_FragColor = color * inside;
}
